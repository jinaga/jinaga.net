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
    office
}");
        }

        [Fact]
        public void Inverse_NegativeExistentialCondition()
        {
            var pipeline = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where !(
                    from officeClosure in facts.OfType<OfficeClosure>()
                    where officeClosure.office == office
                    select officeClosure
                ).Any()
                select office
            ).Pipeline;

            var inverses = pipeline.ComputeInverses();
            inverses.Select(i => i.InversePipeline.ToDescriptiveString()).Should().BeEquivalentTo(new [] {
@"office: Corporate.Office {
    company: Corporate.Company = office P.company Corporate.Company
    office
}",
@"<t1>: Corporate.Office.Closure {
    office: Corporate.Office = <t1> P.office Corporate.Office
    company: Corporate.Company = office P.company Corporate.Company
    office
}"
            });
        }
    }
}
