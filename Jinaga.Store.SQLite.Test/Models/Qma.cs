namespace Jinaga.Store.SQLite.Test.Models;

[FactType("Qma.UserName")]
public record UserName(string Value);

/// <summary>
/// Represents an organization, or business unit within an organization, that might need separate data.
/// Can be thought of as a "tenant." In practice, serves as a convenient root-level "given" for Jinaga queries,
/// though <see cref="AppEnvironment"/> is usually the better choice.
/// </summary>
[FactType("Qma.Company")]
public record QmaCompany(string Id);

/// <summary>
/// Represents a deployment environment, to allow server-side logic to easily ignore facts that may have
/// (intentionally or unintentionally) been stored in the DB but were meant for a different environment.
/// </summary>
[FactType("Qma.AppEnvironment")]
public record AppEnvironment(QmaCompany Company, string Id);

/// <summary>A unique identifier representing the current device.</summary>
/// <remarks>This fact is device-specific, and unlikely to ever need to be pushed to a shared replicator.</remarks>
[FactType("Qma.DeviceId")]
public record DeviceId(AppEnvironment Environment, string Value);

/// <summary>
/// A predecessor fact that represents unsynchronized state on this device. Orders that
/// are not yet ready to be pushed to QOS, for example, are associated with a device session.
/// </summary>
/// <remarks>This fact is device-specific, and unlikely to ever need to be pushed to a shared replicator.</remarks>
[FactType("Qma.DeviceSession")]
public record DeviceSession(DeviceId DeviceId, string SessionId);

[FactType("Qma.Order.Details")]
public record OrderDetails(
    string identifier,
    OrderSourceKey School,
    Order Order
    /* ...snip... this fact is quite large */
);

[FactType("Qma.Order.Saved")]
public record SavedOrder(
    DeviceSession Session,
    OrderDetails Details,
    UserName SavedBy, // This fact contains a string that always looks something like `david.schwartz`
    DateTime SavedOn,
    SavedOrder[] History
);

/// <summary>When added, indicates that the given order is ready to sync.</summary>
[FactType("Qma.Order.ReadyToSync")]
public record OrderReadyToSync(SavedOrder SavedOrder);

/// <summary>A unique identifier representing an order.</summary>
/// <remarks>Including the <see cref="AppEnvironment"/> simplifies queries, which nearly always need to filter based on environment, as well as simplifying many facts, like <see cref="OrderReceived"/>.</remarks>
[FactType("Qma.Order")]
public record Order(AppEnvironment Environment, string OrderId);

/// <summary>Acknowledgment that the order has been received by the central server, and the device no longer needs to preserve its related data.</summary>
[FactType("Qma.Order.Received")]
public record OrderReceived(Order Order, DateTime ReceivedOn);

[FactType("Qma.Order.Source.Key")]
public record OrderSourceKey(string Value);