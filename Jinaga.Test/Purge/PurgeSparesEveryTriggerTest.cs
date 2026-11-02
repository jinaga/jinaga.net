using System.Collections.Generic;
using System.Linq;
using Jinaga.Extensions;
using Jinaga.Test.Model.Order;

namespace Jinaga.Test.Purge;

/// <summary>
/// A purge spares every trigger of every purge condition on the root's type, not only the
/// one trigger whose arrival fired it.
///
/// Each test states both what <c>j.Query</c> reads and what a watch of the same
/// specification holds, because agreement between the two is the property at stake. A purge
/// sends no notification, so a trigger the store has dropped stays in the watch, and the
/// disagreement is the symptom a reader sees.
/// </summary>
public class PurgeSparesEveryTriggerTest
{
    private static Specification<Order, string> CancellationsHashedBy(JinagaClient j) =>
        Given<Order>.Match(order =>
            order.Successors().OfType<OrderCancelled>(cancelled => cancelled.order)
                .Select(cancelled => j.Hash(cancelled))
        );

    private static Specification<Order, string> ShipmentsHashedBy(JinagaClient j) =>
        Given<Order>.Match(order =>
            order.Successors().OfType<OrderShipped>(shipped => shipped.order)
                .Select(shipped => j.Hash(shipped))
        );

    [Fact]
    public async Task WhenASecondTriggerArrives_TheFirstTriggerSurvives()
    {
        var j = JinagaClient.Create(opt =>
        {
            opt.PurgeConditions = p => p
                .Purge<Order>().WhenExists<OrderCancelled>(oc => oc.order);
        });

        var order = await GivenAnOrder(j);
        var first = await j.Fact(new OrderCancelled(order, DateTime.Now));

        var cancellations = CancellationsHashedBy(j);
        var watched = new List<string>();
        var watch = j.Watch(cancellations, order, hash => watched.Add(hash));
        await watch.Loaded;

        var second = await j.Fact(new OrderCancelled(order, DateTime.Now.AddMinutes(1)));

        try
        {
            var queried = await j.Query(cancellations, order);
            queried.Should().BeEquivalentTo(new[] { j.Hash(first), j.Hash(second) });
            watched.Should().BeEquivalentTo(queried);
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task WhenAnotherConditionFires_TheFirstConditionsTriggerSurvives()
    {
        var j = JinagaClient.Create(opt =>
        {
            opt.PurgeConditions = p => p
                .Purge<Order>().WhenExists<OrderCancelled>(oc => oc.order)
                .Purge<Order>().WhenExists<OrderShipped>(os => os.order);
        });

        var order = await GivenAnOrder(j);
        var cancelled = await j.Fact(new OrderCancelled(order, DateTime.Now));

        var cancellations = CancellationsHashedBy(j);
        var watched = new List<string>();
        var watch = j.Watch(cancellations, order, hash => watched.Add(hash));
        await watch.Loaded;

        var shipped = await j.Fact(new OrderShipped(order, DateTime.Now.AddMinutes(1)));

        try
        {
            var queried = await j.Query(cancellations, order);
            queried.Should().ContainSingle().Which.Should().Be(j.Hash(cancelled));
            watched.Should().BeEquivalentTo(queried);

            var shipments = await j.Query(ShipmentsHashedBy(j), order);
            shipments.Should().ContainSingle().Which.Should().Be(j.Hash(shipped));
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task WhenAConditionOfAnotherTypeFires_ItsTriggersAreNotSpared()
    {
        // A condition on Catalog has nothing to say about which of an Order's descendants
        // survive, so sparing stays scoped to the conditions on the root's own type.
        var j = JinagaClient.Create(opt =>
        {
            opt.PurgeConditions = p => p
                .Purge<Order>().WhenExists<OrderCancelled>(oc => oc.order);
        });

        var order = await GivenAnOrder(j);
        var shipped = await j.Fact(new OrderShipped(order, DateTime.Now));
        await j.Fact(new OrderCancelled(order, DateTime.Now.AddMinutes(1)));

        var shipments = await j.Query(ShipmentsHashedBy(j), order);
        shipments.Should().BeEmpty();
    }

    private static async Task<Order> GivenAnOrder(JinagaClient j)
    {
        var store = await j.Fact(new Model.Order.Store("storeId"));
        var catalog = await j.Fact(new Catalog(store, "catalogId"));
        var product = await j.Fact(new Product(catalog, "product1"));
        var item = await j.Fact(new Item(product, 1));
        return await j.Fact(new Order(store, [item]));
    }
}
