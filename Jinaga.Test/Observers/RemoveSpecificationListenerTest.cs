using Jinaga.Facts;
using Jinaga.Observers;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Storage;
using Jinaga.Test.Model;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Jinaga.Test.Observers;

/// <summary>
/// Removing a specification listener. Listeners on structurally equal
/// specifications share one entry, so removing one of them must leave the
/// entry holding only the others.
/// </summary>
public class RemoveSpecificationListenerTest
{
    [Fact]
    public async Task RemovedListener_SharingASpecification_IsNotNotified()
    {
        var source = new ObservableSource(new MemoryStore());
        int removedCalls = 0;
        int keptCalls = 0;
        var removed = source.AddSpecificationListener(AirlineItself(), (products, cancellationToken) =>
        {
            Interlocked.Increment(ref removedCalls);
            return Task.CompletedTask;
        });
        source.AddSpecificationListener(AirlineItself(), (products, cancellationToken) =>
        {
            Interlocked.Increment(ref keptCalls);
            return Task.CompletedTask;
        });

        source.RemoveSpecificationListener(removed);

        var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
        var airline = collector.Serialize(new Airline("value"));
        var graph = collector.Graph;
        await source.Notify(graph, ImmutableList.Create(graph.GetFact(airline)), default);

        removedCalls.Should().Be(0);
        keptCalls.Should().Be(1);
    }

    /// <summary>
    /// A specification whose one unknown is its given. A new instance each
    /// time, so the two listeners share an entry by structure, not identity.
    /// </summary>
    private static Projections.Specification AirlineItself() =>
        new Projections.Specification(
            ImmutableList.Create(
                new SpecificationGiven(
                    new Label("p", "Skylane.Airline"),
                    ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("f", "Skylane.Airline"),
                    ImmutableList.Create(
                        new PathCondition(ImmutableList<Role>.Empty, "p", ImmutableList<Role>.Empty)),
                    ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("f", typeof(Airline)));
}
