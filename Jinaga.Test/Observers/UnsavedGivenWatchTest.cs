using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using System.Collections.Generic;
using System.Linq;

namespace Jinaga.Test.Observers
{
    public class UnsavedGivenWatchTest
    {
        private static Specification<Office, Company> CompanyOfOffice = Given<Office>.Match((office, facts) =>
            facts.OfType<Company>()
                .Where(company => company == office.company)
        );

        private static Specification<Office, Office> SiblingOffices = Given<Office>.Match((office, facts) =>
            facts.OfType<Office>()
                .Where(sibling => sibling.company == office.company)
        );

        [Fact]
        public async Task PredecessorAccessWithUnpersistedGivenDeliversWhenGivenIsSaved()
        {
            var j = JinagaTest.Create();
            var company = await j.Fact(new Company("Contoso"));
            var office = new Office(company, new City("Dallas"));   // not saved

            var results = new List<Company>();
            var observer = j.Watch(CompanyOfOffice, office, c =>
            {
                results.Add(c);
                return Task.CompletedTask;
            });
            await observer.Loaded;
            results.Should().BeEmpty();

            await j.Fact(office);
            observer.Stop();

            results.Should().HaveCount(1);
        }

        [Fact]
        public async Task SiblingsWithUnpersistedGivenDeliverWhenGivenIsSaved()
        {
            var j = JinagaTest.Create();
            var company = await j.Fact(new Company("Contoso"));
            await j.Fact(new Office(company, new City("Austin")));
            var office = new Office(company, new City("Dallas"));   // not saved

            // Dallas arrives through the sibling inverse and again through the
            // self-inverse in the same save; the notified-tuple dedupe is what
            // keeps it to one delivery.
            var repository = new FakeRepository<Office>();
            var observer = j.Watch(SiblingOffices, office, async o =>
            {
                int id = await repository.Insert(o);
                return async () => await repository.Delete(id);
            });
            await observer.Loaded;

            await j.Fact(office);
            observer.Stop();

            repository.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task SavingTheGivenASecondTimeDeliversNothingNew()
        {
            var j = JinagaTest.Create();
            var company = await j.Fact(new Company("Contoso"));
            var office = new Office(company, new City("Dallas"));   // not saved

            var results = new List<Company>();
            var observer = j.Watch(CompanyOfOffice, office, c =>
            {
                results.Add(c);
                return Task.CompletedTask;
            });
            await observer.Loaded;

            await j.Fact(office);
            // The store reports only newly added facts, so the second save
            // notifies nothing and the self-inverse fires once in all.
            await j.Fact(office);
            observer.Stop();

            results.Should().HaveCount(1);
        }

        [Fact]
        public async Task SavingAGivenThatWasAlreadySavedDeliversNothingNew()
        {
            var j = JinagaTest.Create();
            var company = await j.Fact(new Company("Contoso"));
            var office = await j.Fact(new Office(company, new City("Dallas")));

            var results = new List<Company>();
            var observer = j.Watch(CompanyOfOffice, office, c =>
            {
                results.Add(c);
                return Task.CompletedTask;
            });
            await observer.Loaded;
            results.Should().HaveCount(1);

            // The initial read already delivered the company, and re-saving the
            // office adds no fact, so nothing re-runs the specification.
            await j.Fact(office);
            observer.Stop();

            results.Should().HaveCount(1);
        }

        [Fact]
        public async Task QueryWithUnpersistedGivenRunsOnTheGraph()
        {
            var j = JinagaTest.Create();
            var company = await j.Fact(new Company("Contoso"));
            var office = new Office(company, new City("Dallas"));

            var companies = await j.Query(CompanyOfOffice, office);

            companies.Should().HaveCount(1);
        }
    }
}
