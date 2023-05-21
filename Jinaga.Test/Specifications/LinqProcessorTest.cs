using FluentAssertions;
using Jinaga.Projections;
using Jinaga.Specifications;
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
}
