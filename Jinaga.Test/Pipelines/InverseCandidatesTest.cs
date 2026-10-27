using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Storage;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Match = Jinaga.Projections.Match;

namespace Jinaga.Test.Pipelines
{
    /// <summary>
    /// What <see cref="InverseCandidates"/> is proven to do, read through a store: the
    /// candidates of a saved fact include every row the save adds or removes.
    ///
    /// Each shape here is one of jinaga-spec's named inverse vectors, with its own
    /// graph and new fact, built as <see cref="Specification"/> objects because the
    /// LINQ DSL renames the labels these shapes turn on and refuses a label reused
    /// across an existential condition.
    /// </summary>
    public class InverseCandidatesTest
    {
        /// <summary>
        /// <c>exists { notExists }</c> (<c>exists-not-exists-removes</c>): retiring an
        /// account's only current mirror loses the account.
        /// </summary>
        [Fact]
        public async Task Candidates_ExistsNotExists()
        {
            var root = NewFact("Root");
            var account = NewFact("Account", ("root", root));
            var mirror = NewFact("Mirror", ("account", account));
            var retired = NewFact("Retired", ("mirror", mirror));

            var specification = new Specification(
                Givens(Given("p1", "Root")),
                ImmutableList.Create(
                    Unknown("u1", "Account",
                        Paths(Successor("root", "Root", "p1")),
                        Conditions(Exists(
                            Unknown("e1", "Mirror",
                                Paths(Successor("account", "Account", "u1")),
                                Conditions(NotExists(
                                    Unknown("e2", "Retired",
                                        Paths(Successor("mirror", "Mirror", "e1")),
                                        Conditions())))))))),
                Row("u1"));

            await Change(specification, Tuple(("p1", root)), Labels("u1"),
                ImmutableList.Create(root, account, mirror), retired,
                before: ImmutableList.Create(Key(account)),
                after: ImmutableList<string>.Empty);
        }

        /// <summary>
        /// Two sibling <c>notExists</c> that one fact falsifies together
        /// (<c>sibling-not-exists</c>).
        /// </summary>
        [Fact]
        public async Task Candidates_SiblingNotExists()
        {
            var root = NewFact("Root");
            var task = NewFact("Task", ("root", root));
            var done = NewFact("Done", ("task", task), ("other", task));

            var specification = new Specification(
                Givens(Given("p1", "Root")),
                ImmutableList.Create(
                    Unknown("u1", "Task",
                        Paths(Successor("root", "Root", "p1")),
                        Conditions(
                            NotExists(Unknown("e1", "Done",
                                Paths(Successor("task", "Task", "u1")),
                                Conditions())),
                            NotExists(Unknown("e2", "Done",
                                Paths(Successor("other", "Task", "u1")),
                                Conditions()))))),
                Row("u1"));

            await Change(specification, Tuple(("p1", root)), Labels("u1"),
                ImmutableList.Create(root, task), done,
                before: ImmutableList.Create(Key(task)),
                after: ImmutableList<string>.Empty);
        }

        /// <summary>
        /// <c>notExists { notExists { exists } }</c> (<c>depth-three-gain</c>): the new
        /// fact makes the innermost <c>exists</c> true, which removes the middle
        /// solution, which adds the row.
        /// </summary>
        [Fact]
        public async Task Candidates_DepthThree()
        {
            var root = NewFact("Root");
            var a = NewFact("A", ("root", root));
            var b = NewFact("B", ("a", a));
            var c = NewFact("C", ("b", b));
            var d = NewFact("D", ("c", c));

            var specification = new Specification(
                Givens(Given("p1", "Root")),
                ImmutableList.Create(
                    Unknown("a", "A",
                        Paths(Successor("root", "Root", "p1")),
                        Conditions(NotExists(
                            Unknown("b", "B",
                                Paths(Successor("a", "A", "a")),
                                Conditions(NotExists(
                                    Unknown("c", "C",
                                        Paths(Successor("b", "B", "b")),
                                        Conditions(Exists(
                                            Unknown("d", "D",
                                                Paths(Successor("c", "C", "c")),
                                                Conditions()))))))))))),
                Row("a"));

            await Change(specification, Tuple(("p1", root)), Labels("a"),
                ImmutableList.Create(root, a, b, c), d,
                before: ImmutableList<string>.Empty,
                after: ImmutableList.Create(Key(a)));
        }

