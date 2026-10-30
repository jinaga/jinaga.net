# Conformance vectors

`Vectors/` is a pinned copy of the conformance vectors in
[jinaga/jinaga-spec](https://github.com/jinaga/jinaga-spec), which states the
well-formedness check and the specification split in Lean, proves them, and
generates these vectors from the definitions.

**Pinned at** jinaga-spec `65a5d18524a05e7720f3b58e5a9b59d9e1b523bf`.

| Directory | Test | What a vector holds |
|---|---|---|
| `Vectors/well-formed/` | `WellFormedVectorTest` | A specification, and whether it is well formed. |
| `Vectors/split/` | `SplitVectorTest` | A specification, the head and tail its split produces, and whether an authorization rule over it is refused because its tail reads the fact under authorization. |
| `Vectors/inverse/` | `InverseVectorTest` | A specification, a graph, a subscription's givens, a fresh fact or batch, the reduction that removes any conditions on the givens, and the inverses the proven algorithm builds, the reordering of each, and the rows all of them find. |

An inverse vector also carries `expected.exact`, the rows the *exact* inverses
add and remove. Nothing here reads that field: `ExactInverseVectorTest` reads
the fixture described below instead, which states the same changes together
with the graph and the row labels it keys them by.

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

An inverse vector's graph may name a predecessor
that content addressing cannot reach -- a fact saved later, or a cycle -- so
these facts go into the store by the reference `(type, "h" + id)` rather than
by their hashes. `MemoryStore.PlaceGraphByReference` is there for that, and
only the conformance vectors use it.

`vectors/README.md` in jinaga-spec describes the format. A vector's
specification is JSON in the shape jinaga.js serializes. `ConformanceVector`
loads it into a `Specification`, partitioning each match's conditions into path
and existential conditions, because `Match` here keeps them apart.

## `Fixtures/`

`Fixtures/` is not part of the pin, and the procedure below does not touch it.

| File | Test | Where it came from |
|---|---|---|
| `Fixtures/exact-inverse-vectors.json` | `ExactInverseVectorTest` | 39 cases generated from the Lean definitions in [jinaga/jinaga-spec#19](https://github.com/jinaga/jinaga-spec/pull/19), at the pin above, and posted as the first comment on [jinaga/jinaga.net#329](https://github.com/jinaga/jinaga.net/issues/329). |

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
