namespace Jinaga.Store.SQLite.Test.Model.Order;

[FactType("Store")]
record Store(string identifier);

[FactType("Catalog")]
record Catalog(Store store, string identifier);

[FactType("Product")]
record Product(Catalog Catalog, string sku);

[FactType("Price")]
record Price(Product product, decimal value, Price[] prior);

[FactType("Item")]
record Item(Product product, int quantity);

[FactType("Order")]
record Order(Store store, Item[] items);

[FactType("Order.Cancelled")]
record OrderCancelled(Order order, DateTime cancelledAt);

[FactType("Order.Cancelled.Reason")]
record OrderCancelledReason(OrderCancelled orderCancelled, string reason);

[FactType("Order.Shipped")]
record OrderShipped(Order order, DateTime shippedAt);
