using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Jinaga.Pipelines;
using Jinaga.Test.Model;
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
            inverses.Should().ContainSingle().Which.InverseSpecification.ToDescriptiveString()
                .Should().Be(@"(office: Corporate.Office) {
    company: Corporate.Company [
        company = office->company: Corporate.Company
    ]
} => office
".Replace("\r", ""));
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
            inverses.Select(i => i.InverseSpecification.ToDescriptiveString()).Should().BeEquivalentTo(new [] {
@"(office: Corporate.Office) {
    company: Corporate.Company [
        company = office->company: Corporate.Company
    ]
} => office
".Replace("\r", ""),
@"(officeClosure: Corporate.Office.Closure) {
    office: Corporate.Office [
        office = officeClosure->office: Corporate.Office
    ]
    company: Corporate.Company [
        company = office->company: Corporate.Company
    ]
} => office
".Replace("\r", "")
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

            inverses.Select(i => i.InverseSpecification.ToDescriptiveString()).Should().BeEquivalentTo(new [] {
@"(office: Corporate.Office) {
    company: Corporate.Company [
        company = office->company: Corporate.Company
    ]
} => {
    Names = {
        name: Corporate.Office.Name [
            name->office: Corporate.Office = office
        ]
    } => name
    Office = office
}
".Replace("\r", ""),
@"(name: Corporate.Office.Name) {
    office: Corporate.Office [
        office = name->office: Corporate.Office
    ]
    company: Corporate.Company [
        company = office->company: Corporate.Company
    ]
} => name
".Replace("\r", "")
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
            inverses[0].FinalSubset.ToString().Should().Be("company, office");
            inverses[0].CollectionIdentifiers.Should().BeEmpty();

            inverses[1].InitialSubset.ToString().Should().Be("company");
            inverses[1].FinalSubset.ToString().Should().Be("company, office, name");
            var collectionIdentifier = inverses[1].CollectionIdentifiers.Should().ContainSingle().Subject;
            collectionIdentifier.CollectionName.Should().Be("Names");
            // collectionIdentifier.Subset.ToString().Should().Be("name");
        }
    }
}
