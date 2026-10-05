namespace Jinaga.Notebooks.Test.Models;

// Fact type names that are awkward to write into a DOT document: a quote, which
// ends a quoted name early; an ampersand, which is markup in an HTML label; and
// letters outside ASCII. A fact type name is whatever its attribute says, so the
// renderers have to carry all three.

[FactType("Awkward \"Quoted\" Name")]
public record Quoted(string label);

[FactType("My Type & Co")]
public record Ampersand(string label);

[FactType("Awkward.Ünïcôde")]
public record Unicode(string label);

[FactType("Awkward.Child")]
public record Child(Quoted quoted, Ampersand ampersand, Unicode unicode);
