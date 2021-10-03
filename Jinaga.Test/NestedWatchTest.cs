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
        public async Task Watch_NoResults()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = j.Watch(company, officesInCompany,
                async projection =>
                {
                    int officeId = await officeRepository.InsertOffice();
                    projection.Names.OnAdded(async name =>
                    {
                        await officeRepository.UpdateOffice(officeId, name.value);
                    });
                });
            await officeObserver.Initialized;

            await officeObserver.Stop();

            officeRepository.Offices.Should().BeEmpty();
        }

        class OfficeProjection
        {
            public Office Office { get; set; }
            public IObservableCollection<OfficeName> Names { get; set; }
        }

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

        private static Specification<Office, OfficeName> namesOfOffice = Given<Office>.Match((office, facts) =>
            from name in facts.OfType<OfficeName>()
            where name.office == office
            select name
        );
    }
}
