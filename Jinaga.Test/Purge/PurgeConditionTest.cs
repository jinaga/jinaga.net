using Jinaga.Test.Model.Order;
using Jinaga.Extensions;
using System.Linq;

namespace Jinaga.Test.Purge
{
    public class PurgeConditionTest
    {
        [Fact]
        public async Task WhenNoPurgeConditions_AllowsSpecification()
        {
            var jinagaClient = JinagaClient.Create(options =>
            {
            });
            var store = await jinagaClient.Fact(new Model.Order.Store("storeId"));

            var ordersInStore = Given<Model.Order.Store>.Match(store =>
                store.Successors().OfType<Order>(order => order.store)
            );
            var orders = await jinagaClient.Query(ordersInStore, store);
            orders.Should().BeEmpty();
        }

        [Fact]
        public async Task WhenSpecificationDoesNotIncludePurgeCondition_QueryThrows()
        {
            var jinagaClient = CreateJinagaClient(purgeConditions => purgeConditions
                .Purge<Order>().WhenExists<OrderCancelled>(cancelled => cancelled.order)
            );
            var store = await jinagaClient.Fact(new Model.Order.Store("storeId"));

            var ordersInStore = Given<Model.Order.Store>.Match(store =>
                store.Successors().OfType<Order>(order => order.store)
            );
            Func<Task> query = async () => await jinagaClient.Query(ordersInStore, store);
            await query.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task WhenSpecificationPassesThroughPurgeRoot_QueryThrows()
        {
            var jinagaClient = CreateJinagaClient(purgeConditions => purgeConditions
                .Purge<Order>().WhenExists<OrderCancelled>(cancelled => cancelled.order)
            );
            var store = await jinagaClient.Fact(new Model.Order.Store("storeId"));

            var shipmentsInStore = Given<Model.Order.Store>.Match(store =>
                store.Successors().OfType<OrderShipped>(shipped => shipped.order.store)
            );

            Func<Task> query = async () => await jinagaClient.Query(shipmentsInStore, store);
            await query.Should().ThrowAsync<InvalidOperationException>().WithMessage(
                """
                The match for Order.Shipped passes through types that should have purge conditions:
                !E (fact: Order) {
                    cancelled: Order.Cancelled [
                        cancelled->order: Order = fact
                    ]
                }
                """);
        }

        [Fact]
        public async Task WhenSpecificationIncludesPurgeCondition_AllowsSpecification()
        {
            var jinagaClient = CreateJinagaClient(purgeConditions => purgeConditions
                .Purge<Order>().WhenExists<OrderCancelled>(cancelled => cancelled.order)
            );
            var store = await jinagaClient.Fact(new Model.Order.Store("storeId"));

            var ordersInStore = Given<Model.Order.Store>.Match(store =>
                store.Successors().OfType<Order>(order => order.store)
                    .WhereNo((OrderCancelled cancelled) => cancelled.order)
            );

            var orders = await jinagaClient.Query(ordersInStore, store);
            orders.Should().BeEmpty();
        }

        [Fact]
        public void WhenReversiblePurgeCondition_DisallowsSpecification()
        {
            var model = new Model.Order.Store("storeId");
            Action jConstructor = () => CreateJinagaClient(purgeConditions => purgeConditions
                .WhenExists(Given<Order>.Match(order =>
                    order.Successors().OfType<OrderCancelled>(cancelled => cancelled.order)
                        .WhereNo((OrderCancelledReason reason) => reason.orderCancelled)
                ))
            );
            jConstructor.Should().Throw<InvalidOperationException>()
                .WithMessage("A specified purge condition would reverse the purge of Order with Order.Cancelled.Reason.");
        }

        [Fact]
        public async Task WhenMultiplePurgeConditions_AllowsSpecification()
        {
            var jinagaClient = CreateJinagaClient(purgeConditions => purgeConditions
                .WhenExists(Given<Order>.Match(order =>
                    order.Successors().OfType<OrderCancelled>(cancelled => cancelled.order)
                ))
                .WhenExists(Given<Order>.Match(order =>
                    order.Successors().OfType<OrderShipped>(shipped => shipped.order)
                ))
            );
            var store = await jinagaClient.Fact(new Model.Order.Store("storeId"));

            var ordersInStore = Given<Model.Order.Store>.Match(store =>
                store.Successors().OfType<Order>(order => order.store)
                    .WhereNo((OrderCancelled cancelled) => cancelled.order)
                    .WhereNo((OrderShipped shipped) => shipped.order)
            );

            var orders = await jinagaClient.Query(ordersInStore, store);
            orders.Should().BeEmpty();
        }

        [Fact]
        public async Task WhenNegativeExistentialConditions_SpecificationIsAllowed()
        {
            var jinagaClient = CreateJinagaClient(purgeConditions => purgeConditions
                .WhenExists(Given<Order>.Match(order =>
                    order.Successors().OfType<OrderCancelled>(cancelled => cancelled.order)
                ))
            );
            var store = await jinagaClient.Fact(new Model.Order.Store("storeId"));

            var ordersInStore = Given<Model.Order.Store>.Match(store =>
                store.Successors().OfType<Order>(order => order.store)
                    .WhereNo((OrderCancelled cancelled) => cancelled.order)
            );

            var orders = await jinagaClient.Query(ordersInStore, store);
            orders.Should().BeEmpty();
        }

        [Fact]
        public async Task WhenPositiveExistentialConditions_SpecificationFails()
        {
            var jinagaClient = CreateJinagaClient(purgeConditions => purgeConditions
                .WhenExists(Given<Order>.Match(order =>
                    order.Successors().OfType<OrderCancelled>(cancelled => cancelled.order)
                ))
            );
            var store = await jinagaClient.Fact(new Model.Order.Store("storeId"));

            var ordersInStore = Given<Model.Order.Store>.Match(store =>
                store.Successors().OfType<Order>(order => order.store)
                    .WhereAny((OrderCancelled cancelled) => cancelled.order)
            );

            Func<Task> query = async () => await jinagaClient.Query(ordersInStore, store);
            await query.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage(
                    """
                    The match for Order is missing purge conditions:
                    !E (order: Order) {
                        cancelled: Order.Cancelled [
                            cancelled->order: Order = order
                        ]
                    }
                    """);
        }

        [Fact]
        public async Task WhenComplexJoin_MatchesPurgeCondition()
        {
            var jinagaClient = CreateJinagaClient(purgeConditions => purgeConditions
                .Purge<Order>().WhenExists<OrderCancelled>(cancelled => cancelled.order)
            );
            var store = await jinagaClient.Fact(new Model.Order.Store("storeId"));
            var catalog = await jinagaClient.Fact(new Model.Order.Catalog(store, "catalog"));
            var productA = await jinagaClient.Fact(new Model.Order.Product(catalog, "productA"));

            var ordersInStore = Given<Model.Order.Store, Model.Order.Product>.Match((store, product, facts) =>
                store.Successors().OfType<Order>(order => order.store)
                    .WhereNo((OrderCancelled cancelled) => cancelled.order)
                    .Where(order =>
                        facts.Any<Item>(item =>
                            order.items.Contains(item) &&
                            item.product == product))
            );

            var orders = await jinagaClient.Query(ordersInStore, store, productA);
            orders.Should().BeEmpty();
        }

        private JinagaClient CreateJinagaClient(Func<PurgeConditions, PurgeConditions> purgeConditions)
        {
            return JinagaClient.Create(options =>
            {
                options.PurgeConditions = purgeConditions;
            });
        }
    }
}