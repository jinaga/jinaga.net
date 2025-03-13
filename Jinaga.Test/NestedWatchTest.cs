using Jinaga.Observers;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using System.Linq;

namespace Jinaga.Test
{
    public class NestedWatchTest
    {
        private readonly JinagaClient j;
        private readonly FakeOfficeRepository officeRepository;

        public NestedWatchTest()
        {
            j = JinagaTest.Create();
            officeRepository = new FakeOfficeRepository();
        }

        [Fact]
        public async Task NestedWatch_NoResults()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = await WhenWatchOffices(company);

            officeObserver.Stop();

            officeRepository.Offices.Should().BeEmpty();
        }

        [Fact]
        public async Task NestedWatch_NoResultsInlineObservable()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = await WhenWatchOfficesInlineObservable(company);

            officeObserver.Stop();

            officeRepository.Offices.Should().BeEmpty();
        }

        [Fact]
        public async Task NestedWatch_NoResultsInlineObservableWithFields()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = await WhenWatchOfficesInlineObservableWithFields(company);

            officeObserver.Stop();

            officeRepository.Offices.Should().BeEmpty();
        }

        [Fact]
        public async Task NestedWatch_NoResultsInlineQueryable()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = await WhenWatchOfficesInlineQueryable(company);

            officeObserver.Stop();

            officeRepository.Offices.Should().BeEmpty();
        }

        [Fact]
        public async Task NestedWatch_OfficeAlreadyExists()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOffices(company);

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = ""
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeAlreadyExistsInlineObservable()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesInlineObservable(company);

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = ""
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeAlreadyExistsInlineObservableWithFields()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesInlineObservableWithFields(company);

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = ""
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeAlreadyExistsInlineQueryable()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesInlineQueryable(company);

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = ""
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeCloses()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOffices(company);

            await j.Fact(new OfficeClosure(newOffice, DateTime.UtcNow));

            officeRepository.Offices.Should().BeEmpty();

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeClosesInlineObservable()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesInlineObservable(company);

            await j.Fact(new OfficeClosure(newOffice, DateTime.UtcNow));

            officeRepository.Offices.Should().BeEmpty();

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeClosesInlineObservableWithFields()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesInlineObservableWithFields(company);

            await j.Fact(new OfficeClosure(newOffice, DateTime.UtcNow));

            officeRepository.Offices.Should().BeEmpty();

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeClosesInlineQueryable()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesInlineQueryable(company);

            await j.Fact(new OfficeClosure(newOffice, DateTime.UtcNow));

            officeRepository.Offices.Should().BeEmpty();

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeNameAdded()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOffices(company);

            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeNameAddedInlineObservable()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesInlineObservable(company);

            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeNameAddedInlineObservableWithFields()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesInlineObservableWithFields(company);

            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeNameAddedInlineQueryable()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesInlineQueryable(company);

            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = ""
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeNameAlreadySet()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            var officeObserver = await WhenWatchOffices(company);

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeNameAlreadySetInlineObservable()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            var officeObserver = await WhenWatchOfficesInlineObservable(company);

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeNameAlreadySetInlineObservableWithFields()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            var officeObserver = await WhenWatchOfficesInlineObservableWithFields(company);

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeNameAlreadySetInlineQueryable()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            var officeObserver = await WhenWatchOfficesInlineQueryable(company);

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeAndNameAdded()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = await WhenWatchOffices(company);

            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeAndNameAddedInlineObservable()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = await WhenWatchOfficesInlineObservable(company);

            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeAndNameAddedInlineObservableWithFields()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = await WhenWatchOfficesInlineObservableWithFields(company);

            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeAndNameAddedInlineQueryable()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = await WhenWatchOfficesInlineQueryable(company);

            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = ""
                }
            });

            officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_ManagerAndNameAlreadyExist()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var manager42 = await j.Fact(new Manager(newOffice, 42));
            var michael = await j.Fact(new ManagerName(manager42, "Michael", new ManagerName[0]));

            var managerObserver = await WhenWatchManagement(company);

            officeRepository.Managers.Should().BeEquivalentTo(new ManagerRow[]
            {
                new ManagerRow
                {
                    ManagerId = 1,
                    OfficeId = 1,
                    EmployeeNumber = 42,
                    Name = "Michael"
                }
            });
        }

        [Fact]
        public async Task NestedWatch_ManagerTerminated()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var manager42 = await j.Fact(new Manager(newOffice, 42));
            var michael = await j.Fact(new ManagerName(manager42, "Michael", new ManagerName[0]));

            var managerObserver = await WhenWatchManagement(company);

            await j.Fact(new ManagerTerminated(manager42, DateTime.UtcNow));

            officeRepository.Managers.Should().BeEmpty();
        }

        [Fact]
        public async Task NestedWatch_ManagerAndNameAdded()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var managerObserver = await WhenWatchManagement(company);

            var manager42 = await j.Fact(new Manager(newOffice, 42));
            var michael = await j.Fact(new ManagerName(manager42, "Michael", new ManagerName[0]));

            officeRepository.Managers.Should().BeEquivalentTo(new ManagerRow[]
            {
                new ManagerRow
                {
                    ManagerId = 1,
                    OfficeId = 1,
                    EmployeeNumber = 42,
                    Name = "Michael"
                }
            });
        }

        private async Task<IObserver> WhenWatchOffices(Company company)
        {
            var officeObserver = j.Watch(officesInCompany, company,
                async projection =>
                {
                    int officeId = await officeRepository.InsertOffice(projection.Office.city.name);
                    projection.Names.OnAdded(async name =>
                    {
                        await officeRepository.UpdateOfficeName(officeId, name.value);
                    });
                    projection.Headcounts.OnAdded(async headcount =>
                    {
                        await officeRepository.UpdateOfficeHeadcount(officeId, headcount.value);
                    });

                    return async () =>
                    {
                        await officeRepository.DeleteOffice(officeId);
                    };
                });
            await officeObserver.Loaded;
            return officeObserver;
        }

        private async Task<IObserver> WhenWatchOfficesInlineObservable(Company company)
        {
            var officeObserver = j.Watch(officesInCompanyInlineObservable, company,
                async projection =>
                {
                    int officeId = await officeRepository.InsertOffice(projection.Office.city.name);
                    projection.Names.OnAdded(async name =>
                    {
                        await officeRepository.UpdateOfficeName(officeId, name.value);
                    });
                    projection.Headcounts.OnAdded(async headcount =>
                    {
                        await officeRepository.UpdateOfficeHeadcount(officeId, headcount.value);
                    });

                    return async () =>
                    {
                        await officeRepository.DeleteOffice(officeId);
                    };
                });
            await officeObserver.Loaded;
            return officeObserver;
        }

        private async Task<IObserver> WhenWatchOfficesInlineObservableWithFields(Company company)
        {
            var officeObserver = j.Watch(officesInCompanyInlineObservableWithFields, company,
                async projection =>
                {
                    int officeId = await officeRepository.InsertOffice(projection.Office.city.name);
                    projection.Names.OnAdded(async name =>
                    {
                        await officeRepository.UpdateOfficeName(officeId, name);
                    });
                    projection.Headcounts.OnAdded(async headcount =>
                    {
                        await officeRepository.UpdateOfficeHeadcount(officeId, headcount);
                    });

                    return async () =>
                    {
                        await officeRepository.DeleteOffice(officeId);
                    };
                });
            await officeObserver.Loaded;
            return officeObserver;
        }

        private async Task<IObserver> WhenWatchOfficesInlineQueryable(Company company)
        {
            var officeObserver = j.Watch(officesInCompanyInlineQueryableWithFields, company,
                async projection =>
                {
                    int officeId = await officeRepository.InsertOffice(projection.Office.city.name);
                    if (projection.Names.Any())
                    {
                        await officeRepository.UpdateOfficeName(officeId, projection.Names.Last());
                    }
                    if (projection.Headcounts.Any())
                    {
                        await officeRepository.UpdateOfficeHeadcount(officeId, projection.Headcounts.Last());
                    }

                    return async () =>
                    {
                        await officeRepository.DeleteOffice(officeId);
                    };
                });
            await officeObserver.Loaded;
            return officeObserver;
        }

        private async Task<IObserver> WhenWatchManagement(Company company)
        {
            var managementObserver = j.Watch(managersInCompany, company,
                async projection =>
                {
                    int officeId = await officeRepository.InsertOffice(projection.Office.city.name);
                    projection.Managers.OnAdded(async managerProjection =>
                    {
                        int managerId = await officeRepository.InsertManager(officeId, managerProjection.Manager.employeeNumber);
                        managerProjection.Names.OnAdded(async name =>
                        {
                            await officeRepository.UpdateManagerName(managerId, name.value);
                        });

                        return async () =>
                        {
                            await officeRepository.DeleteManager(managerId);
                        };
                    });

                    return async () =>
                    {
                        await officeRepository.DeleteOffice(officeId);
                    };
                }
            );
            await managementObserver.Loaded;
            return managementObserver;
        }

        class OfficeProjection
        {
            public Office Office { get; set; }
            public IObservableCollection<OfficeName> Names { get; set; }
            public IObservableCollection<Headcount> Headcounts { get; set; }
        }

        class OfficeProjectionWithFields
        {
            public Office Office { get; set; }
            public IObservableCollection<string> Names { get; set; }
            public IObservableCollection<int> Headcounts { get; set; }
        }

        class OfficeProjectionWithQueryable
        {
            public Office Office { get; set; }
            public IQueryable<string> Names { get; set; }
            public IQueryable<int> Headcounts { get; set; }
        }

        class ManagerProjection
        {
            public Manager Manager { get; set; }
            public IObservableCollection<ManagerName> Names { get; set; }
        }

        class ManagementProjection
        {
            public Office Office { get; set; }
            public IObservableCollection<ManagerProjection> Managers { get; set; }
        }

        private static Specification<Office, OfficeName> namesOfOffice = Given<Office>.Match((office, facts) =>
            from name in facts.OfType<OfficeName>()
            where name.office == office
            select name
        );

        private static Specification<Office, Headcount> headcountsOfOffice = Given<Office>.Match((office, facts) =>
            from headcount in facts.OfType<Headcount>()
            where headcount.office == office
            where headcount.IsCurrent
            select headcount
        );

        private static Specification<Company, OfficeProjection> officesInCompany = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select new OfficeProjection
            {
                Office = office,
                Names = facts.Observable(office, namesOfOffice),
                Headcounts = facts.Observable(office, headcountsOfOffice)
            }
        );

        private static Specification<Company, OfficeProjection> officesInCompanyInlineObservable = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select new OfficeProjection
            {
                Office = office,
                Names = facts.Observable(
                    from name in facts.OfType<OfficeName>()
                    where name.office == office
                    select name
                ),
                Headcounts = facts.Observable(
                    from headcount in facts.OfType<Headcount>()
                    where headcount.office == office
                    where headcount.IsCurrent
                    select headcount
                )
            }
        );

        private static Specification<Company, OfficeProjectionWithFields> officesInCompanyInlineObservableWithFields = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select new OfficeProjectionWithFields
            {
                Office = office,
                Names = facts.Observable(
                    from name in facts.OfType<OfficeName>()
                    where name.office == office
                    select name.value
                ),
                Headcounts = facts.Observable(
                    from headcount in facts.OfType<Headcount>()
                    where headcount.office == office
                    where headcount.IsCurrent
                    select headcount.value
                )
            }
        );

        private static Specification<Company, OfficeProjectionWithQueryable> officesInCompanyInlineQueryableWithFields = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select new OfficeProjectionWithQueryable
            {
                Office = office,
                Names =
                    from name in facts.OfType<OfficeName>()
                    where name.office == office
                    select name.value,
                Headcounts =
                    from headcount in facts.OfType<Headcount>()
                    where headcount.office == office
                    where headcount.IsCurrent
                    select headcount.value
            }
        );

        private static Specification<Manager, ManagerName> managerNames = Given<Manager>.Match((manager, facts) =>
            from name in facts.OfType<ManagerName>()
            where name.manager == manager
            where name.IsCurrent
            select name
        );

        private static Specification<Office, ManagerProjection> managersInOffice = Given<Office>.Match((office, facts) =>
            from manager in facts.OfType<Manager>()
            where manager.office == office
            where !manager.IsTerminated

            select new ManagerProjection
            {
                Manager = manager,
                Names = facts.Observable(manager, managerNames)
            }
        );

        private static Specification<Company, ManagementProjection> managersInCompany = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select new ManagementProjection
            {
                Office = office,
                Managers = facts.Observable(office, managersInOffice)
            }
        );
    }
}
