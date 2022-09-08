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

            var officeObserver = await WhenWatchOffices(company);

            await j.Fact(new OfficeClosure(newOffice, DateTime.UtcNow));

            officeRepository.Offices.Should().BeEmpty();

            await officeObserver.Stop();
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

            await officeObserver.Stop();
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

            await officeObserver.Stop();
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

            await officeObserver.Stop();
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

        private static SpecificationOld<Office, OfficeName> namesOfOffice = GivenOld<Office>.Match((office, facts) =>
            from name in facts.OfType<OfficeName>()
            where name.office == office
            select name
        );

        private static SpecificationOld<Office, Headcount> headcountsOfOffice = GivenOld<Office>.Match((office, facts) =>
            from headcount in facts.OfType<Headcount>()
            where headcount.office == office
            where headcount.IsCurrent
            select headcount
        );

        private static SpecificationOld<Company, OfficeProjection> officesInCompany = GivenOld<Company>.Match((company, facts) =>
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

        private static SpecificationOld<Manager, ManagerName> managerNames = GivenOld<Manager>.Match((manager, facts) =>
            from name in facts.OfType<ManagerName>()
            where name.manager == manager
            where name.IsCurrent
            select name
        );

        private static SpecificationOld<Office, ManagerProjection> managersInOffice = GivenOld<Office>.Match((office, facts) =>
            from manager in facts.OfType<Manager>()
            where manager.office == office
            where !manager.IsTerminated

            select new ManagerProjection
            {
                Manager = manager,
                Names = facts.All(manager, managerNames)
            }
        );

        private static SpecificationOld<Company, ManagementProjection> managersInCompany = GivenOld<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select new ManagementProjection
            {
                Office = office,
                Managers = facts.All(office, managersInOffice)
            }
        );
    }
}
