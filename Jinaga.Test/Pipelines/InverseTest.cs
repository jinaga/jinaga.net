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
            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                select office
            );

            var inverses = specification.ComputeInverses();
            inverses.Should().ContainSingle().Which.InversePipeline.ToDescriptiveString()
                .Should().Be(@"office: Corporate.Office {
    company: Corporate.Company = office P.company Corporate.Company
}
");
        }

        [Fact]
        public void Inverse_NegativeExistentialCondition()
        {
            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where !(
                    from officeClosure in facts.OfType<OfficeClosure>()
                    where officeClosure.office == office
                    select officeClosure
                ).Any()
                select office
            );

            var inverses = specification.ComputeInverses();
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
                    Office = office,
                    Names = facts.All(office, namesOfOffice)
                }
            );

            var inverses = specification.ComputeInverses();

            inverses.Select(i => i.InversePipeline.ToDescriptiveString()).Should().BeEquivalentTo(new [] {
@"office: Corporate.Office {
    company: Corporate.Company = office P.company Corporate.Company
}
",
@"name: Corporate.Office.Name {
    office: Corporate.Office = name P.office Corporate.Office
    company: Corporate.Company = office P.company Corporate.Company
}
"
            });
        }

        [Fact]
        public void Inverse_GeneratesCollectionIdentifiers()
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
                    Office = office,
                    Names = facts.All(office, namesOfOffice)
                }
            );

            var inverses = specification.ComputeInverses();

            inverses[0].InitialSubset.ToString().Should().Be("company");
            inverses[0].FinalSubset.ToString().Should().Be("office, company");
            inverses[0].CollectionIdentifiers.Should().BeEmpty();

            inverses[1].InitialSubset.ToString().Should().Be("company");
            inverses[1].FinalSubset.ToString().Should().Be("name, office, company");
            var collectionIdentifier = inverses[1].CollectionIdentifiers.Should().ContainSingle().Subject;
            collectionIdentifier.CollectionName.Should().Be("Names");
            // collectionIdentifier.Subset.ToString().Should().Be("name");
        }
    }
}
