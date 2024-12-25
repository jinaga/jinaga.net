using System.Collections.Immutable;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test.Observers;
public class WatchFromNetworkTest
{
    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "WatchFromNetworkTest.db");

    private ITestOutputHelper output;

    public WatchFromNetworkTest(ITestOutputHelper output)
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        this.output = output;
    }

    [Fact]
    public async Task Watch_EmptyUpstream()
    {
        var j = GivenJinagaClient(new FakeNetwork(output));

        var viewModel = new CompanyViewModel();
        var watch = viewModel.Load(j, "contoso");

        try
        {
            await watch.Loaded;
            viewModel.Offices.Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_SingleUnnamedOffice()
    {
        var network = new FakeNetwork(output);
        var contoso = new Company("contoso");
        var dallas = new City("Dallas");
        var dallasOffice = new Office(contoso, dallas);
        network.AddFeed("offices", new object[]
        {
            dallasOffice
        });

        var j = GivenJinagaClient(network);

        var viewModel = new CompanyViewModel();
        var watch = viewModel.Load(j, "contoso");

        try
        {
            await watch.Loaded;
            viewModel.Offices.Should().ContainSingle().Which
                .Name.Should().BeNull();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_SingleNamedOffice()
    {
        var network = new FakeNetwork(output);
        var contoso = new Company("contoso");
        var dallas = new City("Dallas");
        var dallasOffice = new Office(contoso, dallas);
        var dallasOfficeName = new OfficeName(dallasOffice, "Dallas", new OfficeName[0]);
        network.AddFeed("offices", new object[]
        {
            dallasOffice
        }, 1);
        network.AddFeed("officeNames", new object[]
        {
            dallasOfficeName
        }, 1);

        var j = GivenJinagaClient(network);

        var viewModel = new CompanyViewModel();
        var watch = viewModel.Load(j, "contoso");

        try
        {
            await watch.Loaded;
            viewModel.Offices.Should().ContainSingle().Which
                .Name.Should().Be("Dallas");
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_SingleOfficeThreeNames()
    {
        var network = new FakeNetwork(output);
        var contoso = new Company("contoso");
        var dallas = new City("Dallas");
        var dallasOffice = new Office(contoso, dallas);
        var dallasOfficeName1 = new OfficeName(dallasOffice, "Dallas One", new OfficeName[0]);
        var dallasOfficeName2 = new OfficeName(dallasOffice, "Dallas Two", new OfficeName[] { dallasOfficeName1 });
        var dallasOfficeName3 = new OfficeName(dallasOffice, "Dallas Three", new OfficeName[] { dallasOfficeName2 });
        network.AddFeed("offices", new object[]
        {
            dallasOffice
        }, 1);
        network.AddFeed("officeNames", new object[]
        {
            dallasOfficeName1,
            dallasOfficeName2,
            dallasOfficeName3
        }, 1);

        var j = GivenJinagaClient(network);

        var viewModel = new CompanyViewModel();
        var watch = viewModel.Load(j, "contoso");

        try
        {
            await watch.Loaded;
            viewModel.Offices.Should().ContainSingle().Which
                .Name.Should().Be("Dallas Three");
        }
        finally
        {
            watch.Stop();
        }
    }

    private static JinagaClient GivenJinagaClient(FakeNetwork network)
    {
        return new JinagaClient(new SQLiteStore(SQLitePath, NullLoggerFactory.Instance), network, [], NullLoggerFactory.Instance);
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
            var watch = j.Watch(officesInCompany, company, projection =>
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
            return watch;
        }
    }
}
