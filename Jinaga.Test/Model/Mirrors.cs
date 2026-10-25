namespace Jinaga.Test.Model;

/// <summary>
/// Shapes whose membership turns on a negation, for the notifications a watch owes when a
/// level of the specification gains or loses its solution.
/// </summary>

[FactType("Mirror.Tenant")]
public record Tenant(string identifier) { }

[FactType("Mirror.Account")]
public record Account(Tenant tenant, string identifier) { }

[FactType("Mirror.Mirror")]
public record Mirror(Account account, string host) { }

[FactType("Mirror.Retired")]
public record MirrorRetired(Mirror mirror) { }

[FactType("Mirror.Chore")]
public record Chore(Tenant tenant, string description) { }

/// <summary>
/// A completion that names a chore in each of its two roles, so one fact falsifies two
/// sibling conditions of the same chore at once.
/// </summary>
[FactType("Mirror.ChoreDone")]
public record ChoreDone(Chore chore, Chore witness) { }

[FactType("Mirror.Block")]
public record Block(Chore chore) { }

[FactType("Mirror.Override")]
public record Override(Block block) { }

[FactType("Mirror.Reason")]
public record Reason(Override reversal) { }
