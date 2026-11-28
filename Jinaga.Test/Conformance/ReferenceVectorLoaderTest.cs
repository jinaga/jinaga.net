using System;
using System.Collections.Immutable;
using System.Text.Json;

namespace Jinaga.Test.Conformance;

/// <summary>
/// The reference vector loader's field check, which is what makes a re-pin that states
/// something new fail loudly instead of passing unnoticed.
///
/// <para>
/// A skeleton is read field by field and has no second statement of itself to disagree with,
/// unlike a specification, which is also stated as <c>text</c>. So a field jinaga.js adds
/// inside an edge or a fact would otherwise be dropped in silence, and the pin would claim
/// agreement the suite never checked. These feed synthetic vectors through
/// <see cref="ReferenceVector.CheckFields"/>, because the pinned vectors are well formed by
/// construction and would never reach a refusal.
/// </para>
/// </summary>
public class ReferenceVectorLoaderTest
{
    [Fact]
    public void AcceptsAVectorWhoseFieldsAreAllRead()
    {
        var checking = () => Check(Vector());

        checking.Should().NotThrow();
    }

    [Fact]
    public void RefusesAFieldOfTheVectorItDoesNotRead()
    {
        var checking = () => Check(Vector(extra: @", ""surprise"": 1"));

        checking.Should().Throw<NotSupportedException>().WithMessage("*'surprise'*");
    }

    [Fact]
    public void RefusesAFieldOfASkeletonItDoesNotRead()
    {
        var checking = () => Check(Vector(skeletonExtra: @", ""version"": 2"));

        checking.Should().Throw<NotSupportedException>().WithMessage("*'version'*");
    }

    /// <summary>
    /// A field inside one of the skeleton's own lists. This is the level the check did not
    /// reach before: each of these passed in silence, and the skeleton was then read field by
    /// field as though the vector had said nothing more.
    /// </summary>
    [Theory]
    [InlineData("facts", @"{ ""factType"": ""T"", ""factIndex"": 1, ""nickname"": ""n"" }", "nickname")]
    [InlineData("inputs", @"{ ""factIndex"": 1, ""inputIndex"": 0, ""ordinal"": 2 }", "ordinal")]
    [InlineData("edges", @"{ ""edgeIndex"": 1, ""predecessorFactIndex"": 1, ""successorFactIndex"": 2,
        ""roleName"": ""r"", ""direction"": ""up"" }", "direction")]
    [InlineData("outputs", @"{ ""factIndex"": 2, ""label"": ""u1"" }", "label")]
    [InlineData("notExistsConditions",
        @"{ ""edges"": [], ""notExistsConditions"": [], ""exists"": false }", "exists")]
    public void RefusesAFieldInsideASkeletonListItDoesNotRead(string list, string element, string named)
    {
        var checking = () => Check(Vector(lists: Lists().SetItem(list, element)));

        checking.Should().Throw<NotSupportedException>()
            .WithMessage($"*'{named}'*")
            .WithMessage("*which the reference vector loader does not read*");
    }

    /// <summary>
    /// A field one level down inside a nested not-exists condition, so the recursion is what
    /// catches it rather than the first pass over the condition list.
    /// </summary>
    [Fact]
    public void RefusesAFieldInsideANestedNotExistsCondition()
    {
        var nested = $@"{{
            ""edges"": [],
            ""notExistsConditions"": [
                {{
                    ""edges"": [{{ ""edgeIndex"": 1, ""predecessorFactIndex"": 1,
                        ""successorFactIndex"": 2, ""roleName"": ""r"", ""depth"": 2 }}],
                    ""notExistsConditions"": []
                }}
            ]
        }}";

        var checking = () => Check(Vector(lists: Lists().SetItem("notExistsConditions", nested)));

        checking.Should().Throw<NotSupportedException>().WithMessage("*'depth'*");
    }

    [Fact]
    public void RefusesAKindItDoesNotRead()
    {
        var checking = () => ReferenceVector.CheckFields(
            "skeletons", "synthetic", JsonDocument.Parse(Vector()).RootElement);

        checking.Should().Throw<NotSupportedException>().WithMessage("*'skeletons'*");
    }

    private static void Check(string json) =>
        ReferenceVector.CheckFields("feeds", "synthetic", JsonDocument.Parse(json).RootElement);

    /// <summary>
    /// One element for each of the skeleton's lists, every field of which the loader reads. A
    /// case above replaces one of them, so exactly one list carries the field under test and
    /// the refusal it draws can only have come from there.
    /// </summary>
    private static ImmutableDictionary<string, string> Lists() =>
        ImmutableDictionary<string, string>.Empty
            .Add("facts", @"{ ""factType"": ""T"", ""factIndex"": 1 }")
            .Add("inputs", @"{ ""factIndex"": 1, ""inputIndex"": 0 }")
            .Add("edges", @"{ ""edgeIndex"": 1, ""predecessorFactIndex"": 1,
                ""successorFactIndex"": 2, ""roleName"": ""r"" }")
            .Add("notExistsConditions", @"{ ""edges"": [], ""notExistsConditions"": [] }")
            .Add("outputs", @"{ ""factIndex"": 2 }");

    /// <summary>A vector carrying one feed with one skeleton.</summary>
    private static string Vector(
        ImmutableDictionary<string, string>? lists = null,
        string skeletonExtra = "",
        string extra = "")
    {
        var l = lists ?? Lists();
        return $@"{{
            ""name"": ""synthetic"",
            ""text"": ""t"",
            ""specification"": {Specification},
            ""feeds"": [
                {{
                    ""text"": ""t"",
                    ""specification"": {Specification},
                    ""skeleton"": {{
                        ""facts"": [{l["facts"]}],
                        ""inputs"": [{l["inputs"]}],
                        ""edges"": [{l["edges"]}],
                        ""notExistsConditions"": [{l["notExistsConditions"]}],
                        ""outputs"": [{l["outputs"]}]{skeletonExtra}
                    }}
                }}
            ]{extra}
        }}";
    }

    /// <summary>
    /// The check stops at a specification's interior, so this one only has to parse. It is
    /// never read into a <c>Specification</c> here.
    /// </summary>
    private const string Specification = @"{ ""given"": [], ""matches"": [], ""projection"": {} }";
}
