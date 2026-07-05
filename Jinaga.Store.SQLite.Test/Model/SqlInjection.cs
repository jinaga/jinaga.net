namespace Jinaga.Store.SQLite.Test.Model.SqlInjection;

[FactType("O'Brien.Order--")]
public record MaliciousOrder(string identifier);

[FactType("O'Brien.Order.Item--")]
public record MaliciousOrderItem(MaliciousOrder order, string sku);
