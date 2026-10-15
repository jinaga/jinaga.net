using Jinaga.Maui.Authentication;
using Jinaga.Maui.Test.Fakes;
using Jinaga.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;

namespace Jinaga.Maui.Test;

/// <summary>
/// The MAUI authentication service needs the Jinaga client, and the client needs
/// an authentication provider, so a proxy stands in while the service is being
/// constructed. The client reads the identity source out of its provider once,
/// when it is constructed, which is before the proxy has anything to wrap. The
/// proxy therefore has to report the interface itself and forward through it.
/// </summary>
public class AuthenticationProviderProxyTest
{
    private const int DelayLongerThanAnyTest = 60000;

    private static readonly User UserA = new User("---FAKE PUBLIC KEY A---");
    private static readonly User UserB = new User("---FAKE PUBLIC KEY B---");

    [Fact]
    public void CurrentUser_BeforeAProviderIsSet_IsNobody()
    {
        var proxy = new AuthenticationProviderProxy();

        proxy.CurrentUser.Should().BeNull();
    }

    [Fact]
    public void CurrentUser_WithAProviderThatReportsTheUser_IsThatUser()
    {
        var identity = new FakeIdentitySource();
        identity.SetCurrentUser(UserA);
        var proxy = new AuthenticationProviderProxy();

        proxy.SetProvider(identity);

        proxy.CurrentUser.Should().Be(UserA);
    }

    [Fact]
    public void CurrentUser_WithAProviderThatDoesNotReportTheUser_IsNobody()
    {
        var proxy = new AuthenticationProviderProxy();

        proxy.SetProvider(new FakeAuthenticationProviderWithoutIdentity());

        proxy.CurrentUser.Should().BeNull();
    }

    [Fact]
    public void OnCurrentUserChanged_WhenTheWrappedProviderChangesUser_CarriesTheNewUser()
    {
        var identity = new FakeIdentitySource();
        var proxy = new AuthenticationProviderProxy();
        proxy.SetProvider(identity);

        var observed = new List<User?>();
        proxy.OnCurrentUserChanged += user => observed.Add(user);

        identity.SetCurrentUser(UserA);
        identity.SetCurrentUser(null);

        observed.Should().Equal(UserA, null);
        proxy.CurrentUser.Should().BeNull();
    }

    /// <summary>
    /// The whole point of the forwarding: a client constructed with the proxy,
    /// before the proxy wraps anything, still routes the queue by who saved each fact.
    /// </summary>
    [Fact]
    public async Task Push_ThroughAProxyWiredAfterTheClientWasBuilt_HoldsTheOtherUsersFact()
    {
        var proxy = new AuthenticationProviderProxy();
        var store = new PersistentMemoryStore();
        var network = new UploadRecordingNetwork();
        var options = new JinagaClientOptions
        {
            HttpAuthenticationProvider = proxy,
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        var j = new JinagaClient(
            store,
            network,
            ImmutableList<Specification>.Empty,
            NullLoggerFactory.Instance,
            options);

        // The authentication service is constructed after the client, and hands
        // itself to the proxy at that point.
        var identity = new FakeIdentitySource();
        proxy.SetProvider(identity);

        identity.SetCurrentUser(UserA);
        var schoolA = await j.Fact(new School(Guid.NewGuid()));
        identity.SetCurrentUser(UserB);
        var schoolB = await j.Fact(new School(Guid.NewGuid()));

        await j.Push();

        var uploaded = network.UploadedGraph.FactReferences
            .Select(reference => reference.Hash)
            .ToImmutableList();
        uploaded.Should().Contain(j.Hash(schoolB));
        uploaded.Should().NotContain(j.Hash(schoolA));
    }
}
