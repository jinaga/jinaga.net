using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Jinaga.Projections
{
    /// <summary>
    /// Why a specification has no feeds a store can answer, or nothing when it has.
    ///
    /// A feed is one query a replicator answers from its givens. If its labels fall into two
    /// chains that no path condition joins, the store can only answer it as a cross product of
    /// the two, which jinaga-server cannot do (jinaga/jinaga-server#233). <c>buildFeeds</c>
    /// keeps each descendant's matches in written order, so a feed cut at a negative condition
    /// or at a child specification can be such a product even when the whole specification is
    /// connected.
    ///
    /// Ported from <c>isConnectedDeep</c> in jinaga-spec
    /// (<c>JinagaSpec/Connected.lean</c>), following the reference port
    /// <c>ports/typescript/well-formed.ts</c>, at jinaga-spec
    /// <c>79d406a3be968f5f7fa0331932542c7a43bfb973</c>, the commit
    /// <c>Jinaga.Test/Conformance/Vectors</c> is pinned at. Each method below names the Lean
    /// function it corresponds to. <c>ConnectedDescendants</c> is the hypothesis the feeds
    /// theorems take (jinaga/jinaga-spec#57, which addresses jinaga/jinaga-spec#55), and
    /// <c>isConnectedDeep_iff</c> proves this check decides it.
    ///
    /// The two clauses are
    /// <list type="number">
    /// <item>every descendant, as a feed, is connected over the givens it uses; and</item>
    /// <item>every feed is joined -- its path conditions alone join its matches, so a negative
    /// condition never counts as the join.</item>
    /// </list>
    ///
    /// A given no descendant uses is allowed (jinaga/jinaga-spec#60): no feed takes it, and no
    /// match reads it. An earlier clause refused it (jinaga/jinaga-spec#45), and
    /// <see cref="IsConnected"/> counts only the givens the matches use, which is what makes
    /// <c>unused-given</c> and a specification with no matches at all connected.
    ///
    /// <see cref="Disconnect"/> is the one implementation of both clauses: it reports the first
    /// failure it finds, and <see cref="IsConnectedDeep"/> is the question of whether it found
    /// one. Clause 1 is checked at the specification itself, then at each child specification
    /// outermost first, then clause 3 over the feeds in <see cref="FeedBuilder.FeedsOf"/>
    /// order, so the advice names the shallowest level that is already a product.
    /// </summary>
    internal static class ConnectedSpecification
    {
        /// <summary>
        /// Whether every descendant and every feed of the specification is connected
        /// (<c>isConnectedDeep</c>).
        /// </summary>
        public static bool IsConnectedDeep(Specification specification) =>
            Disconnect(specification) == null;

        /// <summary>
        /// The first disconnection of the specification, or null when it has none.
        /// </summary>
        public static Disconnection? Disconnect(Specification specification)
        {
            var descendants = Descendants(specification);

            // Clause 1 at the specification itself.
            var top = Chains(AsFeed(specification.Givens, specification.Matches));
            if (top.Count > 1)
            {
                return Disconnection.SeparateChains(top);
            }

            // Clause 1 at a child specification, outermost first, so the advice names the
            // shallowest level that is already a matrix.
            foreach (var descendant in descendants.Where(descendant => descendant.Path != null))
            {
                var chains = Chains(AsFeed(descendant.Specification.Givens, descendant.Specification.Matches));
                if (chains.Count > 1)
                {
                    return ChildJoinsLate(descendants, descendant, chains);
                }
            }

            // Clause 2: every feed `buildFeeds` emits is joined. The feeds come from the
            // unchecked FeedsOf, in its order, because the refusal this serves runs before
            // BuildFeeds returns any.
            foreach (var feed in FeedBuilder.FeedsOf(specification))
            {
                if (!IsJoined(feed))
                {
                    return FeedDisconnection(descendants, feed);
                }
            }
            return null;
        }

        /* ------------------------------ The ported checks ------------------------------ */

        /// <summary>
        /// Whether every label of a specification is joined to every other, through path
        /// conditions or through the labels an existential condition names
        /// (<c>isConnected</c>, the check <c>invertSpecification</c> runs). It reads only the
        /// top-level matches and the givens those matches use, so a given nothing uses is not
        /// a chain of its own (jinaga/jinaga-spec#60).
        /// </summary>
        public static bool IsConnected(Specification specification) => Chains(specification).Count <= 1;

        /// <summary>
        /// Whether a feed is joined (<c>isJoined</c>): its path conditions alone, its negative
        /// conditions' filters dropped, join its matches and the givens it uses, so it is not a
        /// product; and it passes <see cref="IsConnected"/>, so each filter's matches are
        /// joined to it.
        /// </summary>
        public static bool IsJoined(Specification feed) =>
            IsConnected(feed) && IsConnected(PathsOnly(feed));

        // asFeed: matches as a feed of `givens` -- with the givens they use, and no projection.
        private static Specification AsFeed(
            ImmutableList<SpecificationGiven> givens, ImmutableList<Match> matches) =>
            new Specification(FeedBuilder.UsedGivens(givens, matches), matches, CompoundProjection.Empty);

        // The feed with every filter dropped, over the givens its path conditions then use: a
        // given only a filter names is a parameter of the filter, not a factor of a product.
        private static Specification PathsOnly(Specification feed) =>
            AsFeed(feed.Givens, feed.Matches.Select(Paths).ToImmutableList());

        // Match.paths: a match with its existential conditions dropped.
        private static Match Paths(Match match) =>
            new Match(match.Unknown, match.PathConditions, ImmutableList<ExistentialCondition>.Empty);

        // referenced: every label the matches name in a path condition, at any depth.
        private static ImmutableList<string> Referenced(ImmutableList<Match> matches) =>
            matches
                .SelectMany(match => match.PathConditions
                    .Select(condition => condition.LabelRight)
                    .Concat(match.ExistentialConditions
                        .SelectMany(condition => Referenced(condition.Matches))))
                .ToImmutableList();

        // connections: every label the matches name, and every pair the connectivity check
        // joins. A path condition joins its match's unknown to the label it names. An
        // existential condition joins its match's unknown to every label its path conditions
        // name, at any depth.
        //
        // The reference port walks one list of conditions; a Match here keeps path and
        // existential conditions apart, so this walks the paths and then the existentials.
        // Only the order the labels are first named differs, which is the order a chain's
        // labels are reported in.
        private static (ImmutableList<string> Labels, ImmutableList<(string Left, string Right)> Edges)
            Connections(ImmutableList<Match> matches)
        {
            var labels = ImmutableList.CreateBuilder<string>();
            var edges = ImmutableList.CreateBuilder<(string Left, string Right)>();
            foreach (var match in matches)
            {
                var unknown = match.Unknown.Name;
                labels.Add(unknown);
                foreach (var condition in match.PathConditions)
                {
                    labels.Add(condition.LabelRight);
                    edges.Add((unknown, condition.LabelRight));
                }
                foreach (var condition in match.ExistentialConditions)
                {
                    var inner = Connections(condition.Matches);
                    var names = Referenced(condition.Matches);
                    labels.AddRange(inner.Labels);
                    labels.AddRange(names);
                    edges.AddRange(inner.Edges);
                    edges.AddRange(names.Select(name => (unknown, name)));
                }
            }
            return (labels.ToImmutable(), edges.ToImmutable());
        }

        /* --------------------------------- The chains --------------------------------- */

        // The connected components of a specification's join graph, each in the order its
        // labels were first named, the components in the order they were first reached. Only
        // the givens the matches use are labels, as for a feed (`usedGivens`), so a given
        // nothing uses is no component of its own. A specification with no labels at all has
        // none, and so is connected.
        private static ImmutableList<DisconnectedChain> Chains(Specification specification)
        {
            var givens = FeedBuilder
                .UsedGivens(specification.Givens, specification.Matches)
                .Select(given => given.Label.Name)
                .ToImmutableList();
            var connections = Connections(specification.Matches);
            var labels = givens.AddRange(connections.Labels).Distinct().ToImmutableList();

            var parent = labels.ToDictionary(label => label, label => label);
            string Root(string label)
            {
                while (parent[label] != label)
                {
                    label = parent[label];
                }
                return label;
            }
            foreach (var edge in connections.Edges)
            {
                if (!parent.ContainsKey(edge.Left) || !parent.ContainsKey(edge.Right))
                {
                    continue;
                }
                var left = Root(edge.Left);
                var right = Root(edge.Right);
                if (left != right)
                {
                    parent[left] = right;
                }
            }

            var order = new List<string>();
            var members = new Dictionary<string, ImmutableList<string>.Builder>();
            foreach (var label in labels)
            {
                var root = Root(label);
                if (!members.TryGetValue(root, out var builder))
                {
                    builder = ImmutableList.CreateBuilder<string>();
                    members.Add(root, builder);
                    order.Add(root);
                }
                builder.Add(label);
            }
            return order
                .Select(root => members[root].ToImmutable())
                .Select(group => new DisconnectedChain(
                    group.Where(label => !givens.Contains(label)).ToImmutableList(),
                    givens.Where(group.Contains).ToImmutableList()))
                .ToImmutableList();
        }

        /* ------------------------------- The descendants ------------------------------- */

        /// <summary>
        /// A child specification of a projection, at any depth, as a specification of its own:
        /// the same givens, and the parent's matches followed by its own
        /// (<c>Specification.descendants</c>).
        /// </summary>
        private sealed class Descendant
        {
            public Descendant(string? path, ImmutableList<Match> inherited, Specification specification)
            {
                Path = path;
                Inherited = inherited;
                Specification = specification;
            }

            /// <summary>The component path that names this child, or null at the specification itself.</summary>
            public string? Path { get; }

            /// <summary>The matches this descendant inherits from its parents.</summary>
            public ImmutableList<Match> Inherited { get; }

            public Specification Specification { get; }

            /// <summary>The matches this descendant declares itself.</summary>
            public ImmutableList<Match> Own =>
                Specification.Matches.GetRange(Inherited.Count, Specification.Matches.Count - Inherited.Count);
        }

        private static ImmutableList<Descendant> Descendants(Specification specification) =>
            Descendants(null, ImmutableList<Match>.Empty, specification);

        private static ImmutableList<Descendant> Descendants(
            string? path, ImmutableList<Match> inherited, Specification node) =>
            ImmutableList.Create(new Descendant(path, inherited, node))
                .AddRange(NamedChildren(node.Projection).SelectMany(child => Descendants(
                    path == null ? child.Name : $"{path}.{child.Name}",
                    node.Matches,
                    new Specification(
                        node.Givens,
                        node.Matches.AddRange(child.Child.Matches),
                        child.Child.Projection))));

        // The child specifications a projection holds, each with the component name a developer
        // reads it by. A CompoundProjection may hold a compound, whose children belong to the
        // same level; those are named by their path through it. A collection that is the
        // projection itself is a component of nothing, so it is named by the first label it
        // declares.
        //
        // The order is the order the type gives, which is also the order
        // <see cref="FeedBuilder"/> walks the children in and so the order of the feeds.
        private static IEnumerable<(string Name, CollectionProjection Child)> NamedChildren(Projection projection)
        {
            switch (projection)
            {
                case CollectionProjection collection:
                    return new[] { (NameOf(collection), collection) };
                case CompoundProjection compound:
                    return compound.Names.SelectMany(name => NamedChildren(compound.GetProjection(name))
                        .Select(child => (
                            compound.GetProjection(name) is CompoundProjection ? $"{name}.{child.Name}" : name,
                            child.Child)));
                default:
                    return Enumerable.Empty<(string, CollectionProjection)>();
            }
        }

        private static string NameOf(CollectionProjection collection) =>
            collection.Matches.IsEmpty ? "(unnamed)" : collection.Matches[0].Unknown.Name;

        /* ------------------------------- Which shape it is ------------------------------- */

        // Clause 1 at a child specification. Where a deeper descendant does join the chains,
        // the shape is a matrix the developer wrote in three levels instead of two, and the
        // disconnection carries the match that joins them and the child that declares it.
        // Where none does, the two chains are genuinely independent and it carries neither.
        private static Disconnection ChildJoinsLate(
            ImmutableList<Descendant> descendants, Descendant child, ImmutableList<DisconnectedChain> chains)
        {
            var own = child.Own.Select(match => match.Unknown.Name).ToImmutableList();
            var ownChain = chains.FirstOrDefault(chain => own.Any(chain.Holds)) ?? chains[0];
            var parentChain = chains.FirstOrDefault(chain =>
                chain != ownChain && child.Inherited.Any(match => chain.Holds(match.Unknown.Name)))
                ?? chains.First(chain => chain != ownChain);

            var joiner = Joiner(descendants, child, ownChain, parentChain);
            return Disconnection.ChildJoinsLate(
                child.Path!, ownChain, parentChain, joiner?.Path, joiner?.Match);
        }

        // The first match of a deeper descendant that joins the two chains, and the path of the
        // descendant that declares it. A deeper descendant that is itself disconnected joins
        // nothing, so it is passed over.
        private static (string Path, Match Match)? Joiner(
            ImmutableList<Descendant> descendants, Descendant child,
            DisconnectedChain own, DisconnectedChain parent)
        {
            var prefix = child.Path + ".";
            foreach (var deeper in descendants.Where(descendant =>
                descendant.Path != null && descendant.Path.StartsWith(prefix, StringComparison.Ordinal)))
            {
                if (Chains(AsFeed(deeper.Specification.Givens, deeper.Specification.Matches)).Count > 1)
                {
                    continue;
                }
                var match = deeper.Own.FirstOrDefault(candidate =>
                    candidate.PathConditions.Any(condition => own.Holds(condition.LabelRight)) &&
                    candidate.PathConditions.Any(condition => parent.Holds(condition.LabelRight)));
                if (match != null)
                {
                    return (deeper.Path!, match);
                }
            }
            return null;
        }

        // Clause 2: the first feed that is not joined. A feed that is disconnected even with
        // its filters was cut before its chains were joined; one that only its filters join is
        // a product the filter hides.
        private static Disconnection FeedDisconnection(
            ImmutableList<Descendant> descendants, Specification feed)
        {
            var withFilters = Chains(feed);
            if (withFilters.Count > 1)
            {
                return ConditionCut(descendants) ?? FeedNotJoined(feed, withFilters);
            }
            var pathsOnly = Chains(PathsOnly(feed));
            return JoinedOnlyByCondition(feed, pathsOnly) ?? FeedNotJoined(feed, pathsOnly);
        }

        // The negative condition whose cut explains a feed that is disconnected even with its
        // filters, in the order the feeds walk them. A condition whose level is not joined to
        // the match that carries it is unrelated to that match; one that is joined, but whose
        // match is not yet joined to a chain a later match joins, is checked too early.
        //
        // Each of these shapes is advice about two givens, so a chain that reaches none is
        // left to the fallback, which names the labels instead.
        private static Disconnection? ConditionCut(ImmutableList<Descendant> descendants)
        {
            foreach (var descendant in descendants)
            {
                var level = FeedBuilder.Flatten(descendant.Specification.Matches);
                for (int index = 0; index < level.Count; index++)
                {
                    var carrier = level[index];
                    var before = level.GetRange(0, index + 1).Select(Paths).ToImmutableList();
                    foreach (var condition in carrier.ExistentialConditions.Where(each => !each.Exists))
                    {
                        var filtered = FeedBuilder.Level(condition.Matches);
                        var cut = ConditionUnrelated(descendant, carrier, before, filtered);
                        if (cut != null)
                        {
                            return cut;
                        }
                        cut = ConditionBeforeJoin(descendant, carrier, before, filtered, level, index);
                        if (cut != null)
                        {
                            return cut;
                        }
                    }
                }
            }
            return null;
        }

        // Kind 4: the negative condition's level is not joined by path conditions to the match
        // that carries it, so the condition removes every fact of that match whenever the
        // level matches anything at all.
        private static Disconnection? ConditionUnrelated(
            Descendant descendant, Match carrier, ImmutableList<Match> before, ImmutableList<Match> filtered)
        {
            var chains = Chains(AsFeed(descendant.Specification.Givens, before.AddRange(filtered)));
            var carried = chains.FirstOrDefault(chain => chain.Holds(carrier.Unknown.Name));
            if (carried == null || filtered.Any(match => carried.Holds(match.Unknown.Name)))
            {
                return null;
            }
            var away = chains.FirstOrDefault(chain =>
                chain != carried && filtered.Any(match => chain.Holds(match.Unknown.Name)));
            if (away == null || away.Givens.IsEmpty)
            {
                return null;
            }
            return Disconnection.ConditionUnrelated(carrier.Unknown.Name, away);
        }

        // Kind 3: the negative condition's level is joined to the match that carries it, but
        // the matches up to and including that match form two chains, and a later match joins
        // them. The excluding feed stops at the condition, before the join.
        private static Disconnection? ConditionBeforeJoin(
            Descendant descendant, Match carrier, ImmutableList<Match> before,
            ImmutableList<Match> filtered, ImmutableList<Match> level, int index)
        {
            var reach = Chains(AsFeed(descendant.Specification.Givens, before.AddRange(filtered)));
            var carried = reach.FirstOrDefault(chain => chain.Holds(carrier.Unknown.Name));
            if (carried == null || !filtered.Any(match => carried.Holds(match.Unknown.Name)))
            {
                return null;
            }

            var chains = Chains(AsFeed(descendant.Specification.Givens, before));
            var carrierChain = chains.FirstOrDefault(chain => chain.Holds(carrier.Unknown.Name));
            var other = chains.FirstOrDefault(chain => chain != carrierChain);
            if (carrierChain == null || other == null || carrierChain.Givens.IsEmpty || other.Givens.IsEmpty)
            {
                return null;
            }

            var joiner = level.GetRange(index + 1, level.Count - index - 1).FirstOrDefault(candidate =>
                candidate.PathConditions.Any(condition => carrierChain.Holds(condition.LabelRight)) &&
                candidate.PathConditions.Any(condition => other.Holds(condition.LabelRight)));
            if (joiner == null)
            {
                return null;
            }

            return Disconnection.ConditionBeforeJoin(
                carrier.Unknown.Name, carrierChain, other, joiner);
        }

        // Kind 5: the feed's path conditions leave two chains, and a negative condition on one
        // of its matches names labels in both. The filter is what makes the feed read as
        // connected, and a filter never joins.
        private static Disconnection? JoinedOnlyByCondition(
            Specification feed, ImmutableList<DisconnectedChain> chains)
        {
            if (chains.Count < 2)
            {
                return null;
            }
            foreach (var match in feed.Matches)
            {
                var carried = chains.FirstOrDefault(chain => chain.Holds(match.Unknown.Name));
                if (carried == null)
                {
                    continue;
                }
                foreach (var condition in match.ExistentialConditions.Where(each => !each.Exists))
                {
                    var names = Referenced(condition.Matches);
                    var other = chains.FirstOrDefault(chain =>
                        chain != carried && names.Any(chain.Holds));
                    if (other == null || other.Givens.IsEmpty || carried.Givens.IsEmpty ||
                        !names.Any(carried.Holds))
                    {
                        continue;
                    }
                    return Disconnection.JoinedOnlyByCondition(match.Unknown.Name, carried, other);
                }
            }
            return null;
        }

        // Kind 6: any other clause-2 failure, so that no refusal is silent about its labels.
        private static Disconnection FeedNotJoined(
            Specification feed, ImmutableList<DisconnectedChain> chains) =>
            Disconnection.FeedNotJoined(
                feed.Matches.Select(match => match.Unknown.Name).ToImmutableList(), chains);
    }

    /// <summary>
    /// One connected component of a specification's join graph: the labels it reaches that the
    /// specification declares, and the givens it reaches, each in written order.
    /// </summary>
    internal sealed class DisconnectedChain
    {
        public DisconnectedChain(ImmutableList<string> unknowns, ImmutableList<string> givens)
        {
            Unknowns = unknowns;
            Givens = givens;
        }

        public ImmutableList<string> Unknowns { get; }
        public ImmutableList<string> Givens { get; }

        public bool Holds(string label) => Unknowns.Contains(label) || Givens.Contains(label);

        /// <summary>The label a message names this chain by: its first unknown, or its givens.</summary>
        public string First => Unknowns.IsEmpty ? DisconnectionMessage.Plain(Givens) : Unknowns[0];
    }

    /// <summary>
    /// Which of the ways a specification can be a product it is, so the refusal can advise the
    /// developer about the shape they wrote rather than name the rule alone.
    /// </summary>
    internal enum DisconnectionKind
    {
        /// <summary>The specification's own matches fall into chains nothing joins.</summary>
        SeparateChains,

        /// <summary>A child specification is a matrix, which a deeper one may fill in.</summary>
        ChildJoinsLate,

        /// <summary>A negative condition is checked before the chain it sits on is joined.</summary>
        ConditionBeforeJoin,

        /// <summary>A negative condition's level is joined to another given, not to its match.</summary>
        ConditionUnrelated,

        /// <summary>A feed's two chains are joined only by a negative condition.</summary>
        JoinedOnlyByCondition,

        /// <summary>Any other feed that nothing joins.</summary>
        FeedNotJoined
    }

    /// <summary>
    /// Why a specification is a product: which shape it is, and the labels the refusal names.
    ///
    /// It holds structure alone. <see cref="Message"/> is rendered from the kind and these
    /// labels on each read and is never stored beside them, so the prose cannot drift from the
    /// shape it describes. Tests assert the labels; the prose stands on review.
    /// </summary>
    internal sealed class Disconnection
    {
        private Disconnection(
            DisconnectionKind kind,
            ImmutableList<DisconnectedChain> chains,
            string? conditionLabel,
            string? childPath,
            string? joinerPath,
            Match? joiningMatch,
            ImmutableList<string> feedLabels)
        {
            Kind = kind;
            Chains = chains;
            ConditionLabel = conditionLabel;
            ChildPath = childPath;
            JoinerPath = joinerPath;
            JoiningMatch = joiningMatch;
            FeedLabels = feedLabels;
        }

        public DisconnectionKind Kind { get; }

        /// <summary>
        /// The chains the refusal names, in the order it names them. For a condition it is the
        /// chain of the match that carries the condition and then the chain across the cut,
        /// except in <see cref="DisconnectionKind.ConditionUnrelated"/>, where the condition's
        /// own label stands for the near side and the one chain is the far one. For
        /// <see cref="DisconnectionKind.ChildJoinsLate"/> it is the child's chain and then its
        /// parent's.
        /// </summary>
        public ImmutableList<DisconnectedChain> Chains { get; }

        /// <summary>The unknown whose negative condition cuts the feed, where one does.</summary>
        public string? ConditionLabel { get; }

        /// <summary>The component path of the disconnected child specification.</summary>
        public string? ChildPath { get; }

        /// <summary>The component path of the descendant whose match joins the chains.</summary>
        public string? JoinerPath { get; }

        /// <summary>The match that joins the two chains, later than the feed needs it.</summary>
        public Match? JoiningMatch { get; }

        /// <summary>The unknowns of the feed that is a product, in its order.</summary>
        public ImmutableList<string> FeedLabels { get; }

        /// <summary>
        /// Every label the message names, so a test can assert that the analysis picked the
        /// right side of the disconnection without reading the prose.
        /// </summary>
        public ImmutableList<string> Labels =>
            Chains.SelectMany(chain => chain.Unknowns.Concat(chain.Givens))
                .Concat(ConditionLabel == null ? Enumerable.Empty<string>() : new[] { ConditionLabel })
                .Concat(JoiningMatch == null
                    ? Enumerable.Empty<string>()
                    : new[] { JoiningMatch.Unknown.Name })
                .Concat(FeedLabels)
                .Distinct()
                .ToImmutableList();

        /// <summary>
        /// What is disconnected, in the developer's own labels; what that would mean; and the
        /// rewrite. Rendered from this disconnection's kind and labels, so it names the labels
        /// the developer wrote rather than one of a table of examples.
        /// </summary>
        public string Message => DisconnectionMessage.Render(this);

        public static Disconnection SeparateChains(ImmutableList<DisconnectedChain> chains) =>
            new Disconnection(
                DisconnectionKind.SeparateChains, chains, null, null, null, null,
                ImmutableList<string>.Empty);

        public static Disconnection ChildJoinsLate(
            string childPath, DisconnectedChain own, DisconnectedChain parent,
            string? joinerPath, Match? joiningMatch) =>
            new Disconnection(
                DisconnectionKind.ChildJoinsLate, ImmutableList.Create(own, parent), null,
                childPath, joinerPath, joiningMatch, ImmutableList<string>.Empty);

        public static Disconnection ConditionBeforeJoin(
            string conditionLabel, DisconnectedChain carried, DisconnectedChain other,
            Match joiningMatch) =>
            new Disconnection(
                DisconnectionKind.ConditionBeforeJoin, ImmutableList.Create(carried, other),
                conditionLabel, null, null, joiningMatch, ImmutableList<string>.Empty);

        public static Disconnection ConditionUnrelated(string conditionLabel, DisconnectedChain away) =>
            new Disconnection(
                DisconnectionKind.ConditionUnrelated, ImmutableList.Create(away), conditionLabel,
                null, null, null, ImmutableList<string>.Empty);

        public static Disconnection JoinedOnlyByCondition(
            string conditionLabel, DisconnectedChain carried, DisconnectedChain other) =>
            new Disconnection(
                DisconnectionKind.JoinedOnlyByCondition, ImmutableList.Create(carried, other),
                conditionLabel, null, null, null, ImmutableList<string>.Empty);

        public static Disconnection FeedNotJoined(
            ImmutableList<string> feedLabels, ImmutableList<DisconnectedChain> chains) =>
            new Disconnection(
                DisconnectionKind.FeedNotJoined, chains, null, null, null, null, feedLabels);
    }

    /// <summary>
    /// The prose of a refusal, rendered from a <see cref="Disconnection"/>.
    ///
    /// Each message says what is disconnected, in the developer's own labels and the givens
    /// each chain is reached from; what that would mean, every fact of one chain paired with
    /// every fact of the other; and the rewrite that states what they probably meant. They
    /// match jinaga/jinaga.js#437's <em>Error messages</em> section for shape, so the two
    /// libraries advise a developer alike; a change to one should be made to the other. Parity
    /// is of shape and advice, not of text.
    /// </summary>
    internal static class DisconnectionMessage
    {
        public static string Render(Disconnection disconnection)
        {
            switch (disconnection.Kind)
            {
                case DisconnectionKind.SeparateChains:
                    return SeparateChains(disconnection.Chains);
                case DisconnectionKind.ChildJoinsLate:
                    return ChildJoinsLate(disconnection);
                case DisconnectionKind.ConditionBeforeJoin:
                    return ConditionBeforeJoin(disconnection);
                case DisconnectionKind.ConditionUnrelated:
                    return ConditionUnrelated(disconnection);
                case DisconnectionKind.JoinedOnlyByCondition:
                    return JoinedOnlyByCondition(disconnection);
                default:
                    return FeedNotJoined(disconnection);
            }
        }

        // "Independent parts of a specification belong in sibling child projections, never in
        // one list of matches", without the article, so each site punctuates it for the
        // sentence it stands in rather than keeping a second copy of the words.
        private const string Siblings =
            "Independent parts of a specification belong in sibling child projections, " +
            "never in one list of matches";

        // "a negative condition filters a match; it never joins two", likewise.
        private const string Filters = "negative condition filters a match; it never joins two";

        // Where a sibling goes. A child specification inherits its parent's matches, so a
        // sibling is independent only where its parent matches nothing from the other chain.
        private const string NoMatchesAbove = "with no matches above them";

        // Kind 1. A specification can fall into more than two chains, so the count and the
        // groups are rendered from however many there are rather than from the two the shape is
        // usually written with.
        private static string SeparateChains(ImmutableList<DisconnectedChain> chains) =>
            $"This specification matches {Count(chains.Count)} groups of facts that nothing " +
            $"joins: {string.Join(", and ", chains.Select(Describe))}. {Pairing(chains)} " +
            $"{Siblings}: put each group in its own child, {NoMatchesAbove}.";

        // What one list of matches would come to. Two chains pair; more than two multiply, and
        // naming one pair of them would understate what the store is being asked for.
        private static string Pairing(ImmutableList<DisconnectedChain> chains) =>
            chains.Count == 2
                ? $"As one list of matches, every '{chains[0].First}' would be paired with " +
                  $"every '{chains[1].First}'."
                : $"As one list of matches, every combination of " +
                  $"{Quote(chains.Select(chain => chain.First).ToImmutableList())} would be produced.";

        // Kind 2. Where a deeper descendant does join the chains, the shape is a matrix the
        // developer wrote in three levels instead of two, and the message names the match that
        // joins them and the child that holds it. Where none does, the two chains are genuinely
        // independent: the advice to use two levels would have no match to name, so the message
        // keeps the matrix and gives the sibling rewrite alone.
        private static string ChildJoinsLate(Disconnection disconnection)
        {
            var own = disconnection.Chains[0];
            var parent = disconnection.Chains[1];
            var opening =
                $"The child specification '{disconnection.ChildPath}' matches {Describe(own)}, " +
                $"but its parent matches {Describe(parent)}.";
            var matrix =
                $"so '{disconnection.ChildPath}' would hold every '{own.First}' for every " +
                $"'{parent.First}': a full matrix.";

            if (disconnection.JoiningMatch == null)
            {
                return
                    $"{opening} Nothing joins them, {matrix} {Siblings}: " +
                    $"put each group in its own child, {NoMatchesAbove}.";
            }
            var cell = disconnection.JoiningMatch.Unknown.Name;
            return
                $"{opening} Nothing joins them until '{disconnection.JoinerPath}' matches " +
                $"'{cell}', {matrix} For the cells that exist, use two levels instead of " +
                $"three: declare '{own.First}' in the same child as '{cell}', reached from it " +
                $"({Reached(disconnection.JoiningMatch, own.First)}), so each cell comes with " +
                $"its column. If you need every column, even those with no cells, independent " +
                $"parts belong in sibling child projections: give the rows, the columns and the " +
                $"cells each their own child, {NoMatchesAbove}.";
        }

        // Kind 3.
        private static string ConditionBeforeJoin(Disconnection disconnection)
        {
            var carried = disconnection.Chains[0];
            var other = disconnection.Chains[1];
            var unknown = disconnection.ConditionLabel;
            var away = other.First;
            var cell = disconnection.JoiningMatch!.Unknown.Name;
            return
                $"The negative condition on '{unknown}' is checked before '{unknown}' is joined to " +
                $"'{away}': at that point '{away}' is reached only from {Plain(other.Givens)} and " +
                $"'{unknown}' only from {Plain(carried.Givens)}, and '{cell}' joins them later. " +
                $"A {Filters}. Reorder the matches so that the chain is joined before the condition " +
                $"applies. Either start from '{unknown}', then reach '{cell}' from '{unknown}' and " +
                $"'{away}' from '{cell}', or reach '{cell}' from '{away}' and then '{unknown}' from " +
                $"'{cell}'. Both read the same results, and they differ in what reaches a replica. " +
                $"Starting from '{unknown}' sends no '{cell}' while its '{unknown}' is removed, but " +
                $"it sends every '{unknown}' of {Plain(carried.Givens)} and its removals, even " +
                $"where no '{away}' is joined. Reaching '{unknown}' through '{cell}' sends only " +
                $"joined facts, but it can send an '{cell}' whose '{unknown}' is removed.";
        }

        // Kind 4.
        private static string ConditionUnrelated(Disconnection disconnection)
        {
            var away = disconnection.Chains[0];
            var unknown = disconnection.ConditionLabel;
            var inner = away.First;
            var given = Plain(away.Givens);
            return
                $"The negative condition on '{unknown}' matches {Quote(away.Unknowns)}, which is " +
                $"reached from {given} but not from '{unknown}'. A {Filters}, so as written it " +
                $"removes every '{unknown}' whenever {given} has any '{inner}'. If the condition " +
                $"is about each '{unknown}', join '{inner}' to '{unknown}' (for example, " +
                $"{inner}->{unknown} = {unknown}). If it is a gate on {given} as a whole, it " +
                $"cannot be one specification: a condition on the given is refused too. Read the " +
                $"'{unknown}'s and {given}'s '{inner}'s as sibling child projections, " +
                $"{NoMatchesAbove}, and decide in your code.";
        }

        // Kind 5.
        private static string JoinedOnlyByCondition(Disconnection disconnection)
        {
            var carried = disconnection.Chains[0];
            var other = disconnection.Chains[1];
            var unknown = disconnection.ConditionLabel;
            var paired = other.First;
            var givenOther = Plain(other.Givens);
            return
                $"'{paired}' is reached only from {givenOther}, and '{unknown}' only from " +
                $"{Plain(carried.Givens)}. The negative condition on '{unknown}' mentions " +
                $"both, but a {Filters}, so " +
                $"every '{paired}' would be paired with every '{unknown}'. If you mean the " +
                $"'{unknown}'s that no '{paired}' of {givenOther} links, move '{paired}' " +
                $"into the condition. If you mean, for each '{paired}', the '{unknown}'s it " +
                $"does not link, that is a dense complement and a product however it is " +
                $"nested: read the links and the '{unknown}'s as sibling child projections, " +
                $"{NoMatchesAbove}, and compute the complement in your code.";
        }

        // Kind 6, the fallback.
        private static string FeedNotJoined(Disconnection disconnection) =>
            $"A feed of this specification, through {Quote(disconnection.FeedLabels)}, joins " +
            $"{string.Join(", to ", disconnection.Chains.Select(Describe))}, by nothing. " +
            $"{Siblings}. A {Filters}.";

        // "'a' and 'b', reached from p1", or just the labels where the chain reaches no given.
        private static string Describe(DisconnectedChain chain) =>
            chain.Givens.IsEmpty
                ? Quote(chain.Unknowns)
                : $"{Quote(chain.Unknowns)}, reached from {Plain(chain.Givens)}";

        private static string Quote(ImmutableList<string> labels) =>
            Conjoin(labels.Select(label => $"'{label}'").ToImmutableList());

        public static string Plain(ImmutableList<string> labels) => Conjoin(labels);

        // A count as a word, so the sentence reads. Past a handful the numeral is clearer than
        // a word would be, and a specification that falls into that many chains is past the
        // point where the prose carries the argument anyway.
        private static readonly string[] Numbers = { "no", "one", "two", "three", "four", "five", "six" };

        private static string Count(int count) =>
            count < Numbers.Length ? Numbers[count] : count.ToString(CultureInfo.InvariantCulture);

        private static string Conjoin(ImmutableList<string> parts)
        {
            if (parts.IsEmpty)
            {
                return "nothing";
            }
            if (parts.Count == 1)
            {
                return parts[0];
            }
            return string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }

        // A path condition that would reach `label` from `match`, written as
        // ToDescriptiveString prints one with its role types left off: it is advice about a
        // specification the developer has yet to write, not an echo of one they wrote, and it
        // may name a role the developer has not declared.
        private static string Reached(Match match, string label)
        {
            var condition = match.PathConditions.FirstOrDefault(each => each.LabelRight == label);
            if (condition == null)
            {
                return $"{label} = {match.Unknown.Name}->{label}";
            }
            var left = string.Join("", condition.RolesRight.Select(role => $"->{role.Name}"));
            var right = string.Join("", condition.RolesLeft.Select(role => $"->{role.Name}"));
            return $"{label}{left} = {match.Unknown.Name}{right}";
        }
    }
}
