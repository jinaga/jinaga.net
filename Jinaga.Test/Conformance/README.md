# Conformance vectors

`Vectors/` is a pinned copy of the conformance vectors in
[jinaga/jinaga-spec](https://github.com/jinaga/jinaga-spec), which states the
well-formedness check and the specification split in Lean, proves them, and
generates these vectors from the definitions.

**Pinned at** jinaga-spec `b952bda09a635d90ae4e178dd82d6cf61859d937`.

| Directory | Test | What a vector holds |
|---|---|---|
| `Vectors/well-formed/` | `WellFormedVectorTest` | A specification, and whether it is well formed. |

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
