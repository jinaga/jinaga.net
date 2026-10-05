namespace Jinaga.Notebooks.Test.Models;

// Optional predecessors. This file is compiled with nullable reference types
// enabled, so a single predecessor declared `Region?` carries a nullable
// annotation and a predecessor declared `Region` does not.

[FactType("Optional.Region")]
public record Region(string name);

[FactType("Optional.Site")]
public record Site(Region region, Region? backup);
