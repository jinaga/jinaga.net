using System;
using FluentAssertions;
using Jinaga.Observers;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Jinaga.UnitTest;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Jinaga.Test
{
    public class NestedWatchTest
    {
        private readonly Jinaga j;
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

            await officeObserver.Stop();

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

            await officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeCloses()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesOld(company);

            await j.Fact(new OfficeClosure(newOffice, DateTime.UtcNow));

            officeRepository.Offices.Should().BeEmpty();

            await officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeNameAdded()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = await WhenWatchOfficesOld(company);

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

            await officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeNameAlreadySet()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var newOfficeName = await j.Fact(new OfficeName(newOffice, "Headquarters", new OfficeName[0]));

            var officeObserver = await WhenWatchOfficesOld(company);

            officeRepository.Offices.Should().BeEquivalentTo(new OfficeRow[]
            {
                new OfficeRow
                {
                    OfficeId = 1,
                    City = "Dallas",
                    Name = "Headquarters"
                }
            });

            await officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_OfficeAndNameAdded()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = await WhenWatchOfficesOld(company);

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

            await officeObserver.Stop();
        }

        [Fact]
        public async Task NestedWatch_ManagerAndNameAlreadyExist()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            var manager42 = await j.Fact(new Manager(newOffice, 42));
            var michael = await j.Fact(new ManagerName(manager42, "Michael", new ManagerName[0]));

            var managerObserver = await WhenWatchManagementOld(company);

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

            var managerObserver = await WhenWatchManagementOld(company);

            await j.Fact(new ManagerTerminated(manager42, DateTime.UtcNow));

            officeRepository.Managers.Should().BeEmpty();
        }

        [Fact]
        public async Task NestedWatch_ManagerAndNameAdded()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var managerObserver = await WhenWatchManagementOld(company);

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

        private async Task<Observer<OfficeProjection>> WhenWatchOffices(Company company)
        {
            var officeObserver = j.Watch(company, officesInCompany,
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
            await officeObserver.Initialized;
            return officeObserver;
        }

        private async Task<Observer<ManagementProjection>> WhenWatchManagement(Company company)
        {
            var managementObserver = j.Watch(company, managersInCompany,
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
            await managementObserver.Initialized;
            return managementObserver;
        }

        private async Task<ObserverOld<OfficeProjection>> WhenWatchOfficesOld(Company company)
        {
            var officeObserver = j.Watch(company, officesInCompanyOld,
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
            await officeObserver.Initialized;
            return officeObserver;
        }

        private async Task<ObserverOld<ManagementProjection>> WhenWatchManagementOld(Company company)
        {
            var managementObserver = j.Watch(company, managersInCompanyOld,
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
            await managementObserver.Initialized;
            return managementObserver;
        }

        class OfficeProjection
        {
            public Office Office { get; set; }
            public IObservableCollection<OfficeName> Names { get; set; }
            public IObservableCollection<Headcount> Headcounts { get; set; }
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
                Names = facts.All(office, namesOfOffice),
                Headcounts = facts.All(office, headcountsOfOffice)
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
                Names = facts.All(manager, managerNames)
            }
        );

        private static Specification<Company, ManagementProjection> managersInCompany = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select new ManagementProjection
            {
                Office = office,
                Managers = facts.All(office, managersInOffice)
            }
        );

        private static SpecificationOld<Office, OfficeName> namesOfOfficeOld = GivenOld<Office>.Match((office, facts) =>
            from name in facts.OfType<OfficeName>()
            where name.office == office
            select name
        );

        private static SpecificationOld<Office, Headcount> headcountsOfOfficeOld = GivenOld<Office>.Match((office, facts) =>
            from headcount in facts.OfType<Headcount>()
            where headcount.office == office
            where headcount.IsCurrent
            select headcount
        );

        private static SpecificationOld<Company, OfficeProjection> officesInCompanyOld = GivenOld<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select new OfficeProjection
            {
                Office = office,
                Names = facts.All(office, namesOfOfficeOld),
                Headcounts = facts.All(office, headcountsOfOfficeOld)
            }
        );

        private static SpecificationOld<Manager, ManagerName> managerNamesOld = GivenOld<Manager>.Match((manager, facts) =>
            from name in facts.OfType<ManagerName>()
            where name.manager == manager
            where name.IsCurrent
            select name
        );

        private static SpecificationOld<Office, ManagerProjection> managersInOfficeOld = GivenOld<Office>.Match((office, facts) =>
            from manager in facts.OfType<Manager>()
            where manager.office == office
            where !manager.IsTerminated

            select new ManagerProjection
            {
                Manager = manager,
                Names = facts.All(manager, managerNamesOld)
            }
        );

        private static SpecificationOld<Company, ManagementProjection> managersInCompanyOld = GivenOld<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select new ManagementProjection
            {
                Office = office,
                Managers = facts.All(office, managersInOfficeOld)
            }
        );
    }
}
