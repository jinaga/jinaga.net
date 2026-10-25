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

/// <summary>Undoes a retirement, so a mirror can leave a collection and come back.</summary>
[FactType("Mirror.Restored")]
public record MirrorRestored(MirrorRetired retired) { }

/// <summary>
/// Makes an account a row of its own accord, so a parent can be added by a listener after
/// the initial read while its children already exist.
/// </summary>
[FactType("Mirror.Activated")]
public record Activated(Account account) { }

/// <summary>
/// Reported by an account within a tenant, so a specification can correlate one fact with
/// two others by walking forward to each of them.
/// </summary>
[FactType("Mirror.Ticket")]
public record Ticket(Tenant tenant, Account reporter, string summary) { }

[FactType("Mirror.Ticket.Closed")]
public record TicketClosed(Ticket ticket) { }
