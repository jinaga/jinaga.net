using System;
using System.Linq;
using Jinaga.Extensions;

namespace Jinaga.Test.Model
{

    [FactType("Corporate.Company")]
    public record Company(string identifier)
    {
        public IQueryable<Office> Offices => Relation.Define(() =>
            this.Successors().OfType<Office>(office => office.company)
                .WhereNo((OfficeClosure closure) => closure.office)
        );
    }

    [FactType("Corporate.Office")]
    public record Office(Company company, City city)
    {
        public Condition IsClosed => Condition.Define(() =>
            this.Successors().Any<OfficeClosure>(closure => closure.office)
        );

        public Relation<Headcount> Headcount => Relation.Define(facts =>
            facts.OfType<Headcount>(headcount => headcount.office == this &&
                headcount.IsCurrent
            )
        );

        public Relation<Manager> Managers => Relation.Define(facts =>
            facts.OfType<Manager>(manager => manager.office == this &&
                !manager.IsTerminated
            )
        );
    }

    [FactType("Corporate.Office.Name")]
    public record OfficeName(Office office, string value, OfficeName[] prior);


    [FactType("Corporate.Office.Closure")]
    public record OfficeClosure(Office office, DateTime closureDate);

    [FactType("Corporate.Office.Reopening")]
    public record OfficeReopening(OfficeClosure officeClosure, DateTime reopeningDate);

    [FactType("Corporate.City")]
    public record City(string name);

    [FactType("Corporate.Headcount")]
    public record Headcount(Office office, int value, Headcount[] prior)
    {
        public Condition IsCurrent => Condition.Define(facts => !(
            from next in facts.OfType<Headcount>()
            where next.prior.Contains(this)
            select next
        ).Any());
    }

    [FactType("Corporate.Manager")]
    public record Manager(Office office, int employeeNumber)
    {
        public Condition IsTerminated => Condition.Define(facts => (
            facts.OfType<ManagerTerminated>(termination => termination.Manager == this)
        ).Any());

        public Relation<ManagerName> Names => Relation.Define(facts =>
            facts.OfType<ManagerName>(name => name.manager == this &&
                name.IsCurrent)
        );

        public Relation<string> NameValues => Relation.Define(facts =>
            facts.OfType<ManagerName>(name => name.manager == this &&
                name.IsCurrent
            ).Select(name => name.value)
        );
    }

    [FactType("Corporate.Manager.Name")]
    public record ManagerName(Manager manager, string value, ManagerName[] prior)
    {
        public Condition IsCurrent => Condition.Define(facts => !(
            facts.OfType<ManagerName>(next => next.prior.Contains(this))
        ).Any());
    }

    [FactType("Corporate.Manager.Terminated")]
    public record ManagerTerminated(Manager Manager, DateTime terminationDate);
}