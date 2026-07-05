using Jinaga.Extensions;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;

namespace Jinaga.Test.Managers;

/// <summary>
/// Verifies the fix for https://github.com/jinaga/jinaga.net/issues/180: NetworkManager.Fetch
/// and NetworkManager.Subscribe should retry exactly once after a transient network failure
/// before propagating the exception.
/// </summary>
public class NetworkManagerRetryTest
{
    private readonly ITestOutputHelper output;

    private static readonly Specification<Company, Office> officesInCompany = Given<Company>.Match((company, facts) =>
        from office in facts.OfType<Office>()
        where office.company == company
        select office
    );

    public NetworkManagerRetryTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task Query_RetriesOnceAfterTransientFailure_ThenSucceeds()
    {
        var network = new FlakyFakeNetwork(output, failureCount: 1);
        var contoso = new Company("contoso");
        var dallas = new City("Dallas");
        var dallasOffice = new Office(contoso, dallas);
        network.AddFeed("offices", new object[]
        {
            dallasOffice
        });

        var j = GivenJinagaClient(network);

        var offices = await j.Query(officesInCompany, contoso);

        offices.Should().ContainSingle().Which.Should().Be(dallasOffice);
        network.FetchFeedFailureCount.Should().Be(1);
    }

    [Fact]
    public async Task Query_FailsAfterTwoConsecutiveFailures()
    {
        var network = new FlakyFakeNetwork(output, failureCount: 2);
        var contoso = new Company("contoso");
        var dallas = new City("Dallas");
        var dallasOffice = new Office(contoso, dallas);
        network.AddFeed("offices", new object[]
        {
            dallasOffice
        });

        var j = GivenJinagaClient(network);

        Func<Task> query = async () => await j.Query(officesInCompany, contoso);

        await query.Should().ThrowAsync<InvalidOperationException>();
        network.FetchFeedFailureCount.Should().Be(2);
    }

    [Fact]
    public async Task Subscribe_RetriesOnceAfterTransientFailure_ThenSucceeds()
    {
        var network = new FlakyFakeNetwork(output, failureCount: 1);
        var contoso = new Company("contoso");
        var dallas = new City("Dallas");
        var dallasOffice = new Office(contoso, dallas);
        network.AddFeed("offices", new object[]
        {
            dallasOffice
        });

        var j = GivenJinagaClient(network);

        var offices = new List<Office>();
        var watch = j.Subscribe(officesInCompany, contoso, office =>
        {
            offices.Add(office);
        });

        try
        {
            await watch.Loaded;
            offices.Should().ContainSingle().Which.Should().Be(dallasOffice);
            network.StreamFeedFailureCount.Should().Be(1);
        }
        finally
        {
            watch.Stop();
        }
    }

    private static JinagaClient GivenJinagaClient(FlakyFakeNetwork network)
    {
        var options = new JinagaClientOptions();
        return new JinagaClient(new MemoryStore(), network, [], NullLoggerFactory.Instance, options);
    }
}
