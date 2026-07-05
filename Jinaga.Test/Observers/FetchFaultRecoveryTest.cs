using System.Linq;
using Jinaga.Extensions;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Jinaga.Test.Observers;

// Regression test for https://github.com/jinaga/jinaga.net/issues/179
// A transient failure fetching a feed must not permanently disable that
// feed for the lifetime of the client. NetworkManager.ProcessFeed must
// remove the feed from activeFeeds on every exit path, including
// exceptions, so a later fetch can retry.
public class FetchFaultRecoveryTest
{
    private readonly ITestOutputHelper output;

    public FetchFaultRecoveryTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task Query_RecoversAfterTransientFeedFailure()
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

        var officesInCompany = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company
            select office
        );

        // NetworkManager.Fetch already retries a single transient failure once
        // internally (see issue #180), so queue two consecutive failures here to
        // exhaust that retry and still observe the exception surfacing to the caller.
        network.FailNextFetch("offices", new InvalidOperationException("Simulated transient network failure"));
        network.FailNextFetch("offices", new InvalidOperationException("Simulated transient network failure"));

        // The first query should surface the transient failure once both the
        // original attempt and the internal retry have failed.
        Func<Task> firstAttempt = async () => await j.Query(officesInCompany, contoso);
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>();

        // A subsequent query must retry the feed rather than replaying the
        // cached faulted task forever.
        var offices = await j.Query(officesInCompany, contoso);

        offices.Should().ContainSingle().Which.Should().BeEquivalentTo(dallasOffice);
    }

    private static JinagaClient GivenJinagaClient(FakeNetwork network)
    {
        var options = new JinagaClientOptions();
        return new JinagaClient(new MemoryStore(), network, [], NullLoggerFactory.Instance, options);
    }
}
