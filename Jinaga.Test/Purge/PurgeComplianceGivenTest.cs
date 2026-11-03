using Jinaga.Extensions;
using Jinaga.Test.Model;
using System.Collections.Generic;
using System.Linq;

namespace Jinaga.Test.Purge
{
    /// <summary>
    /// Purge compliance for a specification's givens.
    ///
    /// Compliance means a purge cannot change what a specification shows. A match whose
    /// unknown is a purge root earns that by carrying the negated purge condition: the
    /// trigger takes the root's rows out of the result through an ordinary removal, and
    /// the purge that follows deletes only facts no compliant result shows. A given is
    /// read directly, so an unconditional one of a purge-root type has its descendants
    /// deleted while they are still in the result. A query stops returning them and a
    /// watch goes on showing them, because purge sends no notification.
    /// </summary>
    public class PurgeComplianceGivenTest
    {
        private const string ManagerGivenIsNotCompliant =
            """
            The given manager of Corporate.Manager is missing purge conditions:
            !E (fact: Corporate.Manager) {
                terminated: Corporate.Manager.Terminated [
                    terminated->Manager: Corporate.Manager = fact
                ]
            }
            Write the condition on the given with Given<Corporate.Manager>.Where.
            """;

        /// <summary>The names of a manager, from a given that carries no condition.</summary>
        private static readonly Specification<Manager, string> NamesOfAnyManager =
            Given<Manager>.Match(manager =>
                manager.Successors().OfType<ManagerName>(name => name.manager)
                    .Select(name => name.value)
            );

        /// <summary>
        /// The names of a manager nobody has terminated. The given carries the purge
        /// condition, so the trigger removes the row before the purge deletes anything.
        /// </summary>
        private static readonly Specification<Manager, string> NamesOfACurrentManager =
            Given<Manager>
                .Where((manager, facts) => !facts.Any<ManagerTerminated>(terminated => terminated.Manager == manager))
                .Match(manager =>
                    manager.Successors().OfType<ManagerName>(name => name.manager)
                        .Select(name => name.value)
                );

        /// <summary>
        /// The names of a manager who has never been named. A negative existential on the
        /// given, but not the purge condition, so it does not satisfy the rule.
        /// </summary>
        private static readonly Specification<Manager, string> NamesOfAnUnnamedManager =
            Given<Manager>
                .Where((manager, facts) => !facts.Any<ManagerName>(name => name.manager == manager))
                .Match(manager =>
                    manager.Successors().OfType<ManagerName>(name => name.manager)
                        .Select(name => name.value)
                );

        /// <summary>The names of an office. Office is not a purge root.</summary>
        private static readonly Specification<Office, string> NamesOfAnOffice =
            Given<Office>.Match(office =>
                office.Successors().OfType<OfficeName>(name => name.office)
                    .Select(name => name.value)
            );

        [Fact]
        public async Task WhenGivenIsAPurgeRootWithoutTheCondition_QueryThrows()
        {
            var jinagaClient = CreateJinagaClient();
            var ada = await GivenANamedManager(jinagaClient);

            Func<Task> query = async () => await jinagaClient.Query(NamesOfAnyManager, ada);

            await query.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage(ManagerGivenIsNotCompliant);
        }

        [Fact]
        public async Task WhenGivenIsAPurgeRootWithoutTheCondition_WatchThrows()
        {
            var jinagaClient = CreateJinagaClient();
            var ada = await GivenANamedManager(jinagaClient);

            Func<Task> watch = async () =>
            {
                var observer = jinagaClient.Watch(NamesOfAnyManager, ada, name => { });
                await observer.Loaded;
                observer.Stop();
            };

            await watch.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage(ManagerGivenIsNotCompliant);
        }

        [Fact]
        public async Task WhenGivenCarriesADifferentCondition_QueryThrows()
        {
            var jinagaClient = CreateJinagaClient();
            var ada = await GivenANamedManager(jinagaClient);

            Func<Task> query = async () => await jinagaClient.Query(NamesOfAnUnnamedManager, ada);

            await query.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage(ManagerGivenIsNotCompliant);
        }

        [Fact]
        public async Task WhenGivenIsNotAPurgeRoot_QuerySucceeds()
        {
            var jinagaClient = CreateJinagaClient();
            var company = await jinagaClient.Fact(new Company("companyId"));
            var city = await jinagaClient.Fact(new City("Dallas"));
            var office = await jinagaClient.Fact(new Office(company, city));
            await jinagaClient.Fact(new OfficeName(office, "Dallas HQ", []));

            var names = await jinagaClient.Query(NamesOfAnOffice, office);

            names.Should().BeEquivalentTo(["Dallas HQ"]);
        }

        [Fact]
        public async Task WhenGivenCarriesTheCondition_TheTriggerRemovesTheRowBeforeThePurge()
        {
            var jinagaClient = CreateJinagaClient();
            var ada = await GivenANamedManager(jinagaClient);

            var names = new List<string>();
            var observer = jinagaClient.Watch(NamesOfACurrentManager, ada, name =>
            {
                names.Add(name);
                return () => { names.Remove(name); };
            });
            await observer.Loaded;

            names.Should().BeEquivalentTo(["Ada"]);
            (await jinagaClient.Query(NamesOfACurrentManager, ada)).Should().BeEquivalentTo(["Ada"]);

            await jinagaClient.Fact(new ManagerTerminated(ada, DateTime.Now));

            // The watch saw the row go, so it does not hold a name the purge has deleted.
            names.Should().BeEmpty();
            (await jinagaClient.Query(NamesOfACurrentManager, ada)).Should().BeEmpty();

            observer.Stop();
        }

        private static async Task<Manager> GivenANamedManager(JinagaClient jinagaClient)
        {
            var company = await jinagaClient.Fact(new Company("companyId"));
            var city = await jinagaClient.Fact(new City("Dallas"));
            var office = await jinagaClient.Fact(new Office(company, city));
            var ada = await jinagaClient.Fact(new Manager(office, 1));
            await jinagaClient.Fact(new ManagerName(ada, "Ada", []));
            return ada;
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
