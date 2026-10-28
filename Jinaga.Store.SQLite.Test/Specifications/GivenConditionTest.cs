using Jinaga.Extensions;
using Jinaga.Facts;
using Jinaga.Identity;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test.Specifications;

/// <summary>
/// Conditions written on a given through <c>Given&lt;...&gt;.Where</c>.
///
/// The specification model, both stores and the inverse port have always accepted a
/// condition on a given. Until <c>Where</c> there was no way to write one in C#, so this
/// pins what the DSL now produces against hand-built specifications carrying the same
/// conditions, and against what the SQLite store answers for each.
///
/// The SQLite store renders a specification into SQL rather than walking it, so the shape
/// has to be exercised here and not only against the in-memory store.
/// </summary>
public class GivenConditionTest
{
    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "GivenConditionTest.db");

    private readonly ITestOutputHelper output;

    public GivenConditionTest(ITestOutputHelper output)
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        this.output = output;
    }

    private JinagaClient GivenJinagaClient()
    {
        return new JinagaClient(
            new SQLiteStore(SQLitePath, NullLoggerFactory.Instance),
            new FakeNetwork(output),
            [],
            NullLoggerFactory.Instance,
            new JinagaClientOptions());
    }

    private const string OfficeType = "Corporate.Office";
    private const string ClosureType = "Corporate.Office.Closure";
    private const string OfficeNameType = "Corporate.Office.Name";

    /// <summary>
    /// The office has not been closed. Hand-built, so it is an expectation rather than a
    /// second copy of what the translation produced.
    /// </summary>
    private static ExistentialCondition OfficeIsOpen(string given) =>
        new ExistentialCondition(false, ImmutableList.Create(
            new Match(
                new Label("closure", ClosureType),
                ImmutableList.Create(new PathCondition(
                    ImmutableList.Create(new Role("office", OfficeType)),
                    given,
                    ImmutableList<Role>.Empty)),
                ImmutableList<ExistentialCondition>.Empty)));

    /// <summary>Some office of the company has been named.</summary>
    private static ExistentialCondition CompanyHasANamedOffice(string given) =>
        new ExistentialCondition(true, ImmutableList.Create(
            new Match(
                new Label("companyName", OfficeNameType),
                ImmutableList.Create(new PathCondition(
                    ImmutableList.Create(
                        new Role("office", OfficeType),
                        new Role("company", "Corporate.Company")),
                    given,
                    ImmutableList<Role>.Empty)),
                ImmutableList<ExistentialCondition>.Empty)));

    /// <summary>Some office in the city has been named.</summary>
    private static ExistentialCondition CityHasANamedOffice(string given) =>
        new ExistentialCondition(true, ImmutableList.Create(
            new Match(
                new Label("cityName", OfficeNameType),
                ImmutableList.Create(new PathCondition(
                    ImmutableList.Create(
                        new Role("office", OfficeType),
                        new Role("city", "Corporate.City")),
                    given,
                    ImmutableList<Role>.Empty)),
                ImmutableList<ExistentialCondition>.Empty)));

    /// <summary>Some office of the company, in the city, has been named.</summary>
    private static ExistentialCondition CompanyHasANamedOfficeInCity(string company, string city) =>
        new ExistentialCondition(true, ImmutableList.Create(
            new Match(
                new Label("sharedName", OfficeNameType),
                ImmutableList.Create(
                    new PathCondition(
                        ImmutableList.Create(
                            new Role("office", OfficeType),
                            new Role("company", "Corporate.Company")),
                        company,
                        ImmutableList<Role>.Empty),
                    new PathCondition(
                        ImmutableList.Create(
                            new Role("office", OfficeType),
                            new Role("city", "Corporate.City")),
                        city,
                        ImmutableList<Role>.Empty)),
                ImmutableList<ExistentialCondition>.Empty)));

    /// <summary>
    /// The same specification with a hand-built condition on its given. The matches and the
    /// projection come from the specification itself, so the two differ in nothing but how
    /// the condition on the given was built.
    /// </summary>
    private static Specification<TFact, TProjection> WithConditionOnGiven<TFact, TProjection>(
        Specification<TFact, TProjection> specification,
        Func<string, ExistentialCondition> condition)
        where TFact : class =>
        new Specification<TFact, TProjection>(
            ImmutableList.Create(new SpecificationGiven(
                specification.Givens.Single().Label,
                ImmutableList.Create(condition(specification.Givens.Single().Label.Name)))),
            specification.Matches,
            specification.Projection);

    private static Specification<TFact1, TFact2, TProjection> WithConditionsOnGivens<TFact1, TFact2, TProjection>(
        Specification<TFact1, TFact2, TProjection> specification,
        ImmutableList<ExistentialCondition> first,
        ImmutableList<ExistentialCondition> second)
        where TFact1 : class
        where TFact2 : class =>
        new Specification<TFact1, TFact2, TProjection>(
            ImmutableList.Create(
                new SpecificationGiven(specification.Givens[0].Label, first),
                new SpecificationGiven(specification.Givens[1].Label, second)),
            specification.Matches,
            specification.Projection);

    // ----- Every entry point of Given<TFact> ------------------------------------------

    private static readonly Specification<Office, Manager> ManagersOfAnOpenOffice =
        Given<Office>
            .Where((office, facts) => !facts.Any<OfficeClosure>(closure => closure.office == office))
            .Match((office, facts) =>
                from manager in facts.OfType<Manager>()
                where manager.office == office
                select manager);

    private static readonly Specification<Office, Manager> ManagersOfAnOpenOfficeUnconditional =
        Given<Office>
            .Match((office, facts) =>
                from manager in facts.OfType<Manager>()
                where manager.office == office
                select manager);

    private static readonly Specification<Office, Manager> SuccessorsOfAnOpenOffice =
        Given<Office>
            .Where((office, facts) => !facts.Any<OfficeClosure>(closure => closure.office == office))
            .Match(office => office.Successors().OfType<Manager>(manager => manager.office));

    private static readonly Specification<Office, Manager> SuccessorsOfAnOpenOfficeUnconditional =
        Given<Office>
            .Match(office => office.Successors().OfType<Manager>(manager => manager.office));

    private static readonly Specification<Office, Company> CompanyOfAnOpenOffice =
        Given<Office>
            .Where((office, facts) => !facts.Any<OfficeClosure>(closure => closure.office == office))
            .Match(office => office.company);

    private static readonly Specification<Office, Company> CompanyOfAnOpenOfficeUnconditional =
        Given<Office>
            .Match(office => office.company);

    private static readonly Specification<Office, Office> AnOpenOfficeWithRepository =
        Given<Office>
            .Where((office, facts) => !facts.Any<OfficeClosure>(closure => closure.office == office))
            .Select((office, facts) => office);

    private static readonly Specification<Office, Office> AnOpenOfficeWithRepositoryUnconditional =
        Given<Office>
            .Select((office, facts) => office);

    private static readonly Specification<Office, Office> AnOpenOffice =
        Given<Office>
            .Where((office, facts) => !facts.Any<OfficeClosure>(closure => closure.office == office))
            .Select(office => office);

    private static readonly Specification<Office, Office> AnOpenOfficeUnconditional =
        Given<Office>
            .Select(office => office);

    /// <summary>
    /// A <see cref="Condition"/> property reaches the same translation a written-out
    /// predicate does, so the condition it defines lands on the given unchanged.
    /// </summary>
    private static readonly Specification<Office, Manager> ManagersOfAnOfficeThatIsNotClosed =
        Given<Office>
            .Where((office, facts) => !office.IsClosed)
            .Match((office, facts) =>
                from manager in facts.OfType<Manager>()
                where manager.office == office
                select manager);

    [Fact]
    public void GivenCondition_MatchWithARepositoryCarriesTheConditionOnItsGiven() =>
        AssertCarriesTheCondition(ManagersOfAnOpenOffice, ManagersOfAnOpenOfficeUnconditional);

    [Fact]
    public Task GivenCondition_MatchWithARepositoryAnswersAsItsHandBuiltEquivalent() =>
        AssertAnswersAsHandBuilt(ManagersOfAnOpenOffice, ManagersOfAnOpenOfficeUnconditional);

    [Fact]
    public void GivenCondition_MatchWithoutARepositoryCarriesTheConditionOnItsGiven() =>
        AssertCarriesTheCondition(SuccessorsOfAnOpenOffice, SuccessorsOfAnOpenOfficeUnconditional);

    [Fact]
    public Task GivenCondition_MatchWithoutARepositoryAnswersAsItsHandBuiltEquivalent() =>
        AssertAnswersAsHandBuilt(SuccessorsOfAnOpenOffice, SuccessorsOfAnOpenOfficeUnconditional);

    [Fact]
    public void GivenCondition_ScalarMatchCarriesTheConditionOnItsGiven() =>
        AssertCarriesTheCondition(CompanyOfAnOpenOffice, CompanyOfAnOpenOfficeUnconditional);

    [Fact]
    public Task GivenCondition_ScalarMatchAnswersAsItsHandBuiltEquivalent() =>
        AssertAnswersAsHandBuilt(CompanyOfAnOpenOffice, CompanyOfAnOpenOfficeUnconditional);

    [Fact]
    public void GivenCondition_SelectWithARepositoryCarriesTheConditionOnItsGiven() =>
        AssertCarriesTheCondition(AnOpenOfficeWithRepository, AnOpenOfficeWithRepositoryUnconditional);

    [Fact]
    public Task GivenCondition_SelectWithARepositoryAnswersAsItsHandBuiltEquivalent() =>
        AssertAnswersAsHandBuiltWithNoMatches(AnOpenOfficeWithRepository, AnOpenOfficeWithRepositoryUnconditional);

    [Fact]
    public void GivenCondition_SelectWithoutARepositoryCarriesTheConditionOnItsGiven() =>
        AssertCarriesTheCondition(AnOpenOffice, AnOpenOfficeUnconditional);

    [Fact]
    public Task GivenCondition_SelectWithoutARepositoryAnswersAsItsHandBuiltEquivalent() =>
        AssertAnswersAsHandBuiltWithNoMatches(AnOpenOffice, AnOpenOfficeUnconditional);

    [Fact]
    public void GivenCondition_AConditionPropertyProducesWhatItsPredicateProduces()
    {
        ManagersOfAnOfficeThatIsNotClosed.Givens.Should().ContainSingle()
            .Which.ExistentialConditions.Should().ContainSingle();
        ManagersOfAnOfficeThatIsNotClosed.ToDescriptiveString()
            .Should().Be(ManagersOfAnOpenOffice.ToDescriptiveString());
    }

    [Fact]
    public Task GivenCondition_AConditionPropertyAnswersAsItsHandBuiltEquivalent() =>
        AssertAnswersAsHandBuilt(ManagersOfAnOfficeThatIsNotClosed, ManagersOfAnOpenOfficeUnconditional);

    private static void AssertCarriesTheCondition<TProjection>(
        Specification<Office, TProjection> conditional,
        Specification<Office, TProjection> unconditional)
    {
        conditional.Givens.Should().ContainSingle()
            .Which.ExistentialConditions.Should().ContainSingle();
        conditional.ToDescriptiveString().Should().Be(
            WithConditionOnGiven(unconditional, OfficeIsOpen).ToDescriptiveString());
    }

    /// <summary>
    /// The specification answers what the hand-built one answers, on both sides of the
    /// condition: the office starts open and is then closed. The expectation is the store's
    /// answer to the hand-built specification rather than a literal, so a change to how
    /// either is evaluated moves both sides together.
    /// </summary>
    private async Task AssertAnswersAsHandBuilt<TProjection>(
        Specification<Office, TProjection> conditional,
        Specification<Office, TProjection> unconditional)
    {
        var j = GivenJinagaClient();
        var company = await j.Fact(new Company("contoso"));
        var dallas = await j.Fact(new City("Dallas"));
        var office = await j.Fact(new Office(company, dallas));
        await j.Fact(new Manager(office, 1));

        var handBuilt = WithConditionOnGiven(unconditional, OfficeIsOpen);

        var before = await j.Query(conditional, office);
        before.Should().NotBeEmpty();
        before.Should().BeEquivalentTo(await j.Query(handBuilt, office));

        await j.Fact(new OfficeClosure(office, DateTime.UtcNow));

        var after = await j.Query(conditional, office);
        after.Should().BeEmpty();
        after.Should().BeEquivalentTo(await j.Query(handBuilt, office));
    }

    /// <summary>
    /// Both <c>Select</c> forms here project the given itself, so the specification has no
    /// matches and therefore no path conditions. The SQLite store allocates a query input
    /// only where a path condition names a given, so such a specification renders a query
    /// with no inputs, which it reads as unsatisfiable: it answers nothing whether the
    /// condition on the given holds or not. The in-memory store answers the given's own row
    /// instead.
    ///
    /// That divergence predates <c>Given.Where</c>. A hand-built specification carrying a
    /// condition on its given has always read this way here, and a specification without one
    /// never reaches the store at all, because <c>CanRunOnGraph</c> sends it to the graph
    /// evaluator. What this test holds is what the DSL owes: <c>Where</c> answers whatever
    /// the hand-built specification answers. The unconditional specification is read as well,
    /// so the emptiness is attributable to the condition rather than to the data.
    /// </summary>
    private async Task AssertAnswersAsHandBuiltWithNoMatches<TProjection>(
        Specification<Office, TProjection> conditional,
        Specification<Office, TProjection> unconditional)
    {
        var j = GivenJinagaClient();
        var company = await j.Fact(new Company("contoso"));
        var dallas = await j.Fact(new City("Dallas"));
        var office = await j.Fact(new Office(company, dallas));
        await j.Fact(new Manager(office, 1));

        var handBuilt = WithConditionOnGiven(unconditional, OfficeIsOpen);

        (await j.Query(unconditional, office)).Should().NotBeEmpty();

        var before = await j.Query(conditional, office);
        before.Should().BeEquivalentTo(await j.Query(handBuilt, office));

        await j.Fact(new OfficeClosure(office, DateTime.UtcNow));

        var after = await j.Query(conditional, office);
        after.Should().BeEmpty();
        after.Should().BeEquivalentTo(await j.Query(handBuilt, office));
    }

    // ----- Two givens: where each conjunct lands -------------------------------------

    private static readonly Specification<Company, City, Manager> ManagersUnconditional =
        Given<Company, City>
            .Match((company, city, facts) =>
                from manager in facts.OfType<Manager>()
                where manager.office.company == company
                where manager.office.city == city
                select manager);

    private static readonly Specification<Company, City, Manager> ManagersWhereTheCompanyHasANamedOffice =
        Given<Company, City>
            .Where((company, city, facts) => facts.Any<OfficeName>(companyName => companyName.office.company == company))
            .Match((company, city, facts) =>
                from manager in facts.OfType<Manager>()
                where manager.office.company == company
                where manager.office.city == city
                select manager);

    private static readonly Specification<Company, City, Manager> ManagersWhereTheCityHasANamedOffice =
        Given<Company, City>
            .Where((company, city, facts) => facts.Any<OfficeName>(cityName => cityName.office.city == city))
            .Match((company, city, facts) =>
                from manager in facts.OfType<Manager>()
                where manager.office.company == company
                where manager.office.city == city
                select manager);

    private static readonly Specification<Company, City, Manager> ManagersWhereBothAreNamed =
        Given<Company, City>
            .Where((company, city, facts) => facts.Any<OfficeName>(sharedName =>
                sharedName.office.company == company &&
                sharedName.office.city == city))
            .Match((company, city, facts) =>
                from manager in facts.OfType<Manager>()
                where manager.office.company == company
                where manager.office.city == city
                select manager);

    private static readonly Specification<Company, City, Manager> ManagersWhereEachIsNamed =
        Given<Company, City>
            .Where((company, city, facts) =>
                facts.Any<OfficeName>(companyName => companyName.office.company == company) &&
                facts.Any<OfficeName>(cityName => cityName.office.city == city))
            .Match((company, city, facts) =>
                from manager in facts.OfType<Manager>()
                where manager.office.company == company
                where manager.office.city == city
                select manager);

    /// <summary>
    /// Two <c>Where</c> calls, which are combined with and exactly as the conjuncts of one
    /// predicate are.
    /// </summary>
    private static readonly Specification<Company, City, Manager> ManagersWhereEachIsNamedInTwoCalls =
        Given<Company, City>
            .Where((company, city, facts) => facts.Any<OfficeName>(companyName => companyName.office.company == company))
            .Where((company, city, facts) => facts.Any<OfficeName>(cityName => cityName.office.city == city))
            .Match((company, city, facts) =>
                from manager in facts.OfType<Manager>()
                where manager.office.company == company
                where manager.office.city == city
                select manager);

    [Fact]
    public void GivenCondition_AConditionOnTheFirstGivenAloneAttachesToTheFirstGiven()
    {
        ManagersWhereTheCompanyHasANamedOffice.Givens[0].ExistentialConditions.Should().ContainSingle();
        ManagersWhereTheCompanyHasANamedOffice.Givens[1].ExistentialConditions.Should().BeEmpty();
        ManagersWhereTheCompanyHasANamedOffice.ToDescriptiveString().Should().Be(
            WithConditionsOnGivens(
                ManagersUnconditional,
                ImmutableList.Create(CompanyHasANamedOffice("company")),
                ImmutableList<ExistentialCondition>.Empty).ToDescriptiveString());
    }

    [Fact]
    public void GivenCondition_AConditionOnTheSecondGivenAloneAttachesToTheSecondGiven()
    {
        ManagersWhereTheCityHasANamedOffice.Givens[0].ExistentialConditions.Should().BeEmpty();
        ManagersWhereTheCityHasANamedOffice.Givens[1].ExistentialConditions.Should().ContainSingle();
        ManagersWhereTheCityHasANamedOffice.ToDescriptiveString().Should().Be(
            WithConditionsOnGivens(
                ManagersUnconditional,
                ImmutableList<ExistentialCondition>.Empty,
                ImmutableList.Create(CityHasANamedOffice("city"))).ToDescriptiveString());
    }

    [Fact]
    public void GivenCondition_AConditionJoiningBothGivensAttachesToTheLater()
    {
        ManagersWhereBothAreNamed.Givens[0].ExistentialConditions.Should().BeEmpty();
        ManagersWhereBothAreNamed.Givens[1].ExistentialConditions.Should().ContainSingle();
        ManagersWhereBothAreNamed.ToDescriptiveString().Should().Be(
            WithConditionsOnGivens(
                ManagersUnconditional,
                ImmutableList<ExistentialCondition>.Empty,
                ImmutableList.Create(CompanyHasANamedOfficeInCity("company", "city"))).ToDescriptiveString());
    }

    [Fact]
    public void GivenCondition_TwoConjunctsSplitAcrossTheGivensTheyReference()
    {
        ManagersWhereEachIsNamed.Givens[0].ExistentialConditions.Should().ContainSingle();
        ManagersWhereEachIsNamed.Givens[1].ExistentialConditions.Should().ContainSingle();
        ManagersWhereEachIsNamed.ToDescriptiveString().Should().Be(
            WithConditionsOnGivens(
                ManagersUnconditional,
                ImmutableList.Create(CompanyHasANamedOffice("company")),
                ImmutableList.Create(CityHasANamedOffice("city"))).ToDescriptiveString());
    }

    [Fact]
    public async Task GivenCondition_TwoGivensAnswerAsTheirHandBuiltEquivalent()
    {
        var j = GivenJinagaClient();
        var contoso = await j.Fact(new Company("contoso"));
        var fabrikam = await j.Fact(new Company("fabrikam"));
        var dallas = await j.Fact(new City("Dallas"));
        var austin = await j.Fact(new City("Austin"));
        var contosoDallas = await j.Fact(new Office(contoso, dallas));
        var contosoAustin = await j.Fact(new Office(contoso, austin));
        var fabrikamDallas = await j.Fact(new Office(fabrikam, dallas));
        await j.Fact(new Manager(contosoDallas, 1));

        var handBuilt = WithConditionsOnGivens(
            ManagersUnconditional,
            ImmutableList.Create(CompanyHasANamedOffice("company")),
            ImmutableList.Create(CityHasANamedOffice("city")));

        // Neither conjunct holds, so the specification answers nothing even though its
        // matches have a row.
        (await j.Query(ManagersUnconditional, contoso, dallas)).Should().ContainSingle();
        (await j.Query(ManagersWhereEachIsNamed, contoso, dallas)).Should().BeEmpty();
        (await j.Query(handBuilt, contoso, dallas)).Should().BeEmpty();

        // Naming an Austin office of Contoso satisfies the condition on the company and
        // leaves the one on the city unsatisfied.
        await j.Fact(new OfficeName(contosoAustin, "Contoso Austin", []));
        (await j.Query(ManagersWhereEachIsNamed, contoso, dallas)).Should().BeEmpty();
        (await j.Query(handBuilt, contoso, dallas)).Should().BeEmpty();

        // Naming a Dallas office of Fabrikam satisfies the condition on the city.
        await j.Fact(new OfficeName(fabrikamDallas, "Fabrikam Dallas", []));
        (await j.Query(ManagersWhereEachIsNamed, contoso, dallas)).Should().ContainSingle();
        (await j.Query(handBuilt, contoso, dallas)).Should().ContainSingle();
    }

    [Fact]
    public void GivenCondition_RepeatedWhereCallsAreCombinedWithAnd()
    {
        ManagersWhereEachIsNamedInTwoCalls.ToDescriptiveString()
            .Should().Be(ManagersWhereEachIsNamed.ToDescriptiveString());
    }

    /// <summary>
    /// A given carries existential conditions only, so a conjunct that compares two labels
    /// directly has nowhere to go. It is refused where the predicate is translated, which is
    /// the <c>Match</c>, because that is where the givens get their labels.
    /// </summary>
    [Fact]
    public void GivenCondition_AConjunctComparingTwoLabelsIsRefused()
    {
        Action act = () => Given<Office, Company>
            .Where((office, company, facts) => office.company == company)
            .Match((office, company, facts) =>
                from manager in facts.OfType<Manager>()
                where manager.office == office
                select manager);

        act.Should().Throw<SpecificationException>()
            .WithMessage("*must be an existential condition*");
    }

    // ----- A watch whose given stops qualifying ---------------------------------------

    /// <summary>
    /// The condition on the given reaches the observer through the inverse port's reduction
    /// of given conditions, so a fact that falsifies it has to remove rows that no match of
    /// the specification mentions.
    /// </summary>
    [Fact]
    public async Task GivenCondition_ClosingTheOfficeRemovesEveryRow()
    {
        var j = GivenJinagaClient();
        var company = await j.Fact(new Company("contoso"));
        var dallas = await j.Fact(new City("Dallas"));
        var office = await j.Fact(new Office(company, dallas));
        await j.Fact(new Manager(office, 1));
        await j.Fact(new Manager(office, 2));

        var managers = new List<Manager>();
        var removals = 0;
        var watch = j.Watch(ManagersOfAnOpenOffice, office, (Manager added) =>
        {
            lock (managers)
            {
                managers.Add(added);
            }
            return () =>
            {
                lock (managers)
                {
                    managers.Remove(added);
                }
                Interlocked.Increment(ref removals);
            };
        });

        try
        {
            await watch.Loaded;
            lock (managers)
            {
                managers.Should().HaveCount(2);
            }

            await j.Fact(new OfficeClosure(office, DateTime.UtcNow));

            lock (managers)
            {
                managers.Should().BeEmpty();
            }
            removals.Should().Be(2);
            (await j.Query(ManagersOfAnOpenOffice, office)).Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    /// <summary>
    /// An observer can start on a given that is not yet stored. When the given arrives with
    /// its condition already failing, nothing is delivered -- neither for the given's own
    /// arrival nor for the rows that arrive after it.
    /// </summary>
    [Fact]
    public async Task GivenCondition_SavingTheGivenWhileTheConditionFailsDeliversNothing()
    {
        var j = GivenJinagaClient();
        var company = await j.Fact(new Company("contoso"));
        var dallas = await j.Fact(new City("Dallas"));
        var office = new Office(company, dallas);

        var managers = new List<Manager>();
        var removals = 0;
        var watch = j.Watch(ManagersOfAnOpenOffice, office, (Manager added) =>
        {
            lock (managers)
            {
                managers.Add(added);
            }
            return () =>
            {
                lock (managers)
                {
                    managers.Remove(added);
                }
                Interlocked.Increment(ref removals);
            };
        });

        try
        {
            await watch.Loaded;

            // Saving the closure saves the office with it, so the given arrives already
            // closed. The manager then arrives under a given that does not qualify.
            await j.Fact(new OfficeClosure(office, DateTime.UtcNow));
            await j.Fact(new Manager(office, 1));

            lock (managers)
            {
                managers.Should().BeEmpty();
            }
            removals.Should().Be(0);
            (await j.Query(ManagersOfAnOpenOffice, office)).Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    // ----- A predicate that constrains nothing ---------------------------------------

    [Fact]
    public void GivenCondition_APredicateThatReferencesNoGivenIsRefused()
    {
        Action act = () => Given<Office>
            .Where((office, facts) => facts.OfType<OfficeClosure>().Any());

        act.Should().Throw<SpecificationException>()
            .WithMessage("*does not reference any given*");
    }

    [Fact]
    public void GivenCondition_AConjunctThatReferencesNoGivenIsRefused()
    {
        Action act = () => Given<Office>
            .Where((office, facts) =>
                !facts.Any<OfficeClosure>(closure => closure.office == office) &&
                facts.OfType<Manager>().Any());

        act.Should().Throw<SpecificationException>()
            .WithMessage("*does not reference any given*");
    }

    // ----- Specifications written without Where are unchanged ------------------------

    [Fact]
    public void GivenCondition_ASpecificationWithoutWhereIsTranslatedAsBefore()
    {
        var handBuilt = new Specification(
            ImmutableList.Create(new SpecificationGiven(
                new Label("office", OfficeType),
                ImmutableList<ExistentialCondition>.Empty)),
            ImmutableList.Create(new Match(
                new Label("manager", "Corporate.Manager"),
                ImmutableList.Create(new PathCondition(
                    ImmutableList.Create(new Role("office", OfficeType)),
                    "office",
                    ImmutableList<Role>.Empty)),
                ImmutableList<ExistentialCondition>.Empty)),
            new SimpleProjection("manager", typeof(Manager)));
        var givenTuple = FactReferenceTuple.Empty
            .Add("office", new FactReference(OfficeType, "irrelevant"));

        ManagersOfAnOpenOfficeUnconditional.ToDescriptiveString()
            .Should().Be(handBuilt.ToDescriptiveString());
        IdentityUtilities.ComputeSpecificationHash(ManagersOfAnOpenOfficeUnconditional, givenTuple)
            .Should().Be(IdentityUtilities.ComputeSpecificationHash(handBuilt, givenTuple));
    }
}
