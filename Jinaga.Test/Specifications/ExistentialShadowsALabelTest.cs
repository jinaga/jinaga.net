using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Storage;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Jinaga.Test.Specifications;

/// <summary>
/// A match inside an existential condition may declare a label that the enclosing
/// specification has already bound. The parser and <c>WellFormedSpecification</c> refuse
/// that shape from a user, but the inverses this library computes for itself have it, so
/// the evaluators have to read it. The inner declaration is a new variable that shadows
/// the outer one within the condition.
/// </summary>
public class ExistentialShadowsALabelTest
{
    /// <summary>
    /// <code>
    /// (p1: A) {
    ///     e: A [
    ///         e->x: A = p1
    ///         !E {
    ///             e: A [
    ///                 e = p1->y: A
    ///             ]
    ///         }
    ///     ]
    /// } => e
    /// </code>
    /// Built from <see cref="Specification"/> objects, because the C# query DSL renames a
    /// reused label and so cannot express the shape under test.
    /// </summary>
    private static Specification ShadowingSpecification => new Specification(
        ImmutableList.Create(
            new SpecificationGiven(
                new Label("p1", "A"),
                ImmutableList<ExistentialCondition>.Empty)),
        ImmutableList.Create(
            new Match(
                new Label("e", "A"),
                ImmutableList.Create(
                    new PathCondition(
                        ImmutableList.Create(new Role("x", "A")),
                        "p1",
                        ImmutableList<Role>.Empty)),
                ImmutableList.Create(
                    new ExistentialCondition(false, ImmutableList.Create(
                        new Match(
                            new Label("e", "A"),
                            ImmutableList.Create(
                                new PathCondition(
                                    ImmutableList<Role>.Empty,
                                    "p1",
                                    ImmutableList.Create(new Role("y", "A")))),
                            ImmutableList<ExistentialCondition>.Empty)))))),
        new SimpleProjection("e", typeof(object)));

    [Fact]
    public async Task MemoryStore_InnerDeclarationRangesOverItsOwnPath()
    {
        var b = FactA("b");
        var p1 = FactA("p1", new PredecessorSingle("y", b.Reference));
        var a = FactA("a", new PredecessorSingle("x", p1.Reference));

        var products = await WhenRead(ShadowingSpecification, p1, b, p1, a);

        // The inner e ranges over p1->y, which is b. It exists, so the negation fails.
        products.Should().BeEmpty();
    }

    [Fact]
    public async Task MemoryStore_InnerDeclarationIsNotBoundToTheOuterValue()
    {
        var p1 = FactA("p1");
        var a = FactA("a", new PredecessorSingle("x", p1.Reference));

        var products = await WhenRead(ShadowingSpecification, p1, p1, a);

        // p1 has no y predecessor, so the inner e ranges over nothing and the negation
        // holds. Were the inner e bound to the outer value instead, it would range over
        // a, and the negation would fail.
        products.Should().ContainSingle()
            .Which.GetElement("e").Should().Be(new SimpleElement(a.Reference));
    }

    [Fact]
    public void GraphEvaluator_IsNotAskedToReadTheShape()
    {
        // The graph evaluator walks predecessors only, and its input cannot hold a
        // successor step or an existential condition at all. CanRunOnGraph is the gate
        // that sends this shape to the store, and
        // WellFormedSpecification.SplitBeforeFirstSuccessor keeps it out of the head an
        // authorization rule runs on the graph. This pins the gate shut: opening it
        // means teaching the evaluator both successor steps and existential scope.
        ShadowingSpecification.CanRunOnGraph.Should().BeFalse();
    }

    private static Fact FactA(string identifier, params Predecessor[] predecessors) =>
        Fact.Create(
            "A",
            ImmutableList.Create(new Field("identifier", new FieldValueString(identifier))),
            predecessors.ToImmutableList());

    private static async Task<ImmutableList<Product>> WhenRead(
        Specification specification, Fact given, params Fact[] facts)
    {
        var builder = new FactGraphBuilder();
        foreach (var fact in facts)
        {
            builder.Add(new FactEnvelope(fact, ImmutableList<FactSignature>.Empty));
        }
        var store = new MemoryStore();
        await store.Save(builder.Build(), false, CancellationToken.None);
        return await store.Read(
            FactReferenceTuple.Empty.Add(
                specification.Givens.Single().Label.Name,
                given.Reference),
            specification,
            CancellationToken.None);
    }
}
