using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// The SQLite store's answer to a specification whose existential condition redeclares a
/// label the enclosing specification has already bound. <c>ResultDescriptionBuilder</c>
/// drops the outer binding before it builds the condition, so the inner declaration
/// already has its own scope here. These assertions hold that behavior, which the
/// in-memory store now matches.
/// </summary>
public class ExistentialShadowsALabelTest
{
    private readonly IStore store;

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "ExistentialShadowsALabelTest.db");

    public ExistentialShadowsALabelTest()
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
    }

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
    public async Task InnerDeclarationRangesOverItsOwnPath()
    {
        var b = FactA("b");
        var p1 = FactA("p1", new PredecessorSingle("y", b.Reference));
        var a = FactA("a", new PredecessorSingle("x", p1.Reference));

        var products = await WhenRead(ShadowingSpecification, p1, b, p1, a);

        // The inner e ranges over p1->y, which is b. It exists, so the negation fails.
        products.Should().BeEmpty();
    }

    [Fact]
    public async Task InnerDeclarationIsNotBoundToTheOuterValue()
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

    private static Fact FactA(string identifier, params Predecessor[] predecessors) =>
        Fact.Create(
            "A",
            ImmutableList.Create(new Field("identifier", new FieldValueString(identifier))),
            predecessors.ToImmutableList());

    private async Task<ImmutableList<Product>> WhenRead(
        Specification specification, Fact given, params Fact[] facts)
    {
        var builder = new FactGraphBuilder();
        foreach (var fact in facts)
        {
            builder.Add(new FactEnvelope(fact, ImmutableList<FactSignature>.Empty));
        }
        await store.Save(builder.Build(), false, default);
        return await store.Read(
            FactReferenceTuple.Empty.Add(
                specification.Givens.Single().Label.Name,
                given.Reference),
            specification,
            default);
    }
}
