namespace Jinaga.Tool.Test;

[FactType("Tool.Test.Site")]
public record Site(User creator, string identifier) { }

[FactType("Tool.Test.Post")]
public record Post(Site site, string path) { }

[FactType("Tool.Test.PostDeleted")]
public record PostDeleted(Post post) { }

/// <summary>
/// The shape the tool could not interpret. Each builder carries the same name
/// as the method that describes it, so a test can hand the builder to
/// JinagaTestOptions while the tool reads the description.
/// </summary>
public static class OverloadedConfig
{
    public static string Authorization() => AuthorizationRules.Describe(Authorization);

    public static AuthorizationRules Authorization(AuthorizationRules r) => r
        .Any<User>()
        .Type<Site>(site => site.creator)
        .Type<Post>(post => post.site.creator);

    public static string Distribution() => DistributionRules.Describe(Distribution);

    public static DistributionRules Distribution(DistributionRules r) => r
        .Share(Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            select post
        ))
        .With(site => site.creator);

    public static string Purge() => PurgeConditions.Describe(Purge);

    public static PurgeConditions Purge(PurgeConditions r) => r
        .Purge<Post>().WhenExists<PostDeleted>(deleted => deleted.post);
}

/// <summary>
/// The shape the tool already handled: one parameterless method per rule, with
/// the builders named apart from them.
/// </summary>
public static class ParameterlessOnlyConfig
{
    public static string Authorization() => AuthorizationRules.Describe(Authorize);

    private static AuthorizationRules Authorize(AuthorizationRules r) => r
        .Any<User>()
        .Type<Site>(site => site.creator);

    public static string Distribution() => DistributionRules.Describe(Distribute);

    private static DistributionRules Distribute(DistributionRules r) => r
        .Share(Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            select post
        ))
        .WithEveryone();

    // Declares no Purge at all, which the policy command has always tolerated.
}

/// <summary>
/// An author who wrote the builder and forgot the method that describes it.
/// </summary>
public static class ParameterizedOnlyConfig
{
    public static AuthorizationRules Authorization(AuthorizationRules r) => r
        .Any<User>();
}
