using System.Collections.Immutable;
using System.Linq;
using Jinaga.Pipelines;
using Jinaga.Projections;

namespace Jinaga.Test.Specifications;

/// <summary>
/// The split relies on its input naming only labels that are in scope, declaring none twice or
/// with the reserved prefix, and projecting only labels it declares. The model builder produces
/// such specifications by construction, so these build specifications directly, as one loaded
/// from elsewhere would arrive.
/// </summary>
public class WellFormedSpecificationTest
{
    private static readonly Role OfficeRole = new("office", "Office");
    private static readonly Role CompanyRole = new("company", "Company");

    [Fact]
    public void FindsNothingWrongWithAWellFormedSpecification()
    {
        var specification = Spec(
            Given("p1", "Employee"),
            new[] { MatchOf("u1", "Office", Walk("p1", OfficeRole)) },
            "u1");

        WellFormedSpecification.Errors(specification).Should().BeEmpty();
    }

    [Fact]
    public void ReportsAProjectionThatNamesAnUndefinedLabel()
    {
        var specification = Spec(
            Given("p1", "Employee"),
            new[] { MatchOf("u1", "Office", Walk("p1", OfficeRole)) },
            "nosuchlabel");

        WellFormedSpecification.Errors(specification).Should().Equal(
            "The projection names the label 'nosuchlabel', which has not been defined.");
    }

    [Fact]
    public void ReportsAPathConditionThatJoinsAnUndefinedLabel()
    {
        var specification = Spec(
            Given("p1", "Employee"),
            new[] { MatchOf("u1", "Office", Walk("nosuchlabel", OfficeRole)) },
            "u1");

        WellFormedSpecification.Errors(specification).Should().Equal(
            "The label 'nosuchlabel' has not been defined.");
    }

    [Fact]
    public void ReportsAMatchThatJoinsItsOwnUnknown()
    {
        var specification = Spec(
            Given("p1", "Employee"),
            new[] { MatchOf("u1", "Office", Walk("u1", OfficeRole)) },
            "u1");

        WellFormedSpecification.Errors(specification).Should().Equal(
            "The label 'u1' has not been defined.");
    }

    [Fact]
    public void ReportsAMatchThatDeclaresALabelAlreadyInScope()
    {
        var specification = Spec(
            Given("p1", "Employee"),
            new[] { MatchOf("p1", "Office", Walk("p1", OfficeRole)) },
            "p1");

        WellFormedSpecification.Errors(specification).Should().Equal(
            "The name 'p1' has already been used.");
    }

    [Fact]
    public void ReservesTheLabelsTheSplitNamesForItself()
    {
        // In a given, in a match, and in a match inside an existential condition.
        var specification = Spec(
            Given("__p", "Employee"),
            new[]
            {
                MatchOf("__s0", "Office", Walk("__p", OfficeRole)),
                MatchOf("u1", "Company",
                    new[] { Walk("__s0", CompanyRole) },
                    Exists(false, MatchOf("__e", "Office", Walk("u1")))),
            },
            "u1");

        WellFormedSpecification.Errors(specification).Should().Equal(
            "The name '__p' is reserved: labels that begin with '__' belong to the split.",
            "The name '__s0' is reserved: labels that begin with '__' belong to the split.",
            "The name '__e' is reserved: labels that begin with '__' belong to the split.");
    }

    [Fact]
    public void LetsSiblingExistentialConditionsReuseAName()
    {
        // Scope is lexical: the label a nested match declares does not outlive its condition.
        var specification = Spec(
            Given("p1", "Employee"),
            new[]
            {
                MatchOf("u1", "Office",
                    new[] { Walk("p1", OfficeRole) },
                    Exists(false, MatchOf("e", "Office", Walk("u1"))),
                    Exists(false, MatchOf("e", "Office", Walk("u1")))),
            },
            "u1");

        WellFormedSpecification.Errors(specification).Should().BeEmpty();
    }

    [Fact]
    public void RefusesToVouchForASpecificationThatIsNotWellFormed()
    {
        var specification = Spec(
            Given("p1", "Employee"),
            new[] { MatchOf("u1", "Office", Walk("p1", OfficeRole)) },
            "nosuchlabel");

        var checking = () => WellFormedSpecification.Check(specification, "The specification");

        checking.Should().Throw<InvalidOperationException>().WithMessage(
            "The specification is not valid. The projection names the label 'nosuchlabel', which has not been defined.");
    }

    private static SpecificationGiven Given(string name, string type) =>
        new(new Label(name, type), ImmutableList<ExistentialCondition>.Empty);

    private static Match MatchOf(string name, string type, params PathCondition[] paths) =>
        MatchOf(name, type, paths, Array.Empty<ExistentialCondition>());

    private static Match MatchOf(string name, string type, PathCondition[] paths, params ExistentialCondition[] existentials) =>
        new(new Label(name, type), paths.ToImmutableList(), existentials.ToImmutableList());

    private static PathCondition Walk(string labelRight, params Role[] rolesRight) =>
        new(ImmutableList<Role>.Empty, labelRight, rolesRight.ToImmutableList());

    private static ExistentialCondition Exists(bool exists, params Match[] matches) =>
        new(exists, matches.ToImmutableList());

    private static Specification Spec(SpecificationGiven given, Match[] matches, string projected) =>
        new(ImmutableList.Create(given), matches.ToImmutableList(), new SimpleProjection(projected, typeof(object)));
}
