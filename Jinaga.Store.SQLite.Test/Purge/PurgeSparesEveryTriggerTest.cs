using System.Collections.Generic;
using Jinaga.DefaultImplementations;
using Jinaga.Extensions;
using Jinaga.Store.SQLite.Test.Model.Order;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Store.SQLite.Test.Purge;

/// <summary>
/// A purge spares every trigger of every purge condition on the root's type, not only the
/// one trigger whose arrival fired it. The incremental purge reaches the store through
/// <c>PurgeDescendants</c> and the bulk purge through <c>Purge</c>, so each is stated
/// separately.
/// </summary>
public class PurgeSparesEveryTriggerTest
{
    private static JinagaClient GivenClient(Func<PurgeConditions, PurgeConditions> conditions)
    {
        string sqlitePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JinagaSQLiteTest",
            $"PurgeSparesEveryTriggerTest-{Guid.NewGuid():N}.db");

        var purgeConditions = conditions(PurgeConditions.Empty);
        var store = new SQLiteStore(sqlitePath, NullLoggerFactory.Instance);
        return new JinagaClient(store, new LocalNetwork(), purgeConditions.Validate(),
            NullLoggerFactory.Instance, new JinagaClientOptions());
    }

    [Fact]
    public async Task WhenASecondTriggerArrives_TheFirstTriggerSurvives()
    {
        var j = GivenClient(p => p
            .Purge<Order>().WhenExists<OrderCancelled>(oc => oc.order));

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
        var j = GivenClient(p => p
            .Purge<Order>().WhenExists<OrderCancelled>(oc => oc.order)
            .Purge<Order>().WhenExists<OrderShipped>(os => os.order));

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
    public async Task WhenTheBulkPurgeRuns_EveryConditionsTriggerSurvives()
    {
        var j = GivenClient(p => p
            .Purge<Order>().WhenExists<OrderCancelled>(oc => oc.order)
            .Purge<Order>().WhenExists<OrderShipped>(os => os.order));

        var order = await GivenAnOrder(j);
        var cancelled = await j.Fact(new OrderCancelled(order, DateTime.Now));
        await j.Fact(new OrderCancelledReason(cancelled, "reason"));
        var shipped = await j.Fact(new OrderShipped(order, DateTime.Now.AddMinutes(1)));

        await j.Purge();

        var cancellations = await j.Query(CancellationsHashedBy(j), order);
        cancellations.Should().ContainSingle().Which.Should().Be(j.Hash(cancelled));

        var shipments = await j.Query(ShipmentsHashedBy(j), order);
        shipments.Should().ContainSingle().Which.Should().Be(j.Hash(shipped));

        // A descendant that is neither a trigger nor the ancestor of one is still purged.
        var reasons = await j.Query(ReasonsHashedBy(j), order);
        reasons.Should().BeEmpty();
    }

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

    private static Specification<Order, string> ReasonsHashedBy(JinagaClient j) =>
        Given<Order>.Match(order =>
            order.Successors().OfType<OrderCancelledReason>(reason => reason.orderCancelled.order)
                .Select(reason => j.Hash(reason))
        );

    private static async Task<Order> GivenAnOrder(JinagaClient j)
    {
        var store = await j.Fact(new Model.Order.Store("storeId"));
        var catalog = await j.Fact(new Catalog(store, "catalogId"));
        var product = await j.Fact(new Product(catalog, "product1"));
        var item = await j.Fact(new Item(product, 1));
        return await j.Fact(new Order(store, [item]));
    }
}