        /// <summary>
        /// A label reused, after an existential condition, by a later match
        /// (<c>reused-label</c>).
        /// </summary>
        [Fact]
        public async Task Candidates_ReusedLabel()
        {
            var root = NewFact("Root");
            var task = NewFact("Task", ("root", root));
            var note = NewFact("Note", ("root", root));
            var done = NewFact("Done", ("task", task));

            var specification = new Specification(
                Givens(Given("r", "Root")),
                ImmutableList.Create(
                    Unknown("t", "Task",
                        Paths(Successor("root", "Root", "r")),
                        Conditions(NotExists(
                            Unknown("x", "Done",
                                Paths(Successor("task", "Task", "t")),
                                Conditions())))),
                    Unknown("x", "Note",
                        Paths(Successor("root", "Root", "r")),
                        Conditions())),
                new CompoundProjection(
                    ImmutableDictionary<string, Projection>.Empty
                        .Add("t", new SimpleProjection("t", typeof(object)))
                        .Add("x", new SimpleProjection("x", typeof(object))),
                    typeof(object)));

            await Change(specification, Tuple(("r", root)), Labels("t", "x"),
                ImmutableList.Create(root, task, note), done,
                before: ImmutableList.Create(Key(task, note)),
                after: ImmutableList<string>.Empty);
        }

        /// <summary>
        /// A <c>notExists</c> correlated over two predecessors: messages in a channel
        /// that this user has not read (<c>correlated-not-exists</c>). Its given is
        /// joined only inside the existential condition.
        /// </summary>
        [Fact]
        public async Task Candidates_CorrelatedNotExists()
        {
            var user = NewFact("User");
            var channel = NewFact("Channel");
            var message = NewFact("Message", ("channel", channel));
            var read = NewFact("Read", ("message", message), ("user", user));

            var specification = new Specification(
                Givens(Given("p1", "User"), Given("p2", "Channel")),
                ImmutableList.Create(
                    Unknown("m", "Message",
                        Paths(Successor("channel", "Channel", "p2")),
                        Conditions(NotExists(
                            Unknown("rd", "Read",
                                Paths(
                                    Successor("message", "Message", "m"),
                                    Successor("user", "User", "p1")),
                                Conditions()))))),
                Row("m"));

            await Change(specification, Tuple(("p1", user), ("p2", channel)), Labels("m"),
                ImmutableList.Create(user, channel, message), read,
                before: ImmutableList.Create(Key(message)),
                after: ImmutableList<string>.Empty);
        }

        /// <summary>
        /// A condition on the given itself. <see cref="InverseCandidates.CandidateInverses"/>
        /// moves it onto a match of its own, so the fact that falsifies it still produces
        /// a candidate for the row that the given carried.
        /// </summary>
        [Fact]
        public async Task Candidates_ConditionOnTheGiven()
        {
            var root = NewFact("Root");
            var item = NewFact("Item", ("root", root));
            var revoked = NewFact("Revoked", ("root", root));

            var specification = new Specification(
                Givens(new SpecificationGiven(
                    new Label("p1", "Root"),
                    Conditions(NotExists(
                        Unknown("v1", "Revoked",
                            Paths(Successor("root", "Root", "p1")),
                            Conditions()))))),
                ImmutableList.Create(
                    Unknown("u1", "Item",
                        Paths(Successor("root", "Root", "p1")),
                        Conditions())),
                Row("u1"));

            await Change(specification, Tuple(("p1", root)), Labels("u1"),
                ImmutableList.Create(root, item), revoked,
                before: ImmutableList.Create(Key(item)),
                after: ImmutableList<string>.Empty);
        }

        /// <summary>
        /// Read the specification before and after saving <paramref name="fact"/>, check
        /// that the rows moved as expected, and check that every row gained or lost is
        /// among the candidates of that fact.
        /// </summary>
        private static async Task Change(
            Specification specification,
            FactReferenceTuple givens,
            ImmutableList<string> rowLabels,
            ImmutableList<Fact> graph,
            Fact fact,
            ImmutableList<string> before,
            ImmutableList<string> after)
        {
            var store = new MemoryStore();
            await store.Save(Graph(graph), false, CancellationToken.None);
            var rowsBefore = await Rows(store, givens, specification, rowLabels);
            rowsBefore.Should().BeEquivalentTo(before);

            await store.Save(Graph(graph.Add(fact)), false, CancellationToken.None);
            var rowsAfter = await Rows(store, givens, specification, rowLabels);
            rowsAfter.Should().BeEquivalentTo(after);

            var changed = rowsBefore.SymmetricExcept(rowsAfter);
            changed.Should().NotBeEmpty("the save has to change a row for the candidates to cover anything");

            var candidates = await Candidates(store, givens, specification, rowLabels, fact);
            changed.Should().BeSubsetOf(candidates);
        }

