#nullable enable

using Jinaga.Http;
using Jinaga.Projections;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace Jinaga.Test.Http;

/// <summary>
/// The queue chooses the facts to send by who is logged in when it starts, and the
/// token goes on each request when it is sent. A retry can run for half a minute, so
/// a user who logs in during one, or between two batches, would otherwise carry the
/// facts chosen for somebody else up under their own token. Every attempt to send
/// checks that the user it was chosen for is still the one logged in, and sends
/// nothing if not.
/// </summary>
public class UploaderChangeTest
{
    private const int DelayLongerThanAnyTest = 60000;

    private static readonly Uri ReplicatorUrl = new Uri("https://replicator.example.com/");

    private static readonly User UserA = new User("---FAKE PUBLIC KEY A---");
    private static readonly User UserB = new User("---FAKE PUBLIC KEY B---");

    [Fact]
    public async Task Push_WhenAnotherUserLogsInDuringARetry_DoesNotSendTheRetry()
    {
        var identity = new FakeIdentitySource();
        var handler = new FakeHttpMessageHandler((request, index) =>
        {
            if (request.Method == HttpMethod.Options)
            {
                return AcceptsGraphs();
            }

            // The first attempt cannot connect, and while it is failing B logs in.
            identity.SetCurrentUser(UserB);
            throw new HttpRequestException(
                "Connection refused",
                new SocketException((int)SocketError.ConnectionRefused));
        });
        var (j, store) = GivenClient(handler, identity);

        identity.SetCurrentUser(UserA);
        var school = await j.Fact(new School(Guid.NewGuid()));

        await j.Push();

        handler.CountOf(HttpMethod.Post, "/save").Should().Be(1);
        QueuedHashes(store).Should().Equal(j.Hash(school));
    }

    [Fact]
    public async Task Push_WhenTheTokenIsRefreshedForTheSameUser_SendsTheRetry()
    {
        var identity = new FakeIdentitySource();
        identity.OnReauthenticate = () => Task.FromResult(JinagaAuthenticationState.Authenticated);
        bool rejected = false;
        var handler = new FakeHttpMessageHandler((request, index) =>
        {
            if (request.Method == HttpMethod.Options)
            {
                return AcceptsGraphs();
            }
            if (!rejected)
            {
                rejected = true;
                return Status(HttpStatusCode.Unauthorized);
            }
            return Status(HttpStatusCode.Created);
        });
        var (j, store) = GivenClient(handler, identity);

        identity.SetCurrentUser(UserA);
        await j.Fact(new School(Guid.NewGuid()));

        await j.Push();

        handler.CountOf(HttpMethod.Post, "/save").Should().Be(2);
        QueuedHashes(store).Should().BeEmpty();
    }

    [Fact]
    public async Task Push_WhenReauthenticatingLogsInAnotherUser_DoesNotSendTheRetry()
    {
        var identity = new FakeIdentitySource();
        identity.OnReauthenticate = () =>
        {
            identity.SetCurrentUser(UserB);
            return Task.FromResult(JinagaAuthenticationState.Authenticated);
        };
        var handler = new FakeHttpMessageHandler((request, index) =>
        {
            if (request.Method == HttpMethod.Options)
            {
                return AcceptsGraphs();
            }
            return Status(HttpStatusCode.Unauthorized);
        });
        var (j, store) = GivenClient(handler, identity);

        identity.SetCurrentUser(UserA);
        var school = await j.Fact(new School(Guid.NewGuid()));

        await j.Push();

        handler.CountOf(HttpMethod.Post, "/save").Should().Be(1);
        QueuedHashes(store).Should().Equal(j.Hash(school));
    }

    [Fact]
    public async Task Push_WhenAnotherUserLogsInBetweenBatches_DoesNotSendTheNextBatch()
    {
        var identity = new FakeIdentitySource();
        var handler = new FakeHttpMessageHandler((request, index) =>
        {
            if (request.Method == HttpMethod.Options)
            {
                return AcceptsGraphs();
            }

            // The first batch goes through, and B logs in before the second.
            identity.SetCurrentUser(UserB);
            return Status(HttpStatusCode.Created);
        });
        var (j, store) = GivenClient(handler, identity, maxBatchSize: 1);

        identity.SetCurrentUser(UserA);
        await j.Fact(new School(Guid.NewGuid()));
        var second = await j.Fact(new School(Guid.NewGuid()));

        await j.Push();

        handler.CountOf(HttpMethod.Post, "/save").Should().Be(1);
        QueuedHashes(store).Should().Equal(j.Hash(second));
    }

    private static (JinagaClient j, PersistentMemoryStore store) GivenClient(
        FakeHttpMessageHandler handler,
        FakeIdentitySource identity,
        int maxBatchSize = 1000)
    {
        var store = new PersistentMemoryStore();
        var retry = new RetryConfiguration
        {
            InitialDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(1)
        };
        var network = new HttpNetwork(handler, ReplicatorUrl, identity, NullLoggerFactory.Instance, retry);
        var options = new JinagaClientOptions
        {
            HttpAuthenticationProvider = identity,
            QueueProcessingDelay = DelayLongerThanAnyTest,
            MaxBatchSize = maxBatchSize
        };
        var j = new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            options);
        return (j, store);
    }

    private static HttpResponseMessage AcceptsGraphs()
    {
        var response = Status(HttpStatusCode.OK);
        response.Headers.Add("Accept-Post", "application/x-jinaga-graph-v1");
        return response;
    }

    private static HttpResponseMessage Status(HttpStatusCode status) =>
        new HttpResponseMessage(status)
        {
            Content = new StringContent("", Encoding.UTF8, "text/plain")
        };

    private static IEnumerable<string> QueuedHashes(PersistentMemoryStore store)
    {
        var queue = store.GetQueue().GetAwaiter().GetResult();
        return queue.Facts
            .Select(fact => fact.Reference.Hash)
            .ToImmutableList();
    }
}
