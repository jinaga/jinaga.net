using System;
using System.Linq;

namespace Jinaga.Test.Model
{

    [FactType("Corporate.Company")]
    public record Company(string identifier);

    [FactType("Corporate.Office")]
    public record Office(Company company, City city)
    {
        public Condition IsClosed => Condition.Define(facts =>
            facts.Any<OfficeClosure>(closure => closure.office == this)
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