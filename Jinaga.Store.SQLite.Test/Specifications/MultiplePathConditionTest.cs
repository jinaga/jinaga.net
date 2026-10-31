using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using System.Threading;

namespace Jinaga.Store.SQLite.Test.Specifications;

/// <summary>
/// A match that carries more than one path condition, read on both stores.
///
/// The SQLite store renders a specification into SQL rather than walking it, so a join it
/// builds from the wrong end of a path is invisible to the in-memory store. Every shape here
/// is read on both stores and must answer with the same rows, which is what makes the
/// comparison the assertion rather than a row count written out by hand.
///
/// Watches read inverses of exactly these shapes, so a store that drops their rows drops a
/// change a watch should have delivered.
/// </summary>
public class MultiplePathConditionTest : IDisposable
{
    private readonly string databasePath;

    public MultiplePathConditionTest()
    {
        databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JinagaSQLiteTest",
            $"MultiplePathConditionTest-{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    }

    public void Dispose()
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private const string A = "A";

    private static Role X => new Role("x", A);
    private static Role Y => new Role("y", A);

    /// <summary>
    /// A graph of facts of one type, content addressed: <c>z</c> with no predecessors,
    /// <c>p</c> holding <c>z</c> in role <c>y</c>, and <c>u</c> holding <c>z</c> in role
    /// <c>x</c>.
    /// </summary>
    private static (FactGraph graph, Fact z, Fact p, Fact u) ForkGraph()
    {
        var z = Fact.Create(A, ImmutableList<Field>.Empty, ImmutableList<Predecessor>.Empty);
        var p = Fact.Create(A, ImmutableList<Field>.Empty,
            ImmutableList.Create<Predecessor>(new PredecessorSingle("y", z.Reference)));
        var u = Fact.Create(A, ImmutableList<Field>.Empty,
            ImmutableList.Create<Predecessor>(new PredecessorSingle("x", z.Reference)));
        return (GraphOf(z, p, u), z, p, u);
    }

    /// <summary>
    /// A chain: <c>z</c>, then <c>p</c> holding it in role <c>y</c>, then <c>s</c> holding
    /// <c>p</c> in role <c>x</c>. So <c>s-&gt;x-&gt;y</c> and <c>p-&gt;y</c> are both
    /// <c>z</c>.
    /// </summary>
    private static (FactGraph graph, Fact z, Fact p, Fact s) ChainGraph()
    {
        var z = Fact.Create(A, ImmutableList<Field>.Empty, ImmutableList<Predecessor>.Empty);
        var p = Fact.Create(A, ImmutableList<Field>.Empty,
            ImmutableList.Create<Predecessor>(new PredecessorSingle("y", z.Reference)));
        var s = Fact.Create(A, ImmutableList<Field>.Empty,
            ImmutableList.Create<Predecessor>(new PredecessorSingle("x", p.Reference)));
        return (GraphOf(z, p, s), z, p, s);
    }

    private static FactGraph GraphOf(params Fact[] facts) =>
        facts.Aggregate(
            FactGraph.Empty,
            (graph, fact) => graph.Add(new FactEnvelope(fact, ImmutableList<FactSignature>.Empty)));

    private static FactReferenceTuple Tuple(params (string name, Fact fact)[] givens) =>
        givens.Aggregate(
            FactReferenceTuple.Empty,
            (tuple, given) => tuple.Add(given.name, given.fact.Reference));

    /// <summary>
    /// The rows both stores answer with, each keyed by the specification's own unknowns. The
    /// two lists are returned separately so a test asserts that they agree and what they hold,
    /// rather than trusting either one alone.
    /// </summary>
    private async Task<(ImmutableList<string> memory, ImmutableList<string> sqlite)> ReadBothStores(
        FactGraph graph, Specification specification, FactReferenceTuple givenTuple)
    {
        var labels = specification.Matches
            .Select(match => match.Unknown.Name)
            .Distinct()
            .ToImmutableList();

        IStore memoryStore = new MemoryStore();
        await memoryStore.Save(graph, false, CancellationToken.None);
        var memoryRows = Rows(
            await memoryStore.Read(givenTuple, specification, CancellationToken.None), labels);

        IStore sqliteStore = new SQLiteStore(databasePath, NullLoggerFactory.Instance);
        await sqliteStore.Save(graph, false, CancellationToken.None);
        var sqliteRows = Rows(
            await sqliteStore.Read(givenTuple, specification, CancellationToken.None), labels);

        return (memoryRows, sqliteRows);
    }

    /// <summary>
    /// A read's rows as the hashes of the facts its unknowns bind, in a fixed order and
    /// without repetition. A fact that names one predecessor twice is walked as two edges by
    /// one store and joined once by the other, and that difference is not the subject here.
    /// </summary>
    private static ImmutableList<string> Rows(ImmutableList<Product> products, ImmutableList<string> labels) =>
        products
            .Select(product => string.Join(
                ",", labels.Select(label => product.GetFactReference(label).Hash)))
            .Distinct()
            .OrderBy(row => row, StringComparer.Ordinal)
            .ToImmutableList();

    /// <summary>
    /// An unknown pinned to a given, and joined to a second given through a shared
    /// predecessor. This is the inverse jinaga-spec's <c>candidates</c> derives for its
    /// <c>random-4</c> vector, and the shape every watch on a two-sided path reads.
    /// </summary>
    [Fact]
    public async Task Read_WithAPinnedUnknownJoinedThroughAPredecessor_AgreesWithTheMemoryStore()
    {
        var (graph, _, p, u) = ForkGraph();
        var specification = new Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p1", A), ImmutableList<ExistentialCondition>.Empty),
                new SpecificationGiven(new Label("__f", A), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("u1", A),
                    ImmutableList.Create(
                        new PathCondition(ImmutableList<Role>.Empty, "__f", ImmutableList<Role>.Empty),
                        new PathCondition(ImmutableList.Create(X), "p1", ImmutableList.Create(Y))),
                    ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("u1", typeof(object)));

        var (memory, sqlite) = await ReadBothStores(
            graph, specification, Tuple(("p1", p), ("__f", u)));

        memory.Should().Equal(u.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// The same join without the pin, which the store has always answered. It holds the
    /// second path condition's own behavior fixed, so a fix to the pinned shape that broke
    /// the plain one would not pass.
    /// </summary>
    [Fact]
    public async Task Read_WithAnUnpinnedUnknownJoinedThroughAPredecessor_AgreesWithTheMemoryStore()
    {
        var (graph, _, p, u) = ForkGraph();
        var specification = new Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p1", A), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("u1", A),
                    ImmutableList.Create(
                        new PathCondition(ImmutableList.Create(X), "p1", ImmutableList.Create(Y))),
                    ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("u1", typeof(object)));

        var (memory, sqlite) = await ReadBothStores(
            graph, specification, Tuple(("p1", p)));

        memory.Should().Equal(u.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// Two path conditions on one unknown, one of them a two-role walk. Both stores have
    /// always answered this, and it is the list the duplicated case below must agree with.
    /// </summary>
    [Fact]
    public async Task Read_WithTwoPathConditionsOnOneUnknown_AgreesWithTheMemoryStore()
    {
        var (graph, z, p, s) = ChainGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph, ChainSpecification(Duplicated: false), Tuple(("p1", p), ("__f", s)));

        memory.Should().Equal($"{s.Reference.Hash},{z.Reference.Hash}");
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// The same two conditions, each listed twice and the second copy in reverse order. A
    /// condition list is a conjunction, so a condition repeated denotes what one copy of it
    /// denotes, whatever order the copies are written in.
    /// </summary>
    [Fact]
    public async Task Read_WithPathConditionsRepeatedInReverseOrder_ReadsWhatOneCopyReads()
    {
        var (graph, z, p, s) = ChainGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph, ChainSpecification(Duplicated: true), Tuple(("p1", p), ("__f", s)));

        memory.Should().Equal($"{s.Reference.Hash},{z.Reference.Hash}");
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// <c>u1</c> is both the <c>y</c> predecessor of <c>p1</c> and the <c>y</c> predecessor of
    /// the <c>x</c> predecessor of <c>__f</c>. With <paramref name="Duplicated"/> each of the
    /// two conditions is listed twice, the second pair in reverse order.
    /// </summary>
    private static Specification ChainSpecification(bool Duplicated)
    {
        var throughS = new PathCondition(
            ImmutableList<Role>.Empty, "u2", ImmutableList.Create(X, Y));
        var throughP = new PathCondition(
            ImmutableList<Role>.Empty, "p1", ImmutableList.Create(Y));
        var pathConditions = Duplicated
            ? ImmutableList.Create(throughS, throughP, throughP, throughS)
            : ImmutableList.Create(throughS, throughP);

        return new Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p1", A), ImmutableList<ExistentialCondition>.Empty),
                new SpecificationGiven(new Label("__f", A), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("u2", A),
                    ImmutableList.Create(
                        new PathCondition(ImmutableList<Role>.Empty, "__f", ImmutableList<Role>.Empty)),
                    ImmutableList<ExistentialCondition>.Empty),
                new Match(
                    new Label("u1", A),
                    pathConditions,
                    ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("u1", typeof(object)));
    }

    /// <summary>
    /// A match whose only condition is an equality with no roles on either side. It names the
    /// fact another label already binds, so it contributes no edge of its own and the store
    /// has to anchor it on the label it equals. This is jinaga-spec's <c>random-2</c>.
    /// </summary>
    [Fact]
    public async Task Read_WithAnExistentialHoldingOnlyARolelessEquality_AgreesWithTheMemoryStore()
    {
        var (graph, _, p, u) = ForkGraph();
        var specification = new Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p1", A), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("u1", A),
                    ImmutableList.Create(
                        new PathCondition(ImmutableList.Create(X), "p1", ImmutableList.Create(Y))),
                    ImmutableList.Create(
                        new ExistentialCondition(true, ImmutableList.Create(
                            new Match(
                                new Label("e1", A),
                                ImmutableList.Create(new PathCondition(
                                    ImmutableList<Role>.Empty, "u1", ImmutableList<Role>.Empty)),
                                ImmutableList<ExistentialCondition>.Empty)))))),
            new SimpleProjection("u1", typeof(object)));

        var (memory, sqlite) = await ReadBothStores(
            graph, specification, Tuple(("p1", p)));

        memory.Should().Equal(u.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// The same roleless equality under a negated condition. The fact it names is the one
    /// <c>u1</c> binds, so the condition holds of every candidate and the negation admits
    /// none. It holds the edgeless condition's truth value fixed, which a clause that merely
    /// parses would not.
    /// </summary>
    [Fact]
    public async Task Read_WithANegatedExistentialHoldingOnlyARolelessEquality_AgreesWithTheMemoryStore()
    {
        var (graph, _, p, _) = ForkGraph();
        var specification = new Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p1", A), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("u1", A),
                    ImmutableList.Create(
                        new PathCondition(ImmutableList.Create(X), "p1", ImmutableList.Create(Y))),
                    ImmutableList.Create(
                        new ExistentialCondition(false, ImmutableList.Create(
                            new Match(
                                new Label("e1", A),
                                ImmutableList.Create(new PathCondition(
                                    ImmutableList<Role>.Empty, "u1", ImmutableList<Role>.Empty)),
                                ImmutableList<ExistentialCondition>.Empty)))))),
            new SimpleProjection("u1", typeof(object)));

        var (memory, sqlite) = await ReadBothStores(
            graph, specification, Tuple(("p1", p)));

        memory.Should().BeEmpty();
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// A given the outer matches never name, used only inside a positive existential. The
    /// condition's first edge starts from that given, so the subquery has no fact the outer
    /// query has written to hang its from clause on and has to anchor itself on the given.
    /// </summary>
    [Fact]
    public async Task Read_WithAGivenNamedOnlyInsideAnExistential_AgreesWithTheMemoryStore()
    {
        var (graph, z, p, _) = ForkGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph,
            GivenInsideExistentialSpecification(Exists: true, NamedOutside: false),
            Tuple(("p1", z), ("p2", z)));

        memory.Should().Equal(p.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// The same given used only inside the negated form of that condition. The condition
    /// holds of every candidate, so the negation admits none. It pins the anchored subquery's
    /// truth value inside <c>NOT EXISTS</c>, which a subquery that merely parses would not.
    /// </summary>
    [Fact]
    public async Task Read_WithAGivenNamedOnlyInsideANegatedExistential_AgreesWithTheMemoryStore()
    {
        var (graph, z, _, _) = ForkGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph,
            GivenInsideExistentialSpecification(Exists: false, NamedOutside: false),
            Tuple(("p1", z), ("p2", z)));

        memory.Should().BeEmpty();
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// The same condition where the match also names that given in a path condition of its
    /// own, so the outer query writes it and the condition's first edge already touches a
    /// written fact. It holds the already-working case fixed.
    /// </summary>
    [Fact]
    public async Task Read_WithAGivenNamedBothOutsideAndInsideAnExistential_AgreesWithTheMemoryStore()
    {
        var (graph, z, p, _) = ForkGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph,
            GivenInsideExistentialSpecification(Exists: true, NamedOutside: true),
            Tuple(("p1", z), ("p2", z)));

        memory.Should().Equal(p.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// <c>t</c> holds the given <c>p2</c> in role <c>y</c>, and a condition on <c>t</c> names
    /// a second given <c>p1</c> as the <c>x</c> predecessor of the condition's own unknown.
    /// <paramref name="Exists"/> selects the positive or the negated form. With
    /// <paramref name="NamedOutside"/> the match names <c>p1</c> in a path condition of its
    /// own as well, which hands the condition a fact the outer query has already written.
    /// </summary>
    private static Specification GivenInsideExistentialSpecification(bool Exists, bool NamedOutside)
    {
        var throughP2 = new PathCondition(
            ImmutableList.Create(Y), "p2", ImmutableList<Role>.Empty);
        var throughP1 = new PathCondition(
            ImmutableList.Create(Y), "p1", ImmutableList<Role>.Empty);
        var pathConditions = NamedOutside
            ? ImmutableList.Create(throughP2, throughP1)
            : ImmutableList.Create(throughP2);

        return new Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p1", A), ImmutableList<ExistentialCondition>.Empty),
                new SpecificationGiven(new Label("p2", A), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("t", A),
                    pathConditions,
                    ImmutableList.Create(
                        new ExistentialCondition(Exists, ImmutableList.Create(
                            new Match(
                                new Label("op", A),
                                ImmutableList.Create(new PathCondition(
                                    ImmutableList.Create(X), "p1", ImmutableList<Role>.Empty)),
                                ImmutableList<ExistentialCondition>.Empty)))))),
            new SimpleProjection("t", typeof(object)));
    }

    private const string RootType = "Corporate.Root";
    private const string ChannelType = "Corporate.Channel";
    private const string TaskType = "Corporate.Task";
    private const string OpenedType = "Corporate.Opened";

    /// <summary>
    /// An inverse whose parts connect only through its givens: <c>given</c> reaches
    /// <c>p1</c>, the <c>op</c> inside its condition reaches <c>__f</c>, and <c>t</c> reaches
    /// <c>p2</c>. This is the inverse jinaga-spec derives for <c>given-condition-gained</c>,
    /// so a store that cannot render a disconnected component cannot serve a watch on a
    /// specification whose given carries a condition.
    /// </summary>
    [Fact]
    public async Task Read_WithComponentsConnectedOnlyThroughTheGivens_AgreesWithTheMemoryStore()
    {
        var root = Fact.Create(RootType, ImmutableList<Field>.Empty, ImmutableList<Predecessor>.Empty);
        var channel = Fact.Create(ChannelType, ImmutableList<Field>.Empty, ImmutableList<Predecessor>.Empty);
        var task = Fact.Create(TaskType, ImmutableList<Field>.Empty,
            ImmutableList.Create<Predecessor>(new PredecessorSingle("channel", channel.Reference)));
        var opened = Fact.Create(OpenedType, ImmutableList<Field>.Empty,
            ImmutableList.Create<Predecessor>(new PredecessorSingle("root", root.Reference)));
        var graph = GraphOf(root, channel, task, opened);

        var specification = new Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p1", RootType), ImmutableList<ExistentialCondition>.Empty),
                new SpecificationGiven(new Label("p2", ChannelType), ImmutableList<ExistentialCondition>.Empty),
                new SpecificationGiven(new Label("__f", OpenedType), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("given", RootType),
                    ImmutableList.Create(
                        new PathCondition(ImmutableList<Role>.Empty, "p1", ImmutableList<Role>.Empty)),
                    ImmutableList.Create(
                        new ExistentialCondition(true, ImmutableList.Create(
                            new Match(
                                new Label("op", OpenedType),
                                ImmutableList.Create(
                                    new PathCondition(
                                        ImmutableList<Role>.Empty, "__f", ImmutableList<Role>.Empty),
                                    new PathCondition(
                                        ImmutableList.Create(new Role("root", RootType)),
                                        "p1",
                                        ImmutableList<Role>.Empty)),
                                ImmutableList<ExistentialCondition>.Empty))))),
                new Match(
                    new Label("t", TaskType),
                    ImmutableList.Create(
                        new PathCondition(
                            ImmutableList.Create(new Role("channel", ChannelType)),
                            "p2",
                            ImmutableList<Role>.Empty)),
                    ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("t", typeof(object)));

        var (memory, sqlite) = await ReadBothStores(
            graph, specification, Tuple(("p1", root), ("p2", channel), ("__f", opened)));

        memory.Should().Equal($"{root.Reference.Hash},{task.Reference.Hash}");
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// A fact of type <c>A</c> distinguished by an identifier, so one graph can hold several
    /// facts that differ only in which predecessors they name.
    /// </summary>
    private static Fact Named(string identifier, params (string role, Fact predecessor)[] predecessors) =>
        Fact.Create(
            A,
            ImmutableList.Create(new Field("identifier", new FieldValueString(identifier))),
            predecessors
                .Select(predecessor => (Predecessor)new PredecessorSingle(
                    predecessor.role, predecessor.predecessor.Reference))
                .ToImmutableList());

    /// <summary>
    /// <c>p1</c> and <c>w</c> name one <c>y</c> predecessor and different <c>x</c>
    /// predecessors, and each has a sibling that names its own <c>x</c> predecessor.
    /// </summary>
    /// <remarks>
    /// A sibling satisfies a condition on <c>x</c> without being the fact a roleless equality
    /// pins, so a read that drops the equality answers with the sibling as well. The two
    /// branches are separate so that a condition on <c>y</c> admits both <c>p1</c> and
    /// <c>w</c> while a condition on <c>x</c> tells them apart.
    /// </remarks>
    private static (FactGraph graph, Fact p1, Fact pSibling, Fact w, Fact wSibling) SiblingGraph()
    {
        var shared = Named("shared");
        var pBranch = Named("pBranch");
        var wBranch = Named("wBranch");
        var p1 = Named("p1", ("y", shared), ("x", pBranch));
        var w = Named("w", ("y", shared), ("x", wBranch));
        var pSibling = Named("pSibling", ("x", pBranch));
        var wSibling = Named("wSibling", ("x", wBranch));
        return (
            GraphOf(shared, pBranch, wBranch, p1, w, pSibling, wSibling),
            p1, pSibling, w, wSibling);
    }

    /// <summary>
    /// A roleless equality listed after a condition that has already bound the unknown. The
    /// equality says the unknown is the given, so the sibling that satisfies the condition on
    /// <c>x</c> is excluded.
    /// </summary>
    [Fact]
    public async Task Read_WithARolelessEqualityAfterTheConditionThatBindsTheUnknown_AgreesWithTheMemoryStore()
    {
        var (graph, p1, _, _, _) = SiblingGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph, PinnedToTheGiven(EqualityFirst: false), Tuple(("p1", p1)));

        memory.Should().Equal(p1.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// Control: the same two conditions in the other order, which binds the unknown to the
    /// given's own fact. A condition list is a conjunction, so both orders denote one thing.
    /// </summary>
    [Fact]
    public async Task Read_WithARolelessEqualityBeforeTheConditionThatBindsTheUnknown_AgreesWithTheMemoryStore()
    {
        var (graph, p1, _, _, _) = SiblingGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph, PinnedToTheGiven(EqualityFirst: true), Tuple(("p1", p1)));

        memory.Should().Equal(p1.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// <c>u2</c> names the <c>x</c> predecessor the given names, and is the given.
    /// <paramref name="EqualityFirst"/> lists the roleless equality before the condition that
    /// would otherwise bind the unknown.
    /// </summary>
    private static Specification PinnedToTheGiven(bool EqualityFirst)
    {
        var throughX = new PathCondition(
            ImmutableList.Create(X), "p1", ImmutableList.Create(X));
        var equality = new PathCondition(
            ImmutableList<Role>.Empty, "p1", ImmutableList<Role>.Empty);
        var pathConditions = EqualityFirst
            ? ImmutableList.Create(equality, throughX)
            : ImmutableList.Create(throughX, equality);

        return new Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p1", A), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("u2", A),
                    pathConditions,
                    ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("u2", typeof(object)));
    }

    /// <summary>
    /// The same roleless equality inside a positive existential, after a condition that binds
    /// the existential's own unknown. Only <c>p1</c> has a sibling it equals, so <c>w</c>
    /// fails the condition.
    /// </summary>
    [Fact]
    public async Task Read_WithARolelessEqualityInsideAPositiveExistential_AgreesWithTheMemoryStore()
    {
        var (graph, p1, _, _, _) = SiblingGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph, PinnedInsideAnExistential(Exists: true), Tuple(("p1", p1)));

        memory.Should().Equal(p1.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// The negated form of the same existential, which pins the condition's truth value
    /// inside <c>NOT EXISTS</c>. A clause that merely parses would not move this row from
    /// <c>p1</c> to <c>w</c>.
    /// </summary>
    [Fact]
    public async Task Read_WithARolelessEqualityInsideANegatedExistential_AgreesWithTheMemoryStore()
    {
        var (graph, p1, _, w, _) = SiblingGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph, PinnedInsideAnExistential(Exists: false), Tuple(("p1", p1)));

        memory.Should().Equal(w.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// <c>w</c> names the <c>y</c> predecessor the given names, and holds a condition on a
    /// fact that names <c>w</c>'s own <c>x</c> predecessor and is the given.
    /// </summary>
    private static Specification PinnedInsideAnExistential(bool Exists)
    {
        return new Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p1", A), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("w", A),
                    ImmutableList.Create(
                        new PathCondition(ImmutableList.Create(Y), "p1", ImmutableList.Create(Y))),
                    ImmutableList.Create(
                        new ExistentialCondition(Exists, ImmutableList.Create(
                            new Match(
                                new Label("u2", A),
                                ImmutableList.Create(
                                    new PathCondition(
                                        ImmutableList.Create(X), "w", ImmutableList.Create(X)),
                                    new PathCondition(
                                        ImmutableList<Role>.Empty, "p1", ImmutableList<Role>.Empty)),
                                ImmutableList<ExistentialCondition>.Empty)))))),
            new SimpleProjection("w", typeof(object)));
    }

    /// <summary>
    /// A roleless equality inside an existential that contributes no edge at all: its unknown
    /// is bound by a roleless equality with the outer unknown, so the condition says the outer
    /// unknown is the given. Only <c>p1</c> is, so <c>w</c> fails it.
    /// </summary>
    [Fact]
    public async Task Read_WithARolelessEqualityInsideAnEdgelessPositiveExistential_AgreesWithTheMemoryStore()
    {
        var (graph, p1, _, _, _) = SiblingGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph, PinnedInsideAnEdgelessExistential(Exists: true), Tuple(("p1", p1)));

        memory.Should().Equal(p1.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// The negated form of the same edgeless existential, which pins its truth value inside
    /// <c>NOT EXISTS</c> rather than only asserting that the clause parses.
    /// </summary>
    [Fact]
    public async Task Read_WithARolelessEqualityInsideAnEdgelessNegatedExistential_AgreesWithTheMemoryStore()
    {
        var (graph, p1, _, w, _) = SiblingGraph();

        var (memory, sqlite) = await ReadBothStores(
            graph, PinnedInsideAnEdgelessExistential(Exists: false), Tuple(("p1", p1)));

        memory.Should().Equal(w.Reference.Hash);
        sqlite.Should().Equal(memory);
    }

    /// <summary>
    /// <c>w</c> names the <c>y</c> predecessor the given names, and holds a condition on a
    /// fact that is <c>w</c> and is the given. Neither of the condition's two equalities
    /// carries a role, so the condition contributes no edge of its own.
    /// </summary>
    private static Specification PinnedInsideAnEdgelessExistential(bool Exists)
    {
        return new Specification(
            ImmutableList.Create(
                new SpecificationGiven(new Label("p1", A), ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(
                new Match(
                    new Label("w", A),
                    ImmutableList.Create(
                        new PathCondition(ImmutableList.Create(Y), "p1", ImmutableList.Create(Y))),
                    ImmutableList.Create(
                        new ExistentialCondition(Exists, ImmutableList.Create(
                            new Match(
                                new Label("u2", A),
                                ImmutableList.Create(
                                    new PathCondition(
                                        ImmutableList<Role>.Empty, "w", ImmutableList<Role>.Empty),
                                    new PathCondition(
                                        ImmutableList<Role>.Empty, "p1", ImmutableList<Role>.Empty)),
                                ImmutableList<ExistentialCondition>.Empty)))))),
            new SimpleProjection("w", typeof(object)));
    }
}