        private static async Task<ImmutableHashSet<string>> Rows(
            MemoryStore store,
            FactReferenceTuple givens,
            Specification specification,
            ImmutableList<string> rowLabels)
        {
            var products = await store.Read(givens, specification, CancellationToken.None);
            return products
                .Select(product => string.Join("|", rowLabels
                    .Select(label => product.GetFactReference(label).Hash)))
                .ToImmutableHashSet();
        }

        /// <summary>
        /// Every row of every candidate inverse whose pin takes a fact of this type, read
        /// with the subscription's givens and the new fact as <c>__f</c>.
        /// </summary>
        private static async Task<ImmutableHashSet<string>> Candidates(
            MemoryStore store,
            FactReferenceTuple givens,
            Specification specification,
            ImmutableList<string> rowLabels,
            Fact fact)
        {
            var rows = ImmutableHashSet<string>.Empty;
            foreach (var inverse in InverseCandidates.CandidateInverses(specification))
            {
                var pin = inverse.Givens.Single(given => given.Label.Name == InverseCandidates.PinLabel);
                if (pin.Label.Type != fact.Reference.Type)
                {
                    continue;
                }
                rows = rows.Union(await Rows(
                    store,
                    givens.Add(InverseCandidates.PinLabel, fact.Reference),
                    inverse,
                    rowLabels));
            }
            return rows;
        }

        private static Fact NewFact(string type, params (string role, Fact predecessor)[] predecessors) =>
            Fact.Create(
                type,
                ImmutableList<Field>.Empty,
                predecessors
                    .Select(pair => (Predecessor)new PredecessorSingle(pair.role, pair.predecessor.Reference))
                    .ToImmutableList());

        private static FactGraph Graph(ImmutableList<Fact> facts)
        {
            var builder = new FactGraphBuilder();
            foreach (var fact in facts)
            {
                builder.Add(new FactEnvelope(fact, ImmutableList<FactSignature>.Empty));
            }
            return builder.Build();
        }

        private static FactReferenceTuple Tuple(params (string name, Fact fact)[] givens) =>
            givens.Aggregate(
                FactReferenceTuple.Empty,
                (tuple, pair) => tuple.Add(pair.name, pair.fact.Reference));

        private static string Key(params Fact[] facts) =>
            string.Join("|", facts.Select(fact => fact.Reference.Hash));

        private static ImmutableList<string> Labels(params string[] labels) => labels.ToImmutableList();

        private static ImmutableList<SpecificationGiven> Givens(params SpecificationGiven[] givens) =>
            givens.ToImmutableList();

        private static SpecificationGiven Given(string name, string type) =>
            new SpecificationGiven(new Label(name, type), ImmutableList<ExistentialCondition>.Empty);

        private static Projection Row(string label) => new SimpleProjection(label, typeof(object));

        private static Match Unknown(
            string name,
            string type,
            ImmutableList<PathCondition> paths,
            ImmutableList<ExistentialCondition> conditions) =>
            new Match(new Label(name, type), paths, conditions);

        private static ImmutableList<PathCondition> Paths(params PathCondition[] paths) =>
            paths.ToImmutableList();

        private static ImmutableList<ExistentialCondition> Conditions(params ExistentialCondition[] conditions) =>
            conditions.ToImmutableList();

        /// <summary><c>unknown-&gt;role: targetType = labelRight</c>.</summary>
        private static PathCondition Successor(string role, string targetType, string labelRight) =>
            new PathCondition(
                ImmutableList.Create(new Role(role, targetType)),
                labelRight,
                ImmutableList<Role>.Empty);

        private static ExistentialCondition Exists(params Match[] matches) =>
            new ExistentialCondition(true, matches.ToImmutableList());

        private static ExistentialCondition NotExists(params Match[] matches) =>
            new ExistentialCondition(false, matches.ToImmutableList());
    }
}
