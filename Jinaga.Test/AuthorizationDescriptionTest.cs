using System.Collections.Generic;
using System.Linq;
using Jinaga.Repository;

namespace Jinaga.Test;

/// <summary>
/// The text <see cref="AuthorizationRules.Describe"/> produces, rather than the policy that
/// text expresses. One set of rules has one description, so the order of the types in it
/// cannot depend on the globalization data of the machine that produced it.
/// </summary>
public class AuthorizationDescriptionTest
{
    [Fact]
    public void TheDescriptionListsTypesInOrdinalOrderOfTheirNames()
    {
        // `Blog.post` and `Blog.Site` differ in case, which is where a linguistic collation
        // and an ordinal one disagree. A linguistic collation orders them by letter, putting
        // `post` before `Site`; an ordinal one compares code points, in which `S` (U+0053)
        // precedes `p` (U+0070). Both names are read from the types themselves, and the
        // expected order is computed here rather than written out, so neither side of the
        // assertion is a copy of the other.
        string description = AuthorizationRules.Describe(a => a
            .Any<Model.LowercasePost>()
            .Any<Model.Site>());

        var expected = new[]
        {
            typeof(Model.LowercasePost).FactTypeName(),
            typeof(Model.Site).FactTypeName()
        }.OrderBy(name => name, StringComparer.Ordinal);

        TypesIn(description).Should().Equal(expected);
    }

    /// <summary>
    /// Reads the fact type names back out of a description, in the order it lists them.
    /// </summary>
    private static IEnumerable<string> TypesIn(string description) =>
        description.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("any ", StringComparison.Ordinal))
            .Select(line => line.Substring("any ".Length));
}
