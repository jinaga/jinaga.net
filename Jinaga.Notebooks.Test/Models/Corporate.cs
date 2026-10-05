namespace Jinaga.Notebooks.Test.Models;

// One small model whose fact types each exercise a shape the renderers must handle:
// a root with no predecessors, a single predecessor, an array of predecessors,
// a self-referencing mutable property, two roles of the same type, and fields that
// are not facts.

[FactType("Corporate.Company")]
public record Company(string identifier);

[FactType("Corporate.Office")]
public record Office(Company company, string city);

[FactType("Corporate.Office.Name")]
public record OfficeName(Office office, string value, OfficeName[] prior);

[FactType("Corporate.Employee")]
public record Employee(Company company, int employeeNumber);

[FactType("Corporate.Assignment")]
public record Assignment(Employee employee, Office[] offices);

[FactType("Corporate.Transfer")]
public record Transfer(Employee employee, Office from, Office to);

[FactType("Corporate.Badge")]
public record Badge(Employee employee, string code, double clearance, bool active, string? note);

public class NotAFact
{
    public Company? company { get; set; }
}
