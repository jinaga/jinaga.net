namespace Jinaga.Test.Model.Workspaces;

// A fact that links two others, where authority lives on a workspace reachable from each side.
// A rule that constrains both endpoints joins one unknown to two predecessor paths of the given.

[FactType("Workspaces.Workspace")]
public record Workspace(User creator, string identifier) { }

[FactType("Workspaces.Owner")]
public record Owner(Workspace workspace, User user) { }

[FactType("Workspaces.Item")]
public record Item(Workspace workspace, string label) { }

[FactType("Workspaces.Link")]
public record Link(Item item, Item parent) { }

[FactType("Workspaces.Archive")]
public record Archive(Workspace workspace) { }
