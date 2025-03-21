using Jinaga.DefaultImplementations;
using Jinaga.Extensions;
using Jinaga.Store.SQLite.Test.Model.Order;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Store.SQLite.Test.Purge;

public class RealTimePurgeSkipLevelTest
{
    private readonly SQLiteStore store;
    private readonly JinagaClient j;

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "RealTimePurgeSkipLevelTest.db");

    public RealTimePurgeSkipLevelTest()
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        PurgeConditions purgeConditions = PurgeConditions.Empty
            .Purge<Order>().WhenExists<OrderCancelledReason>(ocr => ocr.orderCancelled.order);
        store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        var options = new JinagaClientOptions();
        j = new JinagaClient(store, new LocalNetwork(), purgeConditions.Validate(), NullLoggerFactory.Instance, options);
    }

    [Fact]
    public async Task WhenPurgeConditionIsMet_SuccessorFactDoesNotExist()
    {
        var store = await j.Fact(new Model.Order.Store("storeId"));
        var catalog = await j.Fact(new Catalog(store, "catalogId"));
        var product1 = await j.Fact(new Product(catalog, "product1"));
        var product2 = await j.Fact(new Product(catalog, "product2"));
        var item1 = await j.Fact(new Item(product1, 1));
        var item2 = await j.Fact(new Item(product2, 1));
        var order = await j.Fact(new Order(store, [item1, item2]));
        var shipment = await j.Fact(new OrderShipped(order, DateTime.Now));
        var cancelled = await j.Fact(new OrderCancelled(order, DateTime.Now));
        var reason = await j.Fact(new OrderCancelledReason(cancelled, "reason"));

        var shipmentsInOrder = Given<Order>.Match(order =>
            order.Successors().OfType<OrderShipped>(shipped => shipped.order)
                .Select(s => j.Hash(s))
        );

        var shipments = await j.Query(shipmentsInOrder, order);
        shipments.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenPurgeConditionIsMet_AncestorOfTriggerFactExists()
    {
        var store = await j.Fact(new Model.Order.Store("storeId"));
        var catalog = await j.Fact(new Catalog(store, "catalogId"));
        var product1 = await j.Fact(new Product(catalog, "product1"));
        var product2 = await j.Fact(new Product(catalog, "product2"));
        var item1 = await j.Fact(new Item(product1, 1));
        var item2 = await j.Fact(new Item(product2, 1));
        var order = await j.Fact(new Order(store, [item1, item2]));
        var cancelled = await j.Fact(new OrderCancelled(order, DateTime.Now));
        var reason = await j.Fact(new OrderCancelledReason(cancelled, "reason"));

        var cancellationsInOrder = Given<Order>.Match(order =>
            order.Successors().OfType<OrderCancelled>(cancelled => cancelled.order)
                .Select(s => j.Hash(s))
        );
        var cancellations = await j.Query(cancellationsInOrder, order);
        cancellations.Should().ContainSingle().Which.Should().Be(j.Hash(cancelled));
    }
}
