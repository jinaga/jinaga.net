using Jinaga.Observers;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test.Observers;

/// <summary>
/// A watch whose membership turns on a negation, against the SQLite store.
///
/// Deciding a candidate reads the specification with every unknown of the row pinned, which
/// is a shape the SQLite store had no reason to build before. Its own evaluator renders a
/// specification into SQL rather than walking it, so the shape has to be exercised here and
/// not only against the in-memory store.
/// </summary>
public class WatchThroughNegationTest
{
    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "WatchThroughNegationTest.db");

    private readonly ITestOutputHelper output;

    public WatchThroughNegationTest(ITestOutputHelper output)
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        this.output = output;
    }

    /// <summary>
    /// The offices of a company that have a manager nobody has terminated. The condition's
    /// own level gains and loses solutions, and the office's membership follows it.
    /// </summary>
    private static readonly Specification<Company, Office> OfficesWithACurrentManager =
        Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            where facts.Any<Manager>(manager =>
                manager.office == office &&
                !facts.Any<ManagerTerminated>(terminated => terminated.Manager == manager))
            select office
        );

    [Fact]
    public async Task Watch_TerminatingTheOnlyCurrentManager_RemovesTheOffice()
    {
        var j = GivenJinagaClient();
        var company = await j.Fact(new Company("contoso"));
        var dallas = await j.Fact(new City("Dallas"));
        var office = await j.Fact(new Office(company, dallas));
        var manager = await j.Fact(new Manager(office, 1));

        var offices = new List<Office>();
        var watch = j.Watch(OfficesWithACurrentManager, company, (Office added) =>
        {
            lock (offices)
            {
                offices.Add(added);
            }
            return () =>
            {
                lock (offices)
                {
                    offices.Remove(added);
                }
            };
        });

        try
        {
            await watch.Loaded;
            lock (offices)
            {
                offices.Should().ContainSingle();
            }

            await j.Fact(new ManagerTerminated(manager, DateTime.UtcNow));

            lock (offices)
            {
                offices.Should().BeEmpty();
            }
            (await j.Query(OfficesWithACurrentManager, company)).Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_TerminatingOneOfTwoCurrentManagers_KeepsTheOffice()
    {
        var j = GivenJinagaClient();
        var company = await j.Fact(new Company("contoso"));
        var dallas = await j.Fact(new City("Dallas"));
        var office = await j.Fact(new Office(company, dallas));
        var first = await j.Fact(new Manager(office, 1));
        await j.Fact(new Manager(office, 2));

        var offices = new List<Office>();
        var watch = j.Watch(OfficesWithACurrentManager, company, (Office added) =>
        {
            lock (offices)
            {
                offices.Add(added);
            }
            return () =>
            {
                lock (offices)
                {
                    offices.Remove(added);
                }
            };
        });

        try
        {
            await watch.Loaded;

            var save = async () => await j.Fact(new ManagerTerminated(first, DateTime.UtcNow));

            await save.Should().NotThrowAsync();
            lock (offices)
            {
                offices.Should().ContainSingle();
            }
            (await j.Query(OfficesWithACurrentManager, company)).Should().ContainSingle();
        }
        finally
        {
            watch.Stop();
        }
    }

    private JinagaClient GivenJinagaClient()
    {
        var options = new JinagaClientOptions();
        return new JinagaClient(
            new SQLiteStore(SQLitePath, NullLoggerFactory.Instance),
            new FakeNetwork(output),
            [],
            NullLoggerFactory.Instance,
            options);
    }
}
