using System.Linq;
using FluentAssertions;
using Xunit;

namespace Jinaga.Test.Pipelines
{
    public class InverseTest
    {
        [Fact]
        public void Inverse_SuccessorStep()
        {
            var pipeline = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                select office
            ).Pipeline;

            var inverses = pipeline.ComputeInverses();
            inverses.Should().ContainSingle().Which.InversePipeline.ToDescriptiveString()
                .Should().Be(@"office: Corporate.Office {
    company: Corporate.Company = office P.company Corporate.Company
    {
        affected = company
        added = office
    }
}");
        }
    }
}
