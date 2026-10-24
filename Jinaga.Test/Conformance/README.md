# Conformance vectors

`Vectors/` is a pinned copy of the conformance vectors in
[jinaga/jinaga-spec](https://github.com/jinaga/jinaga-spec), which states the
well-formedness check and the specification split in Lean, proves them, and
generates these vectors from the definitions.

**Pinned at** jinaga-spec `07d5788576975265a6179270a857e954c5678185`.

| Directory | Test | What a vector holds |
|---|---|---|
| `Vectors/well-formed/` | `WellFormedVectorTest` | A specification, and whether it is well formed. |
| `Vectors/split/` | `SplitVectorTest` | A specification, the head and tail its split produces, and whether an authorization rule over it is refused because its tail reads the fact under authorization. |
| `Vectors/inverse/` | `InverseVectorTest` | A specification, a graph, a subscription's givens, a fresh fact or batch, and the inverses the proven algorithm builds, the reordering of each, and the rows all of them find. |

`SplitVectorTest` compares the head and tail as printed by
`ToDescriptiveString`, which prints exactly what jinaga.js's
`describeSpecification` does, so the comparison covers the givens, matches and
projections of each. It also checks that the loader reads the specification the
vector's `text` describes.

`InverseVectorTest` reads each of a vector's `expected.inverses`, and each of
its `expected.reordered`, through `MemoryStore`, starting from the
subscription's givens and each fresh fact in turn, and checks that together
they find exactly the vector's `expected.candidates`. It gates a port of the
proven inverse algorithm: the vectors say what such a port must produce, so
they are here before it is. An inverse vector's graph may name a predecessor
that content addressing cannot reach -- a fact saved later, or a cycle -- so
these facts go into the store by the reference `(type, "h" + id)` rather than
by their hashes. `MemoryStore.PlaceGraphByReference` is there for that, and
only the conformance vectors use it.

`vectors/README.md` in jinaga-spec describes the format. A vector's
specification is JSON in the shape jinaga.js serializes. `ConformanceVector`
loads it into a `Specification`, partitioning each match's conditions into path
and existential conditions, because `Match` here keeps them apart.

## Updating the pin

1. In a checkout of jinaga-spec at the new commit, copy each directory listed
   above from `vectors/` over the directory of the same name here, deleting
   files that no longer exist there.
2. Replace the commit above.
3. Run `dotnet test Jinaga.Test`.

Never edit a vector by hand. A vector that fails means this library and the
specification disagree: change the library, or report the disagreement against
jinaga-spec.
