using Jinaga;
using System;
using System.Linq;

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

[FactType("Corporate.Office.Closure")]
record OfficeClosure(Office office, DateTime closureDate);

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
