using Jinaga;
using System;
using System.Linq;

namespace Jinaga.Test.Model
{

    [FactType("Corporate.Company")]
    record Company(string identifier);

    [FactType("Corporate.Office")]
    record Office(Company company, City city)
    {
        public Condition IsClosed => new Condition(facts => (
            from closure in facts.OfType<OfficeClosure>()
            where closure.office == this
            select closure)
        .Any());
    }

    [FactType("Corporate.Office.Name")]
    record OfficeName(Office office, string value, OfficeName[] prior);


    [FactType("Corporate.Office.Closure")]
    record OfficeClosure(Office office, DateTime closureDate);

    [FactType("Corporate.Office.Reopening")]
    record OfficeReopening(OfficeClosure officeClosure, DateTime reopeningDate);

    [FactType("Corporate.City")]
    record City(string name);

    [FactType("Corporate.Headcount")]
    record Headcount(Office office, int value, Headcount[] prior)
    {
        public Condition IsCurrent => new Condition(facts => !(
            from next in facts.OfType<Headcount>()
            where next.prior.Contains(this)
            select next
        ).Any());
    }

    [FactType("Corporate.Manager")]
    record Manager(Office office, int employeeNumber)
    {
        public Condition IsTerminated => new Condition(facts => (
            facts.OfType<ManagerTerminated>(termination => termination.Manager == this)
        ).Any());
    }

    [FactType("Corporate.Manager.Name")]
    record ManagerName(Manager manager, string value, ManagerName[] prior)
    {
        public Condition IsCurrent => new Condition(facts => !(
            facts.OfType<ManagerName>(next => next.prior.Contains(this))
        ).Any());
    }

    [FactType("Corporate.Manager.Terminated")]
    record ManagerTerminated(Manager Manager, DateTime terminationDate);
}