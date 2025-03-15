using Jinaga.Test.Model.Order;

namespace Jinaga.Test.Purge;

public class PurgeConditionsTest
{
    [Fact]
    public void PurgeConditions_CanDescribeEmpty()
    {
        var description = PurgeConditions.Describe(pc => pc);
        string expected =
            """
            purge {
            }

            """;
        description.Should().Be(expected);
    }

    [Fact]
    public void PurgeConditions_CanDescribeSingleCondition()
    {
        var description = PurgeConditions.Describe(pc => pc
            .Purge<Order>().WhenExists<OrderCancelled>(cancelled => cancelled.order)
        );
        string expected =
            """
            purge {
                (fact: Order) {
                    cancelled: Order.Cancelled [
                        cancelled->order: Order = fact
                    ]
                }
            }

            """;
        description.ReplaceLineEndings().Should().Be(expected);
    }

    [Fact]
    public void PurgeConditions_CanDescribeMultipleConditions()
    {
        var description = PurgeConditions.Describe(pc => pc
            .Purge<Order>().WhenExists<OrderCancelled>(cancelled => cancelled.order)
            .Purge<Order>().WhenExists<OrderShipped>(shipped => shipped.order)
        );
        string expected =
            """
            purge {
                (fact: Order) {
                    cancelled: Order.Cancelled [
                        cancelled->order: Order = fact
                    ]
                }
                (fact: Order) {
                    shipped: Order.Shipped [
                        shipped->order: Order = fact
                    ]
                }
            }

            """;
        description.ReplaceLineEndings().Should().Be(expected);
    }
}