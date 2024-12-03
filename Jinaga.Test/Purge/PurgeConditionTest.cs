using Jinaga.Test.Model.Order;
using Jinaga.Extensions;

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

        private JinagaClient CreateJinagaClient(Func<PurgeConditions, PurgeConditions> purgeConditions)
        {
            return JinagaClient.Create(options =>
            {
                options.PurgeConditions = purgeConditions;
            });
        }
    }
}