using Jinaga.Extensions;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Test.Model;
using System.Collections.Immutable;
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

            // The specification is its own inverse, so that a watch started on a
            // company that is not yet saved delivers when that company lands.
            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo(new[] {
                    """
                    (company: Corporate.Company) {
                    } => company

                    """
                });
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
            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo(new[] {
                    """
                    (office: Corporate.Office) {
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => office

                    """,
                    // The self-inverse, for a given that is saved after the watch starts.
                    """
                    (company: Corporate.Company) {
                        office: Corporate.Office [
                            office->company: Corporate.Company = company
                        ]
                    } => office

                    """
                });
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

            // When the predecessor is created, it does not have a successor yet,
            // so the only inverse is the self-inverse. It carries the company to
            // an observer whose office was saved after the watch started.
            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo(new[] {
                    """
                    (office: Corporate.Office) {
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                    } => company

                    """
                });
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

                    """,
                    // The self-inverse, for a given that is saved after the watch starts.
                    """
                    (company: Corporate.Company) {
                        office: Corporate.Office [
                            office->company: Corporate.Company = company
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

                    """,
                    // The self-inverse, for a given that is saved after the watch starts.
                    """
                    (company: Corporate.Company) {
                        office: Corporate.Office [
                            office->company: Corporate.Company = company
                            !E {
                                officeClosure: Corporate.Office.Closure [
                                    officeClosure->office: Corporate.Office = office
                                ]
                            }
                        ]
                    } => office

                    """
                });

            inverses.Select(i => i.Operation).Should().BeEquivalentTo(new[] {
                InverseOperation.Add,
                InverseOperation.Remove,
                InverseOperation.Add
            });
        }

        [Fact]
        public void Inverse_TwoNegativeExistentialConditionsWithSameUnknownName()
        {
            var bookingsForAirlineDay = Given<AirlineDay>.Match(airlineDay =>
                airlineDay.Successors().OfType<Flight>(flight => flight.airlineDay)
                    .WhereNo((FlightCancellation x) => x.flight)
                    .SelectMany(flight => flight.Successors().OfType<Booking>(booking => booking.flight))
                    .WhereNo((Refund x) => x.booking)
            );

            var inverses = bookingsForAirlineDay.ComputeInverses();

            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo([
                """
                (booking: Skylane.Booking [
                    !E {
                        x2: Skylane.Refund [
                            x2->booking: Skylane.Booking = booking
                        ]
                    }
                ]) {
                    flight: Skylane.Flight [
                        flight = booking->flight: Skylane.Flight
                        !E {
                            x: Skylane.Flight.Cancellation [
                                x->flight: Skylane.Flight = flight
                            ]
                        }
                    ]
                    airlineDay: Skylane.Airline.Day [
                        airlineDay = flight->airlineDay: Skylane.Airline.Day
                    ]
                } => booking

                """,
                """
                (x: Skylane.Flight.Cancellation) {
                    flight: Skylane.Flight [
                        flight = x->flight: Skylane.Flight
                    ]
                    airlineDay: Skylane.Airline.Day [
                        airlineDay = flight->airlineDay: Skylane.Airline.Day
                    ]
                    booking: Skylane.Booking [
                        booking->flight: Skylane.Flight = flight
                        !E {
                            x2: Skylane.Refund [
                                x2->booking: Skylane.Booking = booking
                            ]
                        }
                    ]
                } => booking

                """,
                """
                (x2: Skylane.Refund) {
                    booking: Skylane.Booking [
                        booking = x2->booking: Skylane.Booking
                    ]
                    flight: Skylane.Flight [
                        flight = booking->flight: Skylane.Flight
                        !E {
                            x: Skylane.Flight.Cancellation [
                                x->flight: Skylane.Flight = flight
                            ]
                        }
                    ]
                    airlineDay: Skylane.Airline.Day [
                        airlineDay = flight->airlineDay: Skylane.Airline.Day
                    ]
                } => booking

                """,
                // The self-inverse, for a given that is saved after the watch starts.
                """
                (airlineDay: Skylane.Airline.Day) {
                    flight: Skylane.Flight [
                        flight->airlineDay: Skylane.Airline.Day = airlineDay
                        !E {
                            x: Skylane.Flight.Cancellation [
                                x->flight: Skylane.Flight = flight
                            ]
                        }
                    ]
                    booking: Skylane.Booking [
                        booking->flight: Skylane.Flight = flight
                        !E {
                            x2: Skylane.Refund [
                                x2->booking: Skylane.Booking = booking
                            ]
                        }
                    ]
                } => booking

                """
                ]);
            inverses.Select(i => i.Operation).Should().BeEquivalentTo(new[] {
                InverseOperation.Add,
                InverseOperation.Remove,
                InverseOperation.Remove,
                InverseOperation.Add
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

                    """,
                    // The self-inverse, for a given that is saved after the watch starts.
                    """
                    (company: Corporate.Company) {
                        office: Corporate.Office [
                            office->company: Corporate.Company = company
                            E {
                                officeClosure: Corporate.Office.Closure [
                                    officeClosure->office: Corporate.Office = office
                                ]
                            }
                        ]
                    } => office

                    """
                });

            inverses.Select(i => i.Operation).Should().BeEquivalentTo(new[] {
                InverseOperation.MaybeAdd,
                InverseOperation.Add
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

                    """,
                    // The self-inverse, for a given that is saved after the watch starts.
                    """
                    (company: Corporate.Company) {
                        office: Corporate.Office [
                            office->company: Corporate.Company = company
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
                    } => office

                    """
                });

            inverses.Select(i => i.Operation).Should().BeEquivalentTo(new[] {
                InverseOperation.Add,
                InverseOperation.Remove,
                InverseOperation.MaybeAdd,
                InverseOperation.Add
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

                    """,
                    // The self-inverse, for a given that is saved after the watch starts.
                    """
                    (company: Corporate.Company) {
                        office: Corporate.Office [
                            office->company: Corporate.Company = company
                        ]
                    } => {
                        Names = {
                            name: Corporate.Office.Name [
                                name->office: Corporate.Office = office
                            ]
                        } => name
                        Office = office
                    }

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

                    """,
                    // The self-inverse, for a given that is saved after the watch starts.
                    """
                    (office: Corporate.Office) {
                    } => {
                        Headcount = {
                            headcount: Corporate.Headcount [
                                headcount->office: Corporate.Office = office
                                !E {
                                    next: Corporate.Headcount [
                                        next->prior: Corporate.Headcount = headcount
                                    ]
                                }
                            ]
                        } => headcount
                        Managers = {
                            manager: Corporate.Manager [
                                manager->office: Corporate.Office = office
                                !E {
                                    termination: Corporate.Manager.Terminated [
                                        termination->Manager: Corporate.Manager = manager
                                    ]
                                }
                            ]
                        } => manager
                    }

                    """
                });
        }

        [Fact]
        public void Inverse_SelfInverseOfSingleGiven()
        {
            var specification = Given<Office>.Match((office, facts) =>
                from company in facts.OfType<Company>()
                where office.company == company
                select company
            );

            var inverses = specification.ComputeInverses();

            // The self-inverse re-reads the whole specification when the given
            // arrives, so its given and parent are the given subset, and its
            // result subset spans every label.
            var selfInverse = inverses.Should().ContainSingle().Subject;
            selfInverse.InverseSpecification.Should().BeSameAs(specification);
            selfInverse.Operation.Should().Be(InverseOperation.Add);
            selfInverse.GivenSubset.ToString().Should().Be("office");
            selfInverse.ParentSubset.ToString().Should().Be("office");
            selfInverse.ResultSubset.ToString().Should().Be("office, company");
            selfInverse.Path.Should().Be("");
        }

        [Fact]
        public void Inverse_NoSelfInverseOfTwoGivens()
        {
            var specification = Given<Company, City>.Match((company, city, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where office.city == city
                select office
            );

            var inverses = specification.ComputeInverses();

            // A self-inverse per combination of givens would not stay finite, so
            // a specification with two givens carries none.
            inverses.Should().NotContain(inverse => inverse.InverseSpecification == specification);
            inverses.Select(i => i.InverseSpecification.ToString().ReplaceLineEndings())
                .Should().BeEquivalentTo(new[] {
                    """
                    (office: Corporate.Office) {
                        company: Corporate.Company [
                            company = office->company: Corporate.Company
                        ]
                        city: Corporate.City [
                            city = office->city: Corporate.City
                        ]
                    } => office

                    """
                });
        }

        [Fact]
        public void Inverse_NoSelfInverseWhenGivenCarriesACondition()
        {
            // A given carrying an existential condition is the shape the inverter
            // produces, not one the LINQ processor accepts, so build it directly.
            var office = new Label("office", "Corporate.Office");
            var closureOfOffice = new Match(
                new Label("closure", "Corporate.Office.Closure"),
                ImmutableList.Create(new PathCondition(
                    ImmutableList.Create(new Role("office", "Corporate.Office")),
                    "office",
                    ImmutableList<Role>.Empty)),
                ImmutableList<ExistentialCondition>.Empty);
            var nameOfOffice = new Match(
                new Label("name", "Corporate.Office.Name"),
                ImmutableList.Create(new PathCondition(
                    ImmutableList.Create(new Role("office", "Corporate.Office")),
                    "office",
                    ImmutableList<Role>.Empty)),
                ImmutableList<ExistentialCondition>.Empty);
            var specification = new Specification(
                ImmutableList.Create(new SpecificationGiven(
                    office,
                    ImmutableList.Create(new ExistentialCondition(
                        false,
                        ImmutableList.Create(closureOfOffice))))),
                ImmutableList.Create(nameOfOffice),
                new SimpleProjection("name", typeof(OfficeName)));

            var inverses = specification.ComputeInverses();

            // The given's own condition is not satisfied by its arrival, so
            // re-reading the specification then could deliver rows that the
            // condition excludes.
            inverses.Should().NotContain(inverse => inverse.InverseSpecification == specification);
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
