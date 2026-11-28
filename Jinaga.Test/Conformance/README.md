# Conformance vectors

There are two independent pins here: `Vectors/`, from jinaga-spec, and
`Reference/`, from jinaga.js. Each has its own commit and its own update
procedure.

`Vectors/` is a pinned copy of the conformance vectors in
[jinaga/jinaga-spec](https://github.com/jinaga/jinaga-spec), which states the
well-formedness check and the specification split in Lean, proves them, and
generates these vectors from the definitions.

**Pinned at** jinaga-spec `79d406a3be968f5f7fa0331932542c7a43bfb973`.

| Directory | Test | What a vector holds |
|---|---|---|
| `Vectors/well-formed/` | `WellFormedVectorTest` | A specification, whether it is well formed, and whether it is connected. |
| `Vectors/split/` | `SplitVectorTest` | A specification, the head and tail its split produces, and whether an authorization rule over it is refused because its tail reads the fact under authorization. |
| `Vectors/inverse/` | `InverseVectorTest` | A specification, a graph, a subscription's givens, a fresh fact or batch, the reduction that removes any conditions on the givens, and the inverses the proven algorithm builds, the reordering of each, and the rows all of them find. |
| `Vectors/feeds/` | `FeedVectorTest` | A specification, a graph, a subscription's givens, the feeds the proven decomposition builds, and every fact their tuples deliver. |

An inverse vector also carries `expected.exact`, the rows the *exact* inverses
add and remove. Nothing here reads that field: `ExactInverseVectorTest` reads
the fixture described below instead, which states the same changes together
with the graph and the row labels it keys them by.

`ConformanceVector` knows which fields each kind of vector carries, and refuses
one that holds a field it does not read, naming the field. A re-pin that adds a
field states something no test here reads, so it fails loudly. It checks three
levels: the vector's own fields, the fields of its `expected`, and the fields of
each object `expected` holds -- a feed or an inverse and its text, the split's
head and tail, an inverse vector's `expected.exact`, a nested result. **It stops
at a specification's own interior.** A specification, wherever it appears, is read
by `ConformanceVector.ToSpecification`, which refuses a projection whose `type` it
does not know but does not police a field added to a match or a condition. A
change to the specification JSON shape is caught by the vector's `text` not
matching, not by this check.

Two fields it reads nothing from are listed so that the check passes: an inverse
vector's `expected.exact`, above, and a feed vector's `expected.results` and
`expected.nestedResults`, the rows the specification itself reads, which the
inverse and observer tests already cover from the specification's own side. Every
other listed field is read by the test named in the table, and each vector's
`name`, `description` and `source` describe the case.

`SplitVectorTest` compares the head and tail as printed by
`ToDescriptiveString`, which prints exactly what jinaga.js's
`describeSpecification` does, so the comparison covers the givens, matches and
projections of each. It also checks that the loader reads the specification the
vector's `text` describes.

`InverseVectorTest` reads each of a vector's `expected.inverses`, and each of
its `expected.reordered`, through `MemoryStore`, starting from the
subscription's givens and each fresh fact in turn, and checks that together
they find exactly the vector's `expected.candidates`. Where the fresh fact is
one of the subscription's givens, it also reads the whole specification from
the subscription's givens, because nothing was read before that given arrived
and so every row is a candidate (`factCandidates` in jinaga-spec's
`JinagaSpec/Reorder.lean`, which jinaga.js calls `givenCandidateInverse`).

It also checks the port against the reduction that removes conditions on the
givens: `InverseCandidates.ReduceGivenConditions` must produce a vector's
`expected.reduced` where it states one, and the specification itself where it
does not. The inverses are then built from that reduction, as the proven
algorithm takes no conditions on a given, and `CandidateInverses`, the entry
point the observer calls, must produce the vector's `expected.reordered` from
the specification as written.

A well-formed vector's `expected.connected` is the other oracle's verdict,
`isConnectedDeep` (`JinagaSpec/Connected.lean`): every descendant is connected
over the givens it uses, and every feed is joined by its path conditions rather
than by a negative condition. A given no descendant uses is allowed
(jinaga/jinaga-spec#60), and `isConnected` counts only the givens the matches
use, so `unused-given`, `no-matches` and `no-matches-two-givens` are connected.
`WellFormedVectorTest.TheConnectivityCheckAgreesWithTheOracle` reads the field,
against `ConnectedSpecification`. The two verdicts are independent, and the
field is stated for every vector, so the connectivity check runs on
specifications the well-formedness check refuses.

`FeedBuilder.BuildFeeds` refuses a specification outside the hypotheses of the
feeds theorems, and `FeedBuilderTest` states those refusals. The well-formed
vectors cover the connectivity hypothesis, because the generator states
`connected` for a specification whether it holds or not;
`FeedBuilderTest` selects those vectors by their `expected` fields and requires
a refusal for each. No vector covers the other hypotheses, because the
generator writes vectors only for specifications the theorems hold for. One of
them has no vector shape at all: jinaga-spec's `Specification.given` is a list
of labels, so a condition on a given is not a shape `buildFeeds` is defined
over, and `BuildFeeds` refuses it rather than dropping it.

`FeedVectorTest` reads each vector's `expected.feeds` two ways.
`FeedBuilder.BuildFeeds` must build that many feeds, in that order, each printing
as the feed's `text`; and every feed, read through `MemoryStore` from the
vector's `givens` on its `graph`, must bind to its unknowns exactly the facts of
`expected.delivered`. The second check states what the feeds come to on a graph
rather than what they say, so a port that prints the expected text and delivers
the wrong facts still fails. The pinned vectors are the only statement here of
what the feeds are; `FeedBuilderTest` states the refusals, and the rewrites each
connectivity refusal advises, which it builds directly rather than reading from
a vector.

A vector's graph may name a predecessor
that content addressing cannot reach -- a fact saved later, or a cycle -- so
these facts go into the store by the reference `(type, "h" + id)` rather than
by their hashes. `MemoryStore.PlaceGraphByReference` is there for that, and
only the conformance vectors use it.

`vectors/README.md` in jinaga-spec describes the format. A vector's
specification is JSON in the shape jinaga.js serializes. `ConformanceVector`
loads it into a `Specification`, partitioning each match's conditions into path
and existential conditions, because `Match` here keeps them apart.

A `specification` component of a composite projection, a child specification,
loads as a `CollectionProjection`, whose matches go on from its parent's.
`WellFormedVectorTest` reads it as a descendant checked in its own right
(`isWellFormedDeep`), and `SplitVectorTest` as labels the tail may need given.

## `Reference/`

`Reference/` is a pinned copy of the output of jinaga.js's own `buildFeeds` and
`skeletonOfSpecification`, generated by the vector CLI
[jinaga/jinaga.js#387](https://github.com/jinaga/jinaga.js/issues/387) added.
It is a separate pin from `Vectors/`, against a different repository, and the
procedure below is its own.

**Pinned at** jinaga.js `a8ad06e15227838c2c0d4a4d462a3b7dd39885e3`.

| Directory | Test | What a vector holds |
|---|---|---|
| `Reference/feeds/` | `SkeletonVectorTest` | A specification, the feeds jinaga.js builds for it, and the skeleton jinaga.js gives each of those feeds. |

jinaga-spec does not define skeletons, so there is no Lean definition to port
and no theorem to lean on. **jinaga.js is the reference**, because a replicator
decides whether to serve a feed by comparing its skeleton with the skeletons of
the feeds of its distribution rules, and that replicator runs jinaga.js's
implementation. `ReferenceVector` reads these vectors; it is a separate loader
from `ConformanceVector`, because a reference vector carries no `expected`, no
graph and no givens, and refuses a field it does not read in the same way.

`Skeleton` is deliberately **not** canonical: `factIndex` and `edgeIndex` are
assigned by position in the walk, so two specifications that mean the same thing
but are written in a different order get different skeletons (L7a and O7 in
jinaga.js's `docs/analysis/specification-invariants.md`, both still open there).
A port whose skeletons were equal where the reference's are unequal would grant
distribution the replicator refuses.

`SkeletonVectorTest.ThePortBuildsTheSkeletonOfEveryReferenceFeed` builds the
skeleton of each feed **the vector states**, so it tests the skeleton alone and
not this library's feed decomposition. `Skeleton.Of` must match the vector's
skeleton for that feed, compared field by field: the facts, the inputs, the
top-level edges, the nesting of the not-exists conditions, the edges inside
them, and the outputs.

`Match` holds a match's path conditions and its existential conditions apart,
where jinaga.js keeps the one list they are written in. So `Skeleton.Of` walks
every path condition of a match and then every existential condition of it, and
a specification that wrote an existential condition first would be indexed
differently here. **No pinned vector has that shape**, in a vector's own
specification or in any of its feeds, and
`SkeletonVectorTest.NoReferenceVectorWritesAnExistentialConditionBeforeAPathCondition`
reads the vector JSON and says so, so a re-pin that introduces one fails here
rather than quietly changing what the walk indexes. It has to read the JSON:
loading a vector partitions each match's conditions into the two lists `Match`
holds, so the order they were written in survives nowhere else.

`Skeleton` overrides neither `Equals` nor `GetHashCode`, on purpose. The
comparisons that decide distribution belong to the engine
([#210](https://github.com/jinaga/jinaga.net/issues/210)): `skeletonsEqual`
over sets, `skeletonContains`, and the permutation of the inputs. An `Equals`
here would match none of them, and would invite a caller, or a later test, to
compare skeletons by a rule the replicator does not use. `SkeletonFields` in
`Jinaga.Test` spreads a skeleton into its fields instead, so a test says which
fields it is comparing.

### The feeds here are not jinaga.js's

This library builds the feeds jinaga-spec proves, not the ones jinaga.js builds.
The maintainer decided that on 2026-11-21, recorded on
[#201](https://github.com/jinaga/jinaga.net/issues/201), and again on
2026-11-28 on [#394](https://github.com/jinaga/jinaga.net/issues/394): nothing
here is patched to make the two agree, and the difference is reported rather
than fixed. This library ports from jinaga.js only what jinaga-spec does not
define, which is the skeleton.

`SkeletonVectorTest.TheFeedsEitherAgreeWithJinagaJsOrAreAKnownDifference` holds
every reference vector to one of three outcomes, and its list is exact in both
directions: a vector that newly disagrees fails, and so does one in the list
that has come to agree. A difference resolved on either side therefore reaches
this suite at the next re-pin instead of sitting in the list as a stale claim.

| Outcome | What it means |
|---|---|
| agrees | `BuildFeeds` returns the vector's feeds, feed for feed, as `ToDescriptiveString` prints them. |
| `differs` | Both build feeds, and they are not the same feeds. |
| `refused` | `BuildFeeds` refuses the specification, so there are no feeds to compare. |

**The `refused` pair**, `repeated-label-with-next` and
`repeated-label-without-next`, declare one label twice. jinaga.js renames the
repeated label and goes on
([jinaga/jinaga.js#430](https://github.com/jinaga/jinaga.js/pull/430)), and
jinaga-spec does not prove that renaming preserves results, so `FeedBuilder`
refuses the specification instead. The `-respelled` variants of the same two
cases, whose labels are already distinct, are feed differences like the rest.

**The `differs` group** is the excluding and restoring feeds. Inspected on
`minimal-via-successors` and `served-and-unfrozen`: jinaga.js carries the
negative condition into the excluding and restoring feeds as a filter, and
emits the feeds in a different order, where jinaga-spec's `buildFeeds` leaves
the excluding feed unfiltered. On `served-and-unfrozen` and
`served-and-unfrozen-with-regions` that also changes how many feeds there are.
`FeedBuilder` names the three sources jinaga-spec's `docs/findings.md` records
for the two implementations diverging.

**Both groups are tracked by
[jinaga/jinaga.js#463](https://github.com/jinaga/jinaga.js/issues/463)**, which
is jinaga.js converging on the spec. The maintainer decided on 2026-11-28
([#394](https://github.com/jinaga/jinaga.net/issues/394)) that jinaga-spec
defines the feeds in both libraries, so every entry in the list above comes off
it as jinaga.js converges, and the list is empty when the two libraries build
the same feeds. Nothing in this repository changes to close one of them.

### Updating the `Reference/` pin

1. In a checkout of jinaga.js at the new commit, run `npm install` and then
   `npm run vectors -- <outdir>`. It writes one file per case to
   `<outdir>/feeds/`, and two runs at one commit write byte-identical files.
2. Copy `<outdir>/feeds/` over `Reference/feeds/` here, deleting files that no
   longer exist there. The CLI already deletes a vector of a case jinaga.js has
   renamed or removed, so `<outdir>/feeds` is exactly the current set.
3. Replace the commit above.
4. Run `dotnet test Jinaga.Test`. Expect the difference list in
   `SkeletonVectorTest` to need updating, and read each change before making
   it: a case that has come to agree is a difference resolved, and a case that
   newly differs is a divergence to report.

Never edit a vector by hand. A skeleton vector that fails means this port and
the replicator disagree about a rule's identity, which is a defect here. A feed
vector that newly fails is a divergence to report against jinaga-spec or
jinaga.js, as above.

## `Fixtures/`

`Fixtures/` is part of neither pin, and neither update procedure touches it.

| File | Test | Where it came from |
|---|---|---|
| `Fixtures/exact-inverse-vectors.json` | `ExactInverseVectorTest` | 39 cases generated from the Lean definitions in [jinaga/jinaga-spec#19](https://github.com/jinaga/jinaga-spec/pull/19), at jinaga-spec `65a5d18`, and posted as the first comment on [jinaga/jinaga.net#329](https://github.com/jinaga/jinaga.net/issues/329). |

Each case names a specification, a graph, a subscription's givens, a fresh fact
or batch, the top-level unknowns as `keyLabels`, and the rows its `expected`
calls `gained`, `lost`, `adds` and `removes`. `adds` and `removes` are
jinaga-spec's `changes` (`JinagaSpec/Exact.lean`) with the projection set to
`keyLabels`, so every case falls under `projectsUnknowns`, the hypothesis the
remove direction needs. `gained` is contained in `adds`, `lost` in `removes`,
and `adds` and `removes` are disjoint; `ExactInverseVectorTest` asserts all
three as a check on its own harness.

It lives outside `Vectors/` because that directory's update procedure owns it
and replaces it wholesale.

## Updating the `Vectors/` pin

1. In a checkout of jinaga-spec at the new commit, copy each directory listed
   above from `vectors/` over the directory of the same name here, deleting
   files that no longer exist there.
2. Replace the commit above.
3. Run `dotnet test Jinaga.Test`.

Never edit a vector by hand. A vector that fails means this library and the
specification disagree: change the library, or report the disagreement against
jinaga-spec.
