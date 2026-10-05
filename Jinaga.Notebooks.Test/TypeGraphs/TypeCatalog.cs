using Jinaga.Notebooks.Test.Models;

namespace Jinaga.Notebooks.Test.TypeGraphs;

public static class TypeCatalog
{
    public static readonly Type[] All =
    {
        typeof(Company),
        typeof(Office),
        typeof(OfficeName),
        typeof(Employee),
        typeof(Assignment),
        typeof(Transfer),
        typeof(Badge),
        typeof(NotAFact)
    };

    public static IEnumerable<object[]> Orderings()
    {
        yield return new object[] { Enumerable.Reverse(All).ToArray() };
        yield return new object[] { All.Skip(3).Concat(All.Take(3)).ToArray() };
        yield return new object[] { All.OrderBy(type => type.Name).ToArray() };
    }
}
