using FluentAssertions;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Specifications;
using System;
using System.Collections.Immutable;
using Xunit;
using static Jinaga.Specifications.LinqProcessor;

namespace Jinaga.Test.Specifications;
public class LinqProcessorTest
{
    [Fact]
    public void CanProcessFactSource()
    {
        var source = FactsOfType("Employee");

        var match = source.Matches.Should().ContainSingle().Subject;
        match.Unknown.Name.Should().Be("***");
        match.Unknown.Type.Should().Be("Employee");
        match.Conditions.Should().BeEmpty();

        var projection = source.Projection.Should().BeOfType<SimpleProjection>().Subject;
        projection.Tag.Should().Be("***");
    }

    [Fact]
    public void CanProcessComparison()
    {
        var left = new ReferenceContext(
            new Label("employee", "Employee"),
            ImmutableList.Create(new Role("company", "Company"))
        );
        var right = new ReferenceContext(
            new Label("company", "Company"),
            ImmutableList<Role>.Empty
        );
        var predicate = Compare(left, right);

        var condition = predicate.Conditions.Should().ContainSingle().Subject;
        var pathCondition = condition.Should().BeOfType<PathConditionContext>().Subject;
        pathCondition.Left.Should().Be(left);
        pathCondition.Right.Should().Be(right);
    }

    [Fact]
    public void ComparisonTypesMustMatch()
    {
        var left = new ReferenceContext(
            new Label("employee", "Employee"),
            ImmutableList.Create(new Role("company", "Company"))
        );
        var right = new ReferenceContext(
            new Label("company", "Company"),
            ImmutableList.Create(new Role("founder", "User"))
        );
        Action action = () => Compare(left, right);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Cannot join Company to User.");
    }
}
