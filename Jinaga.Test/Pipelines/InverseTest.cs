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
}
");
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
}
",
@"officeClosure: Corporate.Office.Closure {
    office: Corporate.Office = officeClosure P.office Corporate.Office
    company: Corporate.Company = office P.company Corporate.Company
}
"
            });
        }

        [Fact]
        public void Inverse_OfNestedProjection()
        {
            var namesOfOffice = Given<Office>.Match((office, facts) =>
                from name in facts.OfType<OfficeName>()
                where name.office == office
                select name
            );

            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                select new
                {
                    City = office.city,
                    Names = facts.All(office, namesOfOffice)
                }
            );

            var inverses = specification.ComputeInverses();

            var lines = inverses
                .Select(i => i.InversePipeline.ToDescriptiveString())
                .ToArray();
            string.Join("\r\n", lines).Should().Be(@"office: Corporate.Office {
    company: Corporate.Company = office P.company Corporate.Company
}

city: Corporate.City {
    office: Corporate.Office = city S.city Corporate.Office
    company: Corporate.Company = office P.company Corporate.Company
}

name: Corporate.Office.Name {
    office: Corporate.Office = name P.office Corporate.Office
    company: Corporate.Company = office P.company Corporate.Company
}
");
        }
    }
}
