using Jinaga.Notebooks.Test.Models;
using Jinaga.Notebooks.Test.TypeGraphs;
using static Jinaga.Notebooks.Dot.Renderer;

namespace Jinaga.Notebooks.Test;

/// <summary>
/// Every document the renderers produce for the models in this project, named so
/// that a failure says which one. A test that holds for all of them at once
/// draws on this rather than listing models again.
/// </summary>
public static class Fixtures
{
    public static readonly Type[] Deletions =
    {
        typeof(EmployeeDeleted), typeof(OfficeDeleted), typeof(OfficeRestored),
        typeof(AuditDeleted), typeof(CompanyDeleted), typeof(BadgeDeleted),
        typeof(AssignmentDeleted), typeof(AssignmentDeletionReason),
        typeof(TransferDeleted), typeof(TransferRestored)
    };

    public static IEnumerable<(string Description, Type[] Types)> TypeSelections()
    {
        yield return ("the catalog", TypeCatalog.All);
        yield return ("the deletion markers", Deletions);
        yield return ("the catalog and its deletion markers", TypeCatalog.All.Concat(Deletions).ToArray());
        yield return ("a fact reached through two roles", new[] { typeof(Transfer) });
        yield return ("an optional predecessor", new[] { typeof(Site) });
        yield return ("predecessors without nullable annotations", new[] { typeof(Post) });
        yield return ("awkward fact type names", new[] { typeof(Child) });
    }

    public static IEnumerable<(string Description, string Dot)> TypeGraphs()
    {
        foreach (var (description, types) in TypeSelections())
        {
            yield return ($"the full graph of {description}", RenderTypes(types));
            yield return ($"the compact graph of {description}", RenderTypesCompact(types));
        }
    }

    public static IEnumerable<(string Description, string Dot)> FactGraphs(JinagaClient j)
    {
        string Render(params object[] projections) =>
            Jinaga.Notebooks.Dot.JinagaClientExtensions.RenderFacts(j, projections);

        var acme = new Company("Acme");
        var dallas = new Office(acme, "Dallas");
        var austin = new Office(acme, "Austin");
        var alice = new Employee(acme, 1001);
        var site = new Site(new Region("North"), null);

        yield return ("a fact with no predecessors", Render(acme));
        yield return ("a fact with a predecessor", Render(dallas));
        yield return ("an array of predecessors", Render(new Assignment(alice, new[] { dallas, austin })));
        yield return ("two roles of the same type", Render(new Transfer(alice, dallas, austin)));
        yield return ("fields of every kind", Render(new Badge(alice, "B-17", 3.5, true, "night")));
        yield return ("fields that are all null", Render(new Measurement(site, null, null, null, null, null, null)));
        yield return ("a field holding an empty string", Render(new Measurement(site, null, null, null, null, null, "")));
        yield return ("a field longer than the limit",
            Render(new Office(acme, "Llanfairpwllgwyngyllgogerychwyrndrobwllllantysiliogogogoch")));
        yield return ("a field holding markup", Render(new Office(acme, "<b>R&D</b>")));
        yield return ("a fact type name with an ampersand", Render(new Ampersand("x")));
        yield return ("a fact type name outside ASCII", Render(new Unicode("x")));
        yield return ("a fact type name with a quote", Render(new Quoted("x")));
    }

    /// <summary>
    /// A document Graphviz draws while warning about every node, so that it
    /// writes far more to standard error than a pipe buffer holds. Nothing the
    /// renderers emit looks like this; it is here so that running Graphviz over
    /// a document is exercised with both of its output streams full.
    /// </summary>
    public static (string Description, string Dot) NoisyDocument()
    {
        // Graphviz warns once per unsupported style and quotes the whole style
        // name back, so a long name buys the volume with few enough nodes to
        // lay out in milliseconds.
        var padding = new string('x', 300);
        var statements = Enumerable.Range(0, 400)
            .Select(index => $"    \"n{index}\" [style=notastyle{index}{padding}]");
        return ("a document Graphviz warns about at every node",
            string.Join("\n", new[] { "digraph {", "    rankdir=BT" }.Concat(statements).Append("}")));
    }

    public static IEnumerable<(string Description, string Dot)> All(JinagaClient j) =>
        TypeGraphs().Concat(FactGraphs(j)).Append(NoisyDocument());
}
