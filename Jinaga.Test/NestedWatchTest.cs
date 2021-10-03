using FluentAssertions;
using Jinaga.Observers;
using Jinaga.Test.Fakes;
using Jinaga.UnitTest;
using System;
using System.Collections.Generic;
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
        public async Task NestedWatch_AlreadyExists()
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

        private async Task<Observer<OfficeProjection>> WhenWatchOffices(Company company)
        {
            var officeObserver = j.Watch(company, officesInCompany,
                async projection =>
                {
                    int officeId = await officeRepository.InsertOffice(projection.Office.city.name);
                    projection.Names.OnAdded(async name =>
                    {
                        await officeRepository.UpdateOffice(officeId, name.value);
                    });
                });
            await officeObserver.Initialized;
            return officeObserver;
        }

        class OfficeProjection
        {
            public Office Office { get; set; }
            public IObservableCollection<OfficeName> Names { get; set; }
        }

        private static Specification<Office, OfficeName> namesOfOffice = Given<Office>.Match((office, facts) =>
            from name in facts.OfType<OfficeName>()
            where name.office == office
            select name
        );

        private static Specification<Company, OfficeProjection> officesInCompany = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select new OfficeProjection
            {
                Office = office,
                Names = facts.All(office, namesOfOffice)
            }
        );
    }
}
