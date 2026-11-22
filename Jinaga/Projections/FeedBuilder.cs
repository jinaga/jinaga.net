using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    /// <summary>
    /// The feeds of a specification: chapter 12's ordinary, excluding and restoring feeds.
    /// A replicator answers <c>POST /feeds</c> with these, and a replica that pages every one
    /// of them reads what the replicator reads.
    ///
    /// Ported from <c>buildFeeds</c> in jinaga-spec (<c>JinagaSpec/Feeds.lean</c>), following
    /// the reference port <c>ports/typescript/feed-builder.ts</c>, at jinaga-spec
    /// <c>c67eadbd459ddebffd823f81fa95700194b9bdbc</c>, the commit
    /// <c>Jinaga.Test/Conformance/Vectors</c> is pinned at. Each method below names the Lean
    /// function it corresponds to.
    ///
    /// Walk the specification's matches, each joined with its positive conditions. At every
    /// negative condition, each feed built so far emits an <em>excluding</em> feed -- itself
    /// followed by the condition's level, stopping there -- goes on <em>filtered</em> by the
    /// condition, and goes on <em>restored</em> through the condition's level and then the
    /// level of one of that level's own negative conditions. A feed that completes the
    /// specification, filtered or restored, goes on to each child specification of the
    /// projection; an excluding feed stops before, so an excluded entity's details are
    /// deferred. A feed that walks no successor edge is dropped.
    ///
    /// The theorems these feeds satisfy (<c>feeds_cover</c>, <c>buildFeeds_feedShape</c>,
    /// <c>feed_antitone</c>, <c>feeds_defer</c>, <c>feed_converges</c>) assume every
    /// descendant of the specification is well formed, declares each label once, and has a
    /// path condition on every match. <see cref="BuildFeeds"/> refuses a specification that
    /// does not meet them rather than returning feeds that silently miss results.
    ///
    /// These feeds are not jinaga.js's. jinaga.js filters inside an excluding feed, keeps the
    /// other negative conditions of the fact a restoring feed restores, and leaves a positive
    /// condition inside a negative one unjoined; the spec does none of the three, and
    /// <c>docs/findings.md</c> in jinaga-spec says why.
    /// </summary>
    internal static class FeedBuilder
    {
        /// <summary>The feeds of a level, once the feed before it is complete.</summary>
        private delegate ImmutableList<ImmutableList<Match>> Continuation(ImmutableList<Match> feed);

        /// <summary>
        /// The feeds of a specification, in the order <c>buildFeeds</c> produces them.
        /// </summary>
        /// <exception cref="SpecificationException">
        /// The specification falls outside the hypotheses of the feeds theorems: it or one of
        /// its child specifications is not well formed, declares a label already declared on
        /// the way to it, or has a match with no path condition.
        /// </exception>
        public static ImmutableList<Specification> BuildFeeds(Specification specification)
        {
            Refuse(specification);
            return FeedsOf(
                specification,
                LevelFeeds(
                    ProjectionFeeds(specification.Projection),
                    ImmutableList<Match>.Empty,
                    specification.Matches));
        }

        /* --------------------------------- The feeds --------------------------------- */

        // levelFeeds: the feeds of the level `matches`, after the partial feed `feed`. Each
        // match joins the feed with its positive conditions; then each of its negative
        // conditions is walked. Each feed that completes the level is handed to `k`.
        private static ImmutableList<ImmutableList<Match>> LevelFeeds(
            Continuation k, ImmutableList<Match> feed, ImmutableList<Match> matches)
        {
            if (matches.IsEmpty)
            {
                return k(feed);
            }
            var head = ImmutableList.Create(matches[0]);
            var rest = matches.RemoveAt(0);
            return NegFeeds(
                next => LevelFeeds(k, next, rest),
                feed.AddRange(Level(head)),
                NegativesOf(head));
        }

        // negFeeds: the feeds of the partial feed `feed` with the negative conditions
        // `negatives` still to walk. At each, the feed so far emits an excluding feed, which
        // goes on to the condition's level and stops; and it goes on both filtered by the
        // condition's level and restored, through the condition's level and the level of one
        // of its own negative conditions.
        private static ImmutableList<ImmutableList<Match>> NegFeeds(
            Continuation k, ImmutableList<Match> feed, ImmutableList<ImmutableList<Match>> negatives)
        {
            if (negatives.IsEmpty)
            {
                return k(feed);
            }
            var condition = negatives[0];
            var rest = negatives.RemoveAt(0);
            var level = Level(condition);
            var filter = new ExistentialCondition(false, level);
            return ImmutableList.Create(feed.AddRange(level))
                .AddRange(NegFeeds(k, AddLast(filter, feed), rest))
                .AddRange(NegativesOf(condition).SelectMany(canceller =>
                    LevelFeeds(restored => NegFeeds(k, restored, rest), feed.AddRange(level), canceller)));
        }

        // projectionFeeds: a positive feed, one that completes its level, and then the feeds
        // of each child specification walked after it, each going on to the child's own
        // projection. An excluding feed never reaches here, so it carries no child's facts.
        private static Continuation ProjectionFeeds(Projection projection) =>
            feed => ImmutableList.Create(feed)
                .AddRange(Children(projection).SelectMany(child =>
                    LevelFeeds(ProjectionFeeds(child.Projection), feed, child.Matches)));

        // feedsOf: each partial feed as a feed, with the givens it uses and no projection.
        // A feed that walks no successor edge is dropped: it delivers only predecessors of the
        // givens, which a replica already holds.
        private static ImmutableList<Specification> FeedsOf(
            Specification specification, ImmutableList<ImmutableList<Match>> feeds) =>
            feeds
                .Select(matches => new Specification(
                    UsedGivens(specification.Givens, matches),
                    matches,
                    new CompoundProjection(ImmutableDictionary<string, Projection>.Empty, typeof(object))))
                .Where(TraversesSuccessors)
                .ToImmutableList();

        // usedGivens: the givens a feed names, in its path conditions or its filters, in the
        // specification's order. A feed's given is a label alone, and CheckGivens has already
        // refused a specification whose given carried a condition, so none is dropped here.
        private static ImmutableList<SpecificationGiven> UsedGivens(
            ImmutableList<SpecificationGiven> givens, ImmutableList<Match> matches)
        {
            var free = FreeInMatches(ImmutableList<string>.Empty, matches);
            return givens
                .Where(given => free.Contains(given.Label.Name))
                .Select(given => new SpecificationGiven(given.Label, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();
        }

        // traversesSuccessors: some path condition of the feed's matches walks from the unknown.
        private static bool TraversesSuccessors(Specification feed) =>
            feed.Matches.Any(match => match.PathConditions.Any(condition => !condition.RolesLeft.IsEmpty));

        /* ------------------------------ Levels and labels ------------------------------ */

        // flattenMatches with every positive condition lifted: each match keeps its path
        // conditions and its negative conditions, and the matches of each positive condition
        // follow it.
        private static ImmutableList<Match> Flatten(ImmutableList<Match> matches) =>
            matches
                .SelectMany(match => ImmutableList
                    .Create(new Match(
                        match.Unknown,
                        match.PathConditions,
                        match.ExistentialConditions.Where(condition => !condition.Exists).ToImmutableList()))
                    .AddRange(Flatten(match.ExistentialConditions
                        .Where(condition => condition.Exists)
                        .SelectMany(condition => condition.Matches)
                        .ToImmutableList())))
                .ToImmutableList();

        // level: a negative condition's level, as a feed continues with it. Its matches, with
        // every positive condition joined in, keeping only path conditions.
        private static ImmutableList<Match> Level(ImmutableList<Match> matches) =>
            Flatten(matches)
                .Select(match => new Match(
                    match.Unknown, match.PathConditions, ImmutableList<ExistentialCondition>.Empty))
                .ToImmutableList();

        // negativesOf: the negative conditions of a level, with every positive condition
        // joined in.
        private static ImmutableList<ImmutableList<Match>> NegativesOf(ImmutableList<Match> matches) =>
            Flatten(matches)
                .SelectMany(match => match.ExistentialConditions
                    .Where(condition => !condition.Exists)
                    .Select(condition => condition.Matches))
                .ToImmutableList();

        // addLast: add a condition to the last match of a feed. Every label a filter names is
        // bound by then, so it is checked once the feed so far is matched.
        private static ImmutableList<Match> AddLast(ExistentialCondition condition, ImmutableList<Match> feed) =>
            feed.IsEmpty
                ? feed
                : feed.SetItem(feed.Count - 1, new Match(
                    feed[feed.Count - 1].Unknown,
                    feed[feed.Count - 1].PathConditions,
                    feed[feed.Count - 1].ExistentialConditions.Add(condition)));

        // freeInMatches: the labels the matches name but do not bind, at any depth. `inner`
        // holds the labels bound around them.
        private static ImmutableList<string> FreeInMatches(
            ImmutableList<string> inner, ImmutableList<Match> matches)
        {
            var free = ImmutableList<string>.Empty;
            foreach (var match in matches)
            {
                foreach (var condition in match.PathConditions)
                {
                    if (!inner.Contains(condition.LabelRight))
                    {
                        free = free.Add(condition.LabelRight);
                    }
                }
                foreach (var condition in match.ExistentialConditions)
                {
                    free = free.AddRange(FreeInMatches(
                        inner.AddRange(condition.Matches.Select(inner2 => inner2.Unknown.Name)),
                        condition.Matches));
                }
            }
            return free;
        }

        // declaredMatches: every label the matches declare, at any depth.
        private static ImmutableList<string> Declared(ImmutableList<Match> matches) =>
            matches
                .SelectMany(match => ImmutableList.Create(match.Unknown.Name)
                    .AddRange(match.ExistentialConditions.SelectMany(condition => Declared(condition.Matches))))
                .ToImmutableList();

        // Projection.children: the child specifications a projection holds. A CompoundProjection
        // here may hold a compound, whose children belong to the same level.
        //
        // The order of the children is the order of the feeds they produce. jinaga-spec's
        // composite projection is a list, so its children come in the order they were written;
        // a CompoundProjection holds a dictionary, which does not keep that order, so there is
        // none to follow. The children are taken by name instead, ordinal, which is
        // deterministic, independent of the current culture, and the same order
        // <see cref="CompoundProjection.ToDescriptiveString"/> prints a projection's
        // components in.
        private static IEnumerable<CollectionProjection> Children(Projection projection)
        {
            switch (projection)
            {
                case CollectionProjection collection:
                    return new[] { collection };
                case CompoundProjection compound:
                    return compound.Names
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .SelectMany(name => Children(compound.GetProjection(name)));
                default:
                    return Enumerable.Empty<CollectionProjection>();
            }
        }

        /* --------------------------------- The refusals --------------------------------- */

        // The hypotheses of the feeds theorems, each over every descendant of the
        // specification: WellFormed (isWellFormedDeep), DistinctLabels, and RootedMatches.
        // Then the shape the theorems are stated over at all: givens that are labels alone.
        private static void Refuse(Specification specification)
        {
            var errors = WellFormedSpecification.Errors(specification);
            if (errors.Count > 0)
            {
                throw new SpecificationException(
                    "A specification that is not well formed has no feeds. " +
                    string.Join(" ", errors));
            }
            CheckLabels(
                specification.Givens.Select(given => given.Label.Name).ToImmutableList(),
                specification.Matches,
                specification.Projection);
            CheckRooted(specification.Matches, specification.Projection);
            CheckGivens(specification.Givens);
        }

        // jinaga-spec's Specification.given is a List Label, so a condition on a given is not
        // a shape buildFeeds is defined over, and no theorem says what its feeds are. The
        // reference port drops such a condition because its own type cannot hold one;
        // SpecificationGiven can, and dropping it would be worse than refusing it. A store
        // reads nothing at all while a condition on a given fails (IStore.Read), so a feed
        // that had quietly lost the condition would deliver facts the specification excludes.
        private static void CheckGivens(ImmutableList<SpecificationGiven> givens)
        {
            var conditioned = givens.FirstOrDefault(given => !given.ExistentialConditions.IsEmpty);
            if (conditioned != null)
            {
                throw new SpecificationException(
                    $"The given '{conditioned.Label.Name}' carries an existential condition, " +
                    "which a feed cannot: the feeds are defined over a specification whose " +
                    "givens are labels alone.");
            }
        }

        // DistinctLabels of every descendant. Joining positive conditions into one level would
        // bind a label that two sibling conditions both declare twice, and a child
        // specification's feeds bind its parent's labels too. Sibling child specifications may
        // share a label: no feed binds both. jinaga.js renames such a label instead
        // (jinaga/jinaga.js#430), and the spec does not prove that renaming preserves results.
        private static void CheckLabels(
            ImmutableList<string> outer, ImmutableList<Match> matches, Projection projection)
        {
            var labels = outer.AddRange(Declared(matches));
            var repeated = labels.FirstOrDefault(label => labels.IndexOf(label) != labels.LastIndexOf(label));
            if (repeated != null)
            {
                throw new SpecificationException(
                    $"Rename the label '{repeated}': feeds need every label to be declared once.");
            }
            foreach (var child in Children(projection))
            {
                CheckLabels(labels, child.Matches, child.Projection);
            }
        }

        // RootedMatches of every descendant: every match, at any depth, has a path condition.
        // A match that has none is bound to every fact of its type, and a feed carrying it
        // would deliver them all.
        private static void CheckRooted(ImmutableList<Match> matches, Projection projection)
        {
            CheckRootedMatches(matches);
            foreach (var child in Children(projection))
            {
                CheckRooted(child.Matches, child.Projection);
            }
        }

        private static void CheckRootedMatches(ImmutableList<Match> matches)
        {
            foreach (var match in matches)
            {
                if (match.PathConditions.IsEmpty)
                {
                    throw new SpecificationException(
                        $"The match '{match.Unknown.Name}' has no path condition, " +
                        "so it has no feed: every match of a feed is reached from a label.");
                }
                foreach (var condition in match.ExistentialConditions)
                {
                    CheckRootedMatches(condition.Matches);
                }
            }
        }
    }
}
