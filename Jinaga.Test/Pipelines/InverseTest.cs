using FluentAssertions;
using Jinaga.Test.Model;
using System;
using System.Linq;
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
            inverses.Should().ContainSingle().Which.InverseSpecification.ToDescriptiveString(5)
                .Should().Be(Indented(5, @"
                    (office: Corporate.Office) {
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => office
                    "));
        }

        [Fact]
        public void Inverse_PredecessorOfSuccessor()
        {
            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                from city in facts.OfType<City>()
                where city == office.city
                select city
            );

            var inverses = specification.ComputeInverses();
            inverses.Select(i => i.InverseSpecification.ToDescriptiveString(4)).Should().BeEquivalentTo(new string[] { Indented(4, @"
                (office: Corporate.Office) {
                    company: Corporate.Company [
                        company = office->company: Corporate.Company
                    ]
                    city: Corporate.City [
                        city = office->city: Corporate.City
                    ]
                } => city
                ") });
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
            inverses.Select(i => i.InverseSpecification.ToDescriptiveString(4)).Should().BeEquivalentTo(new [] {Indented(4, @"
                (office: Corporate.Office) {
                    company: Corporate.Company [
                        company = office->company: Corporate.Company
                    ]
                } => office
                "), Indented(4, @"
                (officeClosure: Corporate.Office.Closure) {
                    office: Corporate.Office [
                        office = officeClosure->office: Corporate.Office
                    ]
                    company: Corporate.Company [
                        company = office->company: Corporate.Company
                    ]
                } => office
                ")
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
                    Names = facts.Observable(office, namesOfOffice)
                }
            );

            var inverses = specification.ComputeInverses();

            inverses.Select(i => i.InverseSpecification.ToDescriptiveString(4)).Should().BeEquivalentTo(new [] {Indented(4,@"
                (office: Corporate.Office) {
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
                "), Indented(4,@"
                (name: Corporate.Office.Name) {
                    office: Corporate.Office [
                        office = name->office: Corporate.Office
                    ]
                    company: Corporate.Company [
                        company = office->company: Corporate.Company
                    ]
                } => name
                ")
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
                    Names = facts.Observable(office, namesOfOffice)
                }
            );

            var inverses = specification.ComputeInverses();

            inverses[0].GivenSubset.ToString().Should().Be("company");
            inverses[0].ResultSubset.ToString().Should().Be("company, office");
            inverses[0].CollectionIdentifiers.Should().BeEmpty();

            inverses[1].GivenSubset.ToString().Should().Be("company");
            inverses[1].ResultSubset.ToString().Should().Be("company, office, name");
            var collectionIdentifier = inverses[1].CollectionIdentifiers.Should().ContainSingle().Subject;
            collectionIdentifier.CollectionName.Should().Be("Names");
            // collectionIdentifier.Subset.ToString().Should().Be("name");
        }

        private string Indented(int depth, string str)
        {
            Assert.Equal("\r\n", str.Substring(0, 2));
            Assert.Equal(new String(' ', 4 * depth), str.Substring(str.Length - 4 * depth));
            return str.Substring(2, str.Length - 4 * depth - 2).Replace("\r", "");
        }
    }
}
