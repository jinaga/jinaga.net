using Jinaga.Observers;
using Jinaga.Test.Model;
using System.Collections.Generic;
using System.Linq;

namespace Jinaga.Test
{
    public class StaleNestedHandlerTest
    {
        private readonly JinagaClient j;

        public StaleNestedHandlerTest()
        {
            j = JinagaTest.Create();
        }

        [Fact]
        public async Task NameAddedToReopenedOfficeIsDelivered()
        {
            var company = await j.Fact(new Company("Contoso"));
            var office = await j.Fact(new Office(company, new City("Dallas")));

            var deliveries = new List<List<string>>();
            var observer = j.Watch(openOffices, company, projection =>
            {
                var names = new List<string>();
                deliveries.Add(names);
                projection.Names.OnAdded(name => { names.Add(name.value); });
                return Task.FromResult<Func<Task>>(() => Task.CompletedTask);
            });
            await observer.Loaded;
            deliveries.Should().HaveCount(1);

            var closure = await j.Fact(new OfficeClosure(office, DateTime.Now));
            await j.Fact(new OfficeReopening(closure, DateTime.Now));
            deliveries.Should().HaveCount(2);

            await j.Fact(new OfficeName(office, "Headquarters", new OfficeName[0]));
            observer.Stop();

            deliveries[1].Should().BeEquivalentTo(new[] { "Headquarters" });
            deliveries[0].Should().BeEmpty();
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

        private static Specification<Company, OfficeProjection> openOffices = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where !facts.OfType<OfficeClosure>()
                .Where(closure => closure.office == office)
                .Where(closure => !facts.OfType<OfficeReopening>()
                    .Where(reopening => reopening.officeClosure == closure)
                    .Any())
                .Any()
            select new OfficeProjection
            {
                Office = office,
                Names = facts.Observable(office, namesOfOffice)
            }
        );
    }
}
