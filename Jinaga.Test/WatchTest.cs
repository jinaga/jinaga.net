using FluentAssertions;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Jinaga.UnitTest;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Jinaga.Test
{
    public partial class WatchTest
    {
        private readonly Jinaga j;
        private readonly FakeRepository<Office> officeRepository;

        public WatchTest()
        {
            j = JinagaTest.Create();
            officeRepository = new FakeRepository<Office>();
        }

        [Fact]
        public async Task Watch_NoResults()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = j.Watch(company, officesInCompany,
                async office => await officeRepository.Insert(office)
            );
            await officeObserver.Initialized;

            await officeObserver.Stop();

            officeRepository.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task Watch_AlreadyExists()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = j.Watch(company, officesInCompany,
                async office => await officeRepository.Insert(office)
            );
            await officeObserver.Initialized;

            await officeObserver.Stop();

            officeRepository.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(newOffice);
        }

        [Fact]
        public async Task Watch_Added()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = j.Watch(company, officesInCompany,
                async office => await officeRepository.Insert(office)
            );
            await officeObserver.Initialized;

            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            await officeObserver.Stop();

            officeRepository.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(newOffice);
        }

        [Fact]
        public async Task Watch_AddedToOtherPredecessor()
        {
            var company = await j.Fact(new Company("Contoso"));
            var otherCompany = await j.Fact(new Company("OtherCompany"));

            var officeObserver = j.Watch(company, officesInCompany,
                async office => await officeRepository.Insert(office)
            );
            await officeObserver.Initialized;

            var newOffice = await j.Fact(new Office(otherCompany, new City("Dallas")));
            await officeObserver.Stop();

            officeRepository.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task Watch_ExistingRemoved()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = j.Watch(company, officesInCompany, async office =>
            {
                int officeId = await officeRepository.Insert(office);
                return async () =>
                {
                    await officeRepository.Delete(officeId);
                };
            });
            await officeObserver.Initialized;

            await j.Fact(new OfficeClosure(newOffice, DateTime.Now));
            await officeObserver.Stop();

            officeRepository.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task Watch_NewRemoved()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = j.Watch(company, officesInCompany, async office =>
            {
                int officeId = await officeRepository.Insert(office);
                return async () =>
                {
                    await officeRepository.Delete(officeId);
                };
            });
            await officeObserver.Initialized;

            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            await j.Fact(new OfficeClosure(newOffice, DateTime.Now));
            await officeObserver.Stop();

            officeRepository.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task Watch_AddedAfterStopped()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = j.Watch(company, officesInCompany,
                async office => await officeRepository.Insert(office)
            );
            await officeObserver.Initialized;

            await officeObserver.Stop();

            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            officeRepository.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task Watch_ExistingRemovedAfterStopped()
        {
            var company = await j.Fact(new Company("Contoso"));
            var newOffice = await j.Fact(new Office(company, new City("Dallas")));

            var officeObserver = j.Watch(company, officesInCompany, async office =>
            {
                int officeId = await officeRepository.Insert(office);
                return async () =>
                {
                    await officeRepository.Delete(officeId);
                };
            });
            await officeObserver.Initialized;

            await officeObserver.Stop();

            await j.Fact(new OfficeClosure(newOffice, DateTime.Now));

            officeRepository.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(newOffice);
        }

        [Fact]
        public async Task Watch_NewRemovedAfterStopped()
        {
            var company = await j.Fact(new Company("Contoso"));

            var officeObserver = j.Watch(company, officesInCompany, async office =>
            {
                int id = await officeRepository.Insert(office);
                return async () =>
                {
                    await officeRepository.Delete(id);
                };
            });
            await officeObserver.Initialized;

            var newOffice = await j.Fact(new Office(company, new City("Dallas")));
            await officeObserver.Stop();

            await j.Fact(new OfficeClosure(newOffice, DateTime.Now));

            officeRepository.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(newOffice);
        }

        private static Specification<Company, Office> officesInCompany = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !office.IsClosed

            select office
        );
    }
}
