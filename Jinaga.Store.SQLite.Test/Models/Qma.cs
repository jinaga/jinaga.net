namespace Jianga.Store.SQLite.Test.Models;

/// <summary>Represents an organization, or business unit within an organization, that might need separate data.</summary>
[FactType("Qma.Company")]
public record QmaCompany(string Id);

/// <summary>Represents a deployment environment.</summary>
[FactType("Qma.AppEnvironment")]
public record AppEnvironment(QmaCompany Company, string Id);

/// <summary>A unique identifier representing the current device.</summary>
[FactType("Qma.DeviceId")]
public record DeviceId(AppEnvironment Environment, string Value);

/// <summary>A predecessor fact that represents unsynchronized state on this device.</summary>
[FactType("Qma.DeviceSession")]
public record DeviceSession(DeviceId DeviceId, string SessionId);

[FactType("Qma.Order.Details")]
public record OrderDetails(
    string identifier,
    OrderSourceKey School,
    Order Order
    /* ...snip... this fact is quite large */
);

[FactType("Qma.UserName")]
public record UserName(string Value);

[FactType("Qma.Order.Saved")]
public record SavedOrder(
    DeviceSession Session,
    OrderDetails Details,
    UserName SavedBy,
    DateTime SavedOn,
    SavedOrder[] History
);

[FactType("Qma.Order")]
public record Order(
    string identifier
);

[FactType("Qma.Order.Received")]
public record ReceivedOrder(
    Order Order
);

[FactType("Qma.Order.Source.Key")]
public record OrderSourceKey(string Value);