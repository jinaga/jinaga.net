using Jinaga.DefaultImplementations;
using Jinaga.Extensions;
using Jinaga.Store.SQLite.Test.Model.Order;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;

namespace Jinaga.Store.SQLite.Test.Purge;

/// <summary>
/// Purge compliance for a specification's givens, against the SQLite store.
///
/// The rule itself lives in <c>PurgeFunctions</c> and is store-independent, so the refusal
/// is pinned once against the in-memory store. What has to be exercised here is the other
/// half: that a given carrying the purge condition loses its rows through a removal the
/// caller sees. This store renders a specification into SQL and computes its inverses
/// separately, so a given's condition reaching the watch is not settled by the in-memory
/// store answering correctly.
/// </summary>
public class RealTimePurgeComplianceGivenTest
{
    private readonly JinagaClient j;

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "RealTimePurgeComplianceGivenTest.db");

    public RealTimePurgeComplianceGivenTest()
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        PurgeConditions purgeConditions = PurgeConditions.Empty
            .Purge<Order>().WhenExists<OrderCancelled>(oc => oc.order);
        var store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        var options = new JinagaClientOptions();
        j = new JinagaClient(store, new LocalNetwork(), purgeConditions.Validate(), NullLoggerFactory.Instance, options);
    }

    /// <summary>
    /// The shipments of an order nobody has cancelled. The given carries the purge
    /// condition, so the cancellation removes the row before the purge deletes anything.
    /// </summary>
    private static readonly Specification<Order, OrderShipped> ShipmentsOfALiveOrder =
        Given<Order>
            .Where((order, facts) => !facts.Any<OrderCancelled>(cancelled => cancelled.order == order))
            .Match(order => order.Successors().OfType<OrderShipped>(shipped => shipped.order));

    [Fact]
    public async Task WhenGivenCarriesTheCondition_TheTriggerRemovesTheRowBeforeThePurge()
    {
        var store = await j.Fact(new Model.Order.Store("storeId"));
        var catalog = await j.Fact(new Catalog(store, "catalogId"));
        var product = await j.Fact(new Product(catalog, "product1"));
        var item = await j.Fact(new Item(product, 1));
        var order = await j.Fact(new Order(store, [item]));
        await j.Fact(new OrderShipped(order, DateTime.Now));

        var shipments = new List<OrderShipped>();
        var observer = j.Watch(ShipmentsOfALiveOrder, order, shipped =>
        {
            shipments.Add(shipped);
            return () => { shipments.Remove(shipped); };
        });
        await observer.Loaded;

        shipments.Should().ContainSingle();
        (await j.Query(ShipmentsOfALiveOrder, order)).Should().ContainSingle();

        await j.Fact(new OrderCancelled(order, DateTime.Now));

        // The watch saw the row go, so it does not hold a shipment the purge has deleted.
        shipments.Should().BeEmpty();
        (await j.Query(ShipmentsOfALiveOrder, order)).Should().BeEmpty();

        observer.Stop();
    }
}
