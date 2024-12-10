namespace Jinaga.test.Purge;

using System.Linq;
using Jinaga.Extensions;
using Jinaga.Test.Model.Order;

public class RealTimePurgeTest
{
    [Fact]
    public async Task WhenPurgeConditionIsNotMet_SuccessorsExist()
    {
        var jinagaClient = JinagaClient.Create(opt =>
        {
            opt.PurgeConditions = p => p
                .Purge<Order>().WhenExists<OrderCancelled>(oc => oc.order);
        });

        var store = await jinagaClient.Fact(new Store("storeId"));
        var catalog = await jinagaClient.Fact(new Catalog(store, "catalogId"));
        var product1 = await jinagaClient.Fact(new Product(catalog, "product1"));
        var product2 = await jinagaClient.Fact(new Product(catalog, "product2"));
        var item1 = await jinagaClient.Fact(new Item(product1, 1));
        var item2 = await jinagaClient.Fact(new Item(product2, 1));
        var order = await jinagaClient.Fact(new Order(store, [item1, item2]));
        var shipped = await jinagaClient.Fact(new OrderShipped(order, DateTime.Now));

        var shipmentsInOrder = Given<Order>.Match(order =>
            order.Successors().OfType<OrderShipped>(shipped => shipped.order)
                .Select(s => jinagaClient.Hash(s))
        );

        var shipments = await jinagaClient.Query(shipmentsInOrder, order);
        shipments.Should().ContainSingle().Which.Should().Be(jinagaClient.Hash(shipped));
    }

    [Fact]
    public async Task WhenPurgeConditionIsMet_SuccessorsDoNotExist()
    {
        var jinagaClient = JinagaClient.Create(opt =>
        {
            opt.PurgeConditions = p => p
                .Purge<Order>().WhenExists<OrderCancelled>(oc => oc.order);
        });

        var store = await jinagaClient.Fact(new Store("storeId"));
        var catalog = await jinagaClient.Fact(new Catalog(store, "catalogId"));
        var product1 = await jinagaClient.Fact(new Product(catalog, "product1"));
        var product2 = await jinagaClient.Fact(new Product(catalog, "product2"));
        var item1 = await jinagaClient.Fact(new Item(product1, 1));
        var item2 = await jinagaClient.Fact(new Item(product2, 1));
        var order = await jinagaClient.Fact(new Order(store, [item1, item2]));
        var cancelled = await jinagaClient.Fact(new OrderCancelled(order, DateTime.Now));

        var shipmentsInOrder = Given<Order>.Match(order =>
            order.Successors().OfType<OrderShipped>(shipped => shipped.order)
                .Select(s => jinagaClient.Hash(s))
        );

        var shipments = await jinagaClient.Query(shipmentsInOrder, order);
        shipments.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenPurgeConditionIsMet_TriggerFactExists()
    {
        var jinagaClient = JinagaClient.Create(opt =>
        {
            opt.PurgeConditions = p => p
                .Purge<Order>().WhenExists<OrderCancelled>(oc => oc.order);
        });

        var store = await jinagaClient.Fact(new Store("storeId"));
        var catalog = await jinagaClient.Fact(new Catalog(store, "catalogId"));
        var product1 = await jinagaClient.Fact(new Product(catalog, "product1"));
        var product2 = await jinagaClient.Fact(new Product(catalog, "product2"));
        var item1 = await jinagaClient.Fact(new Item(product1, 1));
        var item2 = await jinagaClient.Fact(new Item(product2, 1));
        var order = await jinagaClient.Fact(new Order(store, [item1, item2]));
        var cancelled = await jinagaClient.Fact(new OrderCancelled(order, DateTime.Now));

        var cancellationsInOrder = Given<Order>.Match(order =>
            order.Successors().OfType<OrderCancelled>(cancelled => cancelled.order)
                .Select(s => jinagaClient.Hash(s))
        );
        var cancellations = await jinagaClient.Query(cancellationsInOrder, order);
        cancellations.Should().ContainSingle().Which.Should().Be(jinagaClient.Hash(cancelled));
    }

    [Fact]
    public async Task WhenPurgeConditionIsMet_AncestorOfTriggerFactExists()
    {
        var jinagaClient = JinagaClient.Create(opt =>
        {
            opt.PurgeConditions = p => p
                .Purge<Order>().WhenExists<OrderCancelledReason>(ocr => ocr.orderCancelled.order);
        });

        var store = await jinagaClient.Fact(new Store("storeId"));
        var catalog = await jinagaClient.Fact(new Catalog(store, "catalogId"));
        var product1 = await jinagaClient.Fact(new Product(catalog, "product1"));
        var product2 = await jinagaClient.Fact(new Product(catalog, "product2"));
        var item1 = await jinagaClient.Fact(new Item(product1, 1));
        var item2 = await jinagaClient.Fact(new Item(product2, 1));
        var order = await jinagaClient.Fact(new Order(store, [item1, item2]));
        var cancelled = await jinagaClient.Fact(new OrderCancelled(order, DateTime.Now));
        var reason = await jinagaClient.Fact(new OrderCancelledReason(cancelled, "reason"));

        var cancellationsInOrder = Given<Order>.Match(order =>
            order.Successors().OfType<OrderCancelled>(cancelled => cancelled.order)
                .Select(s => jinagaClient.Hash(s))
        );
        var cancellations = await jinagaClient.Query(cancellationsInOrder, order);
        cancellations.Should().ContainSingle().Which.Should().Be(jinagaClient.Hash(cancelled));
    }
}