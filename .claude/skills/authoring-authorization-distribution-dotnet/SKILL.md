---
name: authoring-authorization-distribution-dotnet
description: >
  Use when deciding or implementing who can create a fact (authorization)
  or who can read it back (distribution) in a Jinaga.NET app — writing
  `AuthorizationRules` or `DistributionRules`, giving a role its
  permissions, or deploying rules to a replicator.
---

# Authorization and distribution rules in .NET

Two independently enforced concerns: **authorization** decides who may
*create* a fact, and **distribution** decides who may *read* it. Design each
rule as a Factual specification first (`authoring-factual-specifications`
covers the shape and the constraints every rule must meet), then write it
here.

## Keep the rules in a `JinagaConfig` class

```csharp
public static class JinagaConfig
{
    public static string Authorization() => AuthorizationRules.Describe(Authorization);
    public static string Distribution() => DistributionRules.Describe(Distribution);

    public static AuthorizationRules Authorization(AuthorizationRules r) => r
        .Any<User>()
        .Type<Project>(project => project.creator)
        .Type<ProjectInvitation>(invitation => invitation.project.creator)
        .Type<ProjectMember>(member => member.invitation.invitee);

    public static DistributionRules Distribution(DistributionRules r) => r
        .Share(ProjectSpecifications.Tasks).With(project => project.creator);
}
```

The parameterless methods return the rules as Factual text, which is what
`dotnet jinaga` reads (below). The builder overloads are what the
application and the tests pass to the client, so all three use one
definition.

## Authorization: deny by default

- **Every fact type needs a rule**, `Jinaga.User` included. `.Any<User>()` is
  the usual choice: a user fact is the caller's own public key.
- **`.Type<T>(x => x.owner)`** allows the user a predecessor path reaches.
- **`.Type<T>((x, facts) => query)`** allows the users a query projects, for
  anyone not directly on a predecessor path.
- **Several rules for one type allow anyone any of them names.** "The
  project's creator or one of its members" is two `.Type<TaskComment>`
  rules.

Name a membership once on the fact type, and follow it from a declared
label:

```csharp
[FactType("TaskTracker.Project")]
public record Project(User creator, DateTime createdAt)
{
    public IQueryable<User> MemberUsers => Relation.Define(facts =>
        from member in facts.OfType<ProjectMember>()
        where member.invitation.project == this && !member.IsRemoved
        from user in facts.OfType<User>()
        where user == member.invitation.invitee
        select user);
}

[FactType("TaskTracker.Project.Member")]
public record ProjectMember(ProjectInvitation invitation, DateTime acceptedAt)
{
    public Condition IsRemoved => Condition.Define(facts =>
        facts.Any<ProjectMemberRemoval>(removal => removal.member == this));
}
```

```csharp
// Members may comment on tasks in their project.
.Type<TaskComment>((comment, facts) =>
    from project in facts.OfType<Project>()
    where project == comment.task.project
    from user in project.MemberUsers
    select user)
```

`project` is declared before `MemberUsers` is followed: a relation is
followed from a label, never from a predecessor path such as
`comment.task.project.MemberUsers`.

**A fact that links two owned facts** checks that both have the same owner.
A task assignment names a task and a member; without the second condition,
a project creator could assign a member of another project, and
distribution would then send that member this project's tasks:

```csharp
.Type<TaskAssignment>((assignment, facts) =>
    from project in facts.OfType<Project>()
    where project == assignment.task.project &&
        project == assignment.member.invitation.project
    from user in facts.OfType<User>()
    where user == project.creator
    select user)
```

**A policy no rule can express is still a fact.** If an action needs an
approval the actor cannot grant themselves, model the approval as its own
fact type with its own rule, rather than checking the condition in
application code.

## Distribution: share what the app queries

```csharp
.Share(ProjectSpecifications.Tasks).With(project => project.creator)
.Share(ProjectSpecifications.Tasks).With(Given<Project>.Match(project => project.MemberUsers))
.Share(UserSpecifications.Invitations).With(user => user)
.Share(ProjectSpecifications.PublicSummary).WithEveryone()
```

`.Share(specification)` names what is read, and `.With(...)` names the users
it reaches from the same given: a predecessor selector, a
`Specification<T, User>`, or `.WithEveryone()`. Share the specifications the
application runs, so each screen's query is covered by exactly the rule
written for it. A user who should see only their own assignments gets a
specification that starts from their `User` and nothing broader.

## Enforce on the client and at the replicator

Give the client the same rules, so a write the replicator would refuse is
refused when it is saved:

```csharp
var j = JinagaClient.Create(options =>
{
    options.HttpEndpoint = new Uri(settings.ReplicatorUrl);
    options.AuthorizationRules = AuthorizationRules.Build(JinagaConfig.Authorization);
});
```

`await j.Fact(...)` then throws `AuthorizationException` for a refused
fact.

Distribution decisions come in two kinds:

- **Structural**: no rule shares this specification (`no-matching-rule`),
  or the specification is narrower than the rule that would share it
  (`spec-more-restrictive-than-rule`). These never change on their own, so
  `j.Query(...)` throws `DistributionDeniedException`. Fix the specification
  or add the share.
- **Who the user is**: the user isn't among those the rule names
  (`principal-excluded`), or nobody is logged in (`not-authenticated`). A
  later fact can change these, so `j.Query(...)` returns an empty result.
  `j.QueryWithDiagnostics(...)` returns the decision alongside the results,
  and `j.OnDistributionDiagnostic` reports every decision from `Query`,
  `Watch` and `Subscribe`.

Deploy the rules from the built assembly. The tool finds the `JinagaConfig`
class and calls its parameterless methods:

```bash
dotnet jinaga print authorization ./bin/Release/net10.0/TaskTracker.Core.dll
dotnet jinaga deploy authorization ./bin/Release/net10.0/TaskTracker.Core.dll <endpoint> <secret>
dotnet jinaga deploy distribution  ./bin/Release/net10.0/TaskTracker.Core.dll <endpoint> <secret>
```

For a self-hosted replicator, `dotnet jinaga print policy <assembly>` writes
the policy file to mount instead. Run either from the build, so the deployed
rules are always the ones in the code. Test both kinds of rule with
`testing-jinaga-dotnet`.
