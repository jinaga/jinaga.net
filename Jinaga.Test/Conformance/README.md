# Conformance vectors

`Vectors/` is a pinned copy of the conformance vectors in
[jinaga/jinaga-spec](https://github.com/jinaga/jinaga-spec), which states the
well-formedness check and the specification split in Lean, proves them, and
generates these vectors from the definitions.

**Pinned at** jinaga-spec `c67eadbd459ddebffd823f81fa95700194b9bdbc`.

| Directory | Test | What a vector holds |
|---|---|---|
| `Vectors/well-formed/` | `WellFormedVectorTest` | A specification, and whether it is well formed. |
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

`FeedBuilder.BuildFeeds` refuses a specification outside the hypotheses of the
feeds theorems, and `FeedBuilderTest` states those refusals; no vector covers
them, because the generator writes vectors only for specifications the theorems
hold for. One of them has no vector shape at all: jinaga-spec's
`Specification.given` is a list of labels, so a condition on a given is not a
shape `buildFeeds` is defined over, and `BuildFeeds` refuses it rather than
dropping it.

`FeedVectorTest` reads each vector's `expected.feeds` two ways.
`FeedBuilder.BuildFeeds` must build that many feeds, in that order, each printing
as the feed's `text`; and every feed, read through `MemoryStore` from the
vector's `givens` on its `graph`, must bind to its unknowns exactly the facts of
`expected.delivered`. The second check states what the feeds come to on a graph
rather than what they say, so a port that prints the expected text and delivers
the wrong facts still fails. The pinned vectors are the only statement here of
what the feeds are; `FeedBuilderTest` states only the refusals, which the vectors
do not cover.

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

## `Fixtures/`

`Fixtures/` is not part of the pin, and the procedure below does not touch it.

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

It lives outside `Vectors/` because the procedure below owns that directory and
replaces it wholesale.

## Updating the pin

1. In a checkout of jinaga-spec at the new commit, copy each directory listed
   above from `vectors/` over the directory of the same name here, deleting
   files that no longer exist there.
2. Replace the commit above.
3. Run `dotnet test Jinaga.Test`.

Never edit a vector by hand. A vector that fails means this library and the
specification disagree: change the library, or report the disagreement against
jinaga-spec.
