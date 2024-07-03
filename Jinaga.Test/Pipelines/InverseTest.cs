using Jinaga.Pipelines;
using Jinaga.Test.Model;
using System.Linq;

namespace Jinaga.Test.Pipelines
{
    public class InverseTest
    {
        [Fact]
        public void Inverse_Identity()
        {
            var specification = Given<Company>.Match(company => company);

            var inverses = specification.ComputeInverses();
            inverses.Should().BeEmpty();
        }

        [Fact]
        public void Inverse_SuccessorStep()
        {
            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                select office
            );

            var inverses = specification.ComputeInverses();
            inverses.Should().ContainSingle().Which
                .InverseSpecification.ToString().ReplaceLineEndings().Should().Be(
                    """
                    (office: Corporate.Office) {
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => office

                    """
                    );
        }

        [Fact]
        public void Inverse_PredecessorStep()
        {
            var specification = Given<Office>.Match((office, facts) =>
                from company in facts.OfType<Company>()
                where office.company == company
                select company
            );

            var inverses = specification.ComputeInverses();

            // When the predecessor is created, it does not have a successor yet.
            inverses.Should().BeEmpty();
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

            // Expect the inverse to filter out the specification starting from the other predecessor.
            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo(new string[] {
                    """
                    (office: Corporate.Office) {
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                        city: Corporate.City [
                            city = office->city: Corporate.City
                        ]
                    } => city

                    """
                    });
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
            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo(new [] {
                    """
                    (office: Corporate.Office [
                        !E {
                            officeClosure: Corporate.Office.Closure [
                                officeClosure->office: Corporate.Office = office
                            ]
                        }
                    ]) {
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => office

                    """,
                    """
                    (officeClosure: Corporate.Office.Closure) {
                        office: Corporate.Office [
                            office = officeClosure->office: Corporate.Office
                        ]
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => office

                    """
                });

            inverses.Select(i => i.Operation).Should().BeEquivalentTo(new[] {
                InverseOperation.Add,
                InverseOperation.Remove
            });
        }

        [Fact]
        public void Inverse_PositiveExistentialCondition()
        {
            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where (
                    from officeClosure in facts.OfType<OfficeClosure>()
                    where officeClosure.office == office
                    select officeClosure
                ).Any()
                select office
            );

            var inverses = specification.ComputeInverses();

            // The second inverse is not satisfiable because the OfficeClosed
            // fact will not yet exist.
            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo(new[] {
                    """
                    (officeClosure: Corporate.Office.Closure) {
                        office: Corporate.Office [
                            office = officeClosure->office: Corporate.Office
                        ]
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => office

                    """
                });

            inverses.Select(i => i.Operation).Should().BeEquivalentTo(new[] {
                InverseOperation.MaybeAdd
            });
        }

        [Fact]
        public void Inverse_RestorePattern()
        {
            var specification = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where !(
                    from officeClosure in facts.OfType<OfficeClosure>()
                    where officeClosure.office == office
                    where !(
                        from officeReopening in facts.OfType<OfficeReopening>()
                        where officeReopening.officeClosure == officeClosure
                        select officeReopening
                    ).Any()
                    select officeClosure
                ).Any()
                select office
            );

            var inverses = specification.ComputeInverses();

            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo(new[] {
                    """
                    (office: Corporate.Office [
                        !E {
                            officeClosure: Corporate.Office.Closure [
                                officeClosure->office: Corporate.Office = office
                                !E {
                                    officeReopening: Corporate.Office.Reopening [
                                        officeReopening->officeClosure: Corporate.Office.Closure = officeClosure
                                    ]
                                }
                            ]
                        }
                    ]) {
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => office

                    """,
                    """
                    (officeClosure: Corporate.Office.Closure [
                        !E {
                            officeReopening: Corporate.Office.Reopening [
                                officeReopening->officeClosure: Corporate.Office.Closure = officeClosure
                            ]
                        }
                    ]) {
                        office: Corporate.Office [
                            office = officeClosure->office: Corporate.Office
                        ]
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => office
                    
                    """,
                    """
                    (officeReopening: Corporate.Office.Reopening) {
                        officeClosure: Corporate.Office.Closure [
                            officeClosure = officeReopening->officeClosure: Corporate.Office.Closure
                        ]
                        office: Corporate.Office [
                            office = officeClosure->office: Corporate.Office
                            !E {
                                officeClosure: Corporate.Office.Closure [
                                    officeClosure->office: Corporate.Office = office
                                    !E {
                                        officeReopening: Corporate.Office.Reopening [
                                            officeReopening->officeClosure: Corporate.Office.Closure = officeClosure
                                        ]
                                    }
                                ]
                            }
                        ]
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => office

                    """
                });

            inverses.Select(i => i.Operation).Should().BeEquivalentTo(new[] {
                InverseOperation.Add,
                InverseOperation.Remove,
                InverseOperation.MaybeAdd
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

            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo(new [] {
                    """
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

                    """,
                    """
                    (name: Corporate.Office.Name) {
                        office: Corporate.Office [
                            office = name->office: Corporate.Office
                        ]
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => name

                    """
                });
        }

        [Fact]
        public void Inverse_ProjectionFromIdentity()
        {
            var specification = Given<Office>.Select((office, facts) => new
            {
                Managers = office.Managers,
                Headcount = office.Headcount
            });

            var inverses = specification.ComputeInverses();

            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo(new[] {
                    """
                    (headcount: Corporate.Headcount [
                        !E {
                            next: Corporate.Headcount [
                                next->prior: Corporate.Headcount = headcount
                            ]
                        }
                    ]) {
                        office: Corporate.Office [
                            office = headcount->office: Corporate.Office
                        ]
                    } => headcount

                    """,
                    """
                    (next: Corporate.Headcount) {
                        headcount: Corporate.Headcount [
                            headcount = next->prior: Corporate.Headcount
                        ]
                        office: Corporate.Office [
                            office = headcount->office: Corporate.Office
                        ]
                    } => headcount

                    """,
                    """
                    (manager: Corporate.Manager [
                        !E {
                            termination: Corporate.Manager.Terminated [
                                termination->Manager: Corporate.Manager = manager
                            ]
                        }
                    ]) {
                        office: Corporate.Office [
                            office = manager->office: Corporate.Office
                        ]
                    } => manager

                    """,
                    """
                    (termination: Corporate.Manager.Terminated) {
                        manager: Corporate.Manager [
                            manager = termination->Manager: Corporate.Manager
                        ]
                        office: Corporate.Office [
                            office = manager->office: Corporate.Office
                        ]
                    } => manager

                    """
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
            inverses[0].Path.Should().Be("");

            inverses[1].GivenSubset.ToString().Should().Be("company");
            inverses[1].ResultSubset.ToString().Should().Be("company, office, name");
            var collectionIdentifier = inverses[1].Path.Should().Be("Names");
        }
    }
}
