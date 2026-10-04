using Jinaga.Extensions;
using Jinaga.Http;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Jinaga.Test.Managers;

/// <summary>
/// A subscriber asks its owner to declare the feed again when the replicator has forgotten
/// it. NetworkManager is that owner: it evicts the cached feed list for the specification
/// before declaring it, so neither the subscriber nor a concurrent Fetch or Subscribe of the
/// same specification reuses a feed the replicator no longer knows.
/// </summary>
public class NetworkManagerFeedRegistrationTest
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static readonly Specification<Company, Office> officesInCompany = Given<Company>.Match((company, facts) =>
        from office in facts.OfType<Office>()
        where office.company == company
        select office
    );

    [Fact]
    public async Task Query_WhenADeclarationIsCancelled_DoesNotLeaveItInTheFeedCache()
    {
        // A declaration made for a subscriber carries that subscriber's connection token, which
        // its refresh timer cancels every few minutes. A cancelled task is not a faulted one, so
        // the feed cache has to recognize it as one it must not serve.
        var network = new ScriptedStreamNetwork { Feed = "offices", CancelNextDeclaration = true };
        var options = new JinagaClientOptions();
        var j = new JinagaClient(new MemoryStore(), network, [], NullLoggerFactory.Instance, options);
        var contoso = new Company("contoso");

        var offices = await j.Query(officesInCompany, contoso);

        Assert.Empty(offices);
        Assert.Equal(2, network.FeedsCallCount);
    }

    [Fact]
    public async Task Subscribe_WhenTheReplicatorForgetsTheFeed_DeclaresItAgain()
    {
        var network = new ScriptedStreamNetwork { Feed = "offices" };
        var options = new JinagaClientOptions();
        var j = new JinagaClient(new MemoryStore(), network, [], NullLoggerFactory.Instance, options);
        var contoso = new Company("contoso");

        var watch = j.Subscribe(officesInCompany, contoso, office => { });
        try
        {
            await watch.Loaded;
            Assert.Equal(1, network.FeedsCallCount);

            network.LoseRegistration();
            network.RaiseErrorOnLatestConnection(new FeedNotFoundException("offices"));

            // The feed list was declared a second time, which it could not be if the
            // forgotten feed were still cached for this specification.
            await network.FeedsReached(2).WaitAsync(Patience);
            await network.ConnectionReached(2).WaitAsync(Patience);
        }
        finally
        {
            watch.Stop();
        }
    }
}
