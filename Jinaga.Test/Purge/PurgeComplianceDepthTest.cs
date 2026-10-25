using Jinaga.Extensions;
using Jinaga.Test.Model;
using System.Linq;

namespace Jinaga.Test.Purge
{
    public class PurgeComplianceDepthTest
    {
        [Fact]
        public async Task WhenCollectionProjectionReachesPurgeRoot_QueryThrows()
        {
            var jinagaClient = CreateJinagaClient();
            var company = await jinagaClient.Fact(new Company("companyId"));

            var officeManagerNames = Given<Company>.Match(company =>
                company.Successors().OfType<Office>(office => office.company)
                    .Select(office => new
                    {
                        managerNames = office.Successors().OfType<Manager>(manager => manager.office)
                            .SelectMany(manager => manager.Successors().OfType<ManagerName>(name => name.manager))
                            .Select(name => name.value)
                    })
            );

            Func<Task> query = async () => await jinagaClient.Query(officeManagerNames, company);
            await query.Should().ThrowAsync<InvalidOperationException>().WithMessage(
                """
                The match for Corporate.Manager is missing purge conditions:
                !E (fact: Corporate.Manager) {
                    terminated: Corporate.Manager.Terminated [
                        terminated->Manager: Corporate.Manager = fact
                    ]
                }
                """);
        }

        [Fact]
        public async Task WhenExistentialConditionReachesPurgeRoot_QueryThrows()
        {
            var jinagaClient = CreateJinagaClient();
            var company = await jinagaClient.Fact(new Company("companyId"));

            var officesWithNamedManagers = Given<Company>.Match(company =>
                company.Successors().OfType<Office>(office => office.company)
                    .Where(office => office.Successors().OfType<Manager>(manager => manager.office)
                        .SelectMany(manager => manager.Successors().OfType<ManagerName>(name => name.manager))
                        .Any())
            );

            Func<Task> query = async () => await jinagaClient.Query(officesWithNamedManagers, company);
            await query.Should().ThrowAsync<InvalidOperationException>().WithMessage(
                """
                The match for Corporate.Manager is missing purge conditions:
                !E (fact: Corporate.Manager) {
                    terminated: Corporate.Manager.Terminated [
                        terminated->Manager: Corporate.Manager = fact
                    ]
                }
                """);
        }

        [Fact]
        public async Task WhenNegativeExistentialConditionReachesPurgeRoot_QueryThrows()
        {
            var jinagaClient = CreateJinagaClient();
            var company = await jinagaClient.Fact(new Company("companyId"));

            var officesWithNoManagers = Given<Company>.Match(company =>
                company.Successors().OfType<Office>(office => office.company)
                    .WhereNo((Manager manager) => manager.office)
            );

            Func<Task> query = async () => await jinagaClient.Query(officesWithNoManagers, company);
            await query.Should().ThrowAsync<InvalidOperationException>().WithMessage(
                """
                The match for Corporate.Manager is missing purge conditions:
                !E (fact: Corporate.Manager) {
                    terminated: Corporate.Manager.Terminated [
                        terminated->Manager: Corporate.Manager = fact
                    ]
                }
                """);
        }

        [Fact]
        public async Task WhenCollectionProjectionCarriesPurgeCondition_PurgeDoesNotChangeTheResult()
        {
            var jinagaClient = CreateJinagaClient();
            var company = await jinagaClient.Fact(new Company("companyId"));
            var city = await jinagaClient.Fact(new City("Dallas"));
            var office = await jinagaClient.Fact(new Office(company, city));
            var ada = await jinagaClient.Fact(new Manager(office, 1));
            await jinagaClient.Fact(new ManagerName(ada, "Ada", []));
            var carl = await jinagaClient.Fact(new Manager(office, 2));

            var officeManagerNames = Given<Company>.Match(company =>
                company.Successors().OfType<Office>(office => office.company)
                    .Select(office => new
                    {
                        managerNames = office.Successors().OfType<Manager>(manager => manager.office)
                            .WhereNo((ManagerTerminated terminated) => terminated.Manager)
                            .SelectMany(manager => manager.Successors().OfType<ManagerName>(name => name.manager))
                            .Select(name => name.value)
                    })
            );

            var before = await jinagaClient.Query(officeManagerNames, company);
            before.Single().managerNames.Should().BeEquivalentTo(["Ada"]);

            await jinagaClient.Fact(new ManagerTerminated(carl, DateTime.Now));

            var after = await jinagaClient.Query(officeManagerNames, company);
            after.Single().managerNames.Should().BeEquivalentTo(["Ada"]);
        }

        [Fact]
        public async Task WhenExistentialConditionCarriesPurgeCondition_PurgeDoesNotChangeTheResult()
        {
            var jinagaClient = CreateJinagaClient();
            var company = await jinagaClient.Fact(new Company("companyId"));
            var city = await jinagaClient.Fact(new City("Dallas"));
            var office = await jinagaClient.Fact(new Office(company, city));
            var ada = await jinagaClient.Fact(new Manager(office, 1));
            await jinagaClient.Fact(new ManagerName(ada, "Ada", []));
            var carl = await jinagaClient.Fact(new Manager(office, 2));
            await jinagaClient.Fact(new ManagerName(carl, "Carl", []));

            var officesWithNamedManagers = Given<Company>.Match(company =>
                company.Successors().OfType<Office>(office => office.company)
                    .Where(office => office.Successors().OfType<Manager>(manager => manager.office)
                        .WhereNo((ManagerTerminated terminated) => terminated.Manager)
                        .SelectMany(manager => manager.Successors().OfType<ManagerName>(name => name.manager))
                        .Any())
            );

            var before = await jinagaClient.Query(officesWithNamedManagers, company);
            before.Should().ContainSingle().Which.Should().Be(office);

            await jinagaClient.Fact(new ManagerTerminated(carl, DateTime.Now));

            var after = await jinagaClient.Query(officesWithNamedManagers, company);
            after.Should().ContainSingle().Which.Should().Be(office);
        }

        private static JinagaClient CreateJinagaClient()
        {
            return JinagaClient.Create(options =>
            {
                options.PurgeConditions = p => p
                    .Purge<Manager>().WhenExists<ManagerTerminated>(terminated => terminated.Manager);
            });
        }
    }
}
