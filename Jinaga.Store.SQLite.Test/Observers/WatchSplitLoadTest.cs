using Jinaga.Observers;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test.Observers;

/// <summary>
/// A watch loaded from a network whose feeds answer far enough apart that the facts arrive in
/// two saved batches, against the SQLite store.
///
/// The parent and its children land in one batch when both feeds answer inside
/// <c>LoadBatch</c>'s window, and the parent's own decision then reads the children along
/// with it. Split the batches and each child is decided on its own, by a read of the child's
/// specification with the child pinned. That read is the one the store had to learn to
/// render: pinning a fact reached through an edge once produced SQL that selected and
/// filtered a column no table in it supplied, the decision threw
/// <c>no such column</c>, and <c>NetworkManager.Fetch</c>'s retry found the facts already
/// known and completed the load with the child never delivered.
///
/// The failure was intermittent on an unsplit feed, so the split is forced here rather than
/// waited for, and the run is repeated: an exception swallowed by a retry shows up as a
/// missing child in some fraction of runs, not in all of them.
/// </summary>
public class WatchSplitLoadTest
{
    private readonly ITestOutputHelper output;

    public WatchSplitLoadTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    /// <summary>
    /// Past <c>LoadBatch</c>'s 100 ms window, so the office and its names are saved in
    /// separate batches and every name is decided by a pinned read of its own.
    /// </summary>
    private const int NamesFeedDelay = 250;

    [Fact]
    public async Task Watch_OfficeAndNamesInSeparateBatches_DeliversTheCurrentName()
    {
        for (int run = 0; run < 20; run++)
        {
            await LoadOneOfficeWithThreeNames(run);
        }
    }

    private async Task LoadOneOfficeWithThreeNames(int run)
    {
        var network = new FakeNetwork(output);
        var contoso = new Company("contoso");
        var dallas = new City("Dallas");
        var dallasOffice = new Office(contoso, dallas);
        var first = new OfficeName(dallasOffice, "Dallas One", new OfficeName[0]);
        var second = new OfficeName(dallasOffice, "Dallas Two", new OfficeName[] { first });
        var third = new OfficeName(dallasOffice, "Dallas Three", new OfficeName[] { second });
        network.AddFeed("offices", new object[] { dallasOffice }, 1);
        network.AddFeed("officeNames", new object[] { first, second, third }, NamesFeedDelay);

        var path = DatabasePath(run);
        var j = GivenJinagaClient(path, network);
        var viewModel = new CompanyViewModel();
        var watch = viewModel.Load(j, "contoso");

        try
        {
            await watch.Loaded;
            viewModel.Offices.Should().ContainSingle($"run {run} should hold the office")
                .Which.Name.Should().Be("Dallas Three", $"run {run} should hold its current name");
        }
        finally
        {
            watch.Stop();
            await j.Unload();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// A database of its own per run, so a run that holds a name decides it from its own
    /// store rather than from what an earlier run left behind.
    /// </summary>
    private static string DatabasePath(int run) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        $"WatchSplitLoadTest-{run}-{Guid.NewGuid():N}.db");

    private static JinagaClient GivenJinagaClient(string path, FakeNetwork network)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var options = new JinagaClientOptions();
        return new JinagaClient(
            new SQLiteStore(path, NullLoggerFactory.Instance),
            network,
            [],
            NullLoggerFactory.Instance,
            options);
    }

    private class OfficeViewModel
    {
        public required Office Office;
        public string? Name;
    }

    private class CompanyViewModel
    {
        public List<OfficeViewModel> Offices = new();

        public IObserver Load(JinagaClient j, string identifier)
        {
            var officesInCompany = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where !office.IsClosed

                select new
                {
                    office,
                    names = facts.Observable(
                        from name in facts.OfType<OfficeName>()
                        where name.office == office &&
                            !facts.Any<OfficeName>(next => next.prior.Contains(name))
                        select name.value
                    )
                }
            );

            var company = new Company(identifier);
            return j.Watch(officesInCompany, company, projection =>
            {
                var officeViewModel = new OfficeViewModel
                {
                    Office = projection.office
                };
                Offices.Add(officeViewModel);

                projection.names.OnAdded(name =>
                {
                    officeViewModel.Name = name;
                });

                return () =>
                {
                    Offices.Remove(officeViewModel);
                };
            });
        }
    }
}
