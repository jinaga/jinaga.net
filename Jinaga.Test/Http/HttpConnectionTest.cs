using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Jinaga.Http;
using Jinaga.Records;
using Jinaga.Test.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using WebClient = Jinaga.Http.WebClient;

namespace Jinaga.Test.Http;

/// <summary>
/// Verifies that <see cref="HttpConnection"/> classifies a replicator's response by its status
/// code rather than leaving the status to be recovered from an exception message.
/// See https://github.com/jinaga/jinaga.net/issues/202.
/// </summary>
public class HttpConnectionTest
{
    /// <summary>
    /// 419 is not a member of <see cref="HttpStatusCode"/>. Deployments use it to signal an
    /// expired token, and jinaga.js re-authenticates on it alongside 401 and 407.
    /// </summary>
    private const HttpStatusCode PageExpired = (HttpStatusCode)419;

    private static readonly Uri ReplicatorUrl = new Uri("https://replicator.example.com/");

    private const string FeedHash = "kEsoWQKhBbqKWhGSNCNHTBPR9wM9lXjMOcSCvNvGLUQ";

    public static TheoryData<HttpStatusCode> StaleCredentialStatuses => new TheoryData<HttpStatusCode>
    {
        HttpStatusCode.Unauthorized,
        HttpStatusCode.ProxyAuthenticationRequired,
        PageExpired
    };

    [Theory]
    [MemberData(nameof(StaleCredentialStatuses))]
    public async Task Login_ReauthenticatesOnce_WhenTheServerSaysTheCredentialIsStale(HttpStatusCode staleCredential)
    {
        var harness = GivenHarness(StaleOnFirst(staleCredential, Is(HttpMethod.Get, "/login")));

        var response = await harness.WebClient.Login(CancellationToken.None);

        response.Profile.DisplayName.Should().Be("Ada Lovelace");
        harness.Handler.CountOf(HttpMethod.Get, "/login").Should().Be(2);
        harness.ReauthenticateCount.Should().Be(1);
        harness.AuthenticationStates.Should().Equal(JinagaAuthenticationState.Authenticated);
    }

    [Theory]
    [MemberData(nameof(StaleCredentialStatuses))]
    public async Task Save_ReauthenticatesOnce_WhenTheServerSaysTheCredentialIsStale(HttpStatusCode staleCredential)
    {
        var harness = GivenHarness(StaleOnFirst(staleCredential, Is(HttpMethod.Post, "/save")));

        await harness.WebClient.Save(Jinaga.Facts.FactGraph.Empty);

        harness.Handler.CountOf(HttpMethod.Post, "/save").Should().Be(2);
        harness.ReauthenticateCount.Should().Be(1);
        harness.AuthenticationStates.Should().Equal(JinagaAuthenticationState.Authenticated);
    }

    [Theory]
    [MemberData(nameof(StaleCredentialStatuses))]
    public async Task Feeds_ReauthenticatesOnce_WhenTheServerSaysTheCredentialIsStale(HttpStatusCode staleCredential)
    {
        var harness = GivenHarness(StaleOnFirst(staleCredential, Is(HttpMethod.Post, "/feeds")));

        var response = await harness.WebClient.Feeds("declaration\nspecification");

        response.Feeds.Should().Equal(FeedHash);
        harness.Handler.CountOf(HttpMethod.Post, "/feeds").Should().Be(2);
        harness.ReauthenticateCount.Should().Be(1);
        harness.AuthenticationStates.Should().Equal(JinagaAuthenticationState.Authenticated);
    }

    [Theory]
    [MemberData(nameof(StaleCredentialStatuses))]
    public async Task Feed_ReauthenticatesOnce_WhenTheServerSaysTheCredentialIsStale(HttpStatusCode staleCredential)
    {
        var harness = GivenHarness(StaleOnFirst(staleCredential, Is(HttpMethod.Get, $"/feeds/{FeedHash}")));

        var response = await harness.WebClient.Feed(FeedHash, "bookmark", CancellationToken.None);

        response.bookmark.Should().Be("next-bookmark");
        harness.Handler.CountOf(HttpMethod.Get, $"/feeds/{FeedHash}").Should().Be(2);
        harness.ReauthenticateCount.Should().Be(1);
        harness.AuthenticationStates.Should().Equal(JinagaAuthenticationState.Authenticated);
    }

    [Theory]
    [MemberData(nameof(StaleCredentialStatuses))]
    public async Task Load_ReauthenticatesOnce_WhenTheServerSaysTheCredentialIsStale(HttpStatusCode staleCredential)
    {
        var harness = GivenHarness(StaleOnFirst(staleCredential, Is(HttpMethod.Post, "/load")));

        var graph = await harness.WebClient.Load(new LoadRequest(), CancellationToken.None);

        graph.FactReferences.Should().HaveCount(1);
        harness.Handler.CountOf(HttpMethod.Post, "/load").Should().Be(2);
        harness.ReauthenticateCount.Should().Be(1);
        harness.AuthenticationStates.Should().Equal(JinagaAuthenticationState.Authenticated);
    }

    [Fact]
    public async Task Feeds_ThrowsForbiddenWithTheReason_WhenTheServerRefusesWithJson()
    {
        const string body = "{\"reason\":\"The signatures on the facts are invalid.\"}";
        var harness = GivenHarness(Always(HttpStatusCode.Forbidden, body, "application/json"));

        Func<Task> feeds = () => harness.WebClient.Feeds("declaration\nspecification");

        var thrown = await feeds.Should().ThrowAsync<ForbiddenException>();
        thrown.Which.Reason.Should().Be("The signatures on the facts are invalid.");
        thrown.Which.Body.Should().Be(body);
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Feeds_ThrowsForbiddenWithTheReason_WhenTheServerRefusesWithAMessageProperty()
    {
        const string body = "{\"message\":\"Distribution rules do not permit this specification.\"}";
        var harness = GivenHarness(Always(HttpStatusCode.Forbidden, body, "application/json"));

        Func<Task> feeds = () => harness.WebClient.Feeds("declaration\nspecification");

        var thrown = await feeds.Should().ThrowAsync<ForbiddenException>();
        thrown.Which.Reason.Should().Be("Distribution rules do not permit this specification.");
        thrown.Which.Body.Should().Be(body);
    }

    [Fact]
    public async Task Feeds_ThrowsForbiddenWithTheBodyAsTheReason_WhenTheServerRefusesWithPlainText()
    {
        const string body = "Not authorized to read this fact.";
        var harness = GivenHarness(Always(HttpStatusCode.Forbidden, body, "text/plain"));

        Func<Task> feeds = () => harness.WebClient.Feeds("declaration\nspecification");

        var thrown = await feeds.Should().ThrowAsync<ForbiddenException>();
        thrown.Which.Reason.Should().Be(body);
        thrown.Which.Body.Should().Be(body);
    }

    [Fact]
    public async Task Feed_ThrowsFeedNotFoundNamingTheFeed_WhenTheRegistrationIsGone()
    {
        var harness = GivenHarness(Always(HttpStatusCode.NotFound, "feed_not_found", "text/plain"));

        Func<Task> feed = () => harness.WebClient.Feed(FeedHash, "bookmark", CancellationToken.None);

        var thrown = await feed.Should().ThrowAsync<FeedNotFoundException>();
        thrown.Which.Feed.Should().Be(FeedHash);
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Feed_ThrowsTheBaseExceptionCarryingTheStatus_WhenA404IsNotAMissingFeed()
    {
        const string body = "The replicator is not configured to serve feeds.";
        var harness = GivenHarness(Always(HttpStatusCode.NotFound, body, "text/plain"));

        Func<Task> feed = () => harness.WebClient.Feed(FeedHash, "bookmark", CancellationToken.None);

        var thrown = await feed.Should().ThrowAsync<JinagaHttpException>();
        thrown.Which.Should().NotBeOfType<FeedNotFoundException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        thrown.Which.Body.Should().Be(body);
    }

    [Fact]
    public async Task Login_ThrowsTheBaseExceptionCarryingTheStatus_WhenTheServerFails()
    {
        const string body = "upstream database unavailable";
        var harness = GivenHarness(Always(HttpStatusCode.InternalServerError, body, "text/plain"));

        Func<Task> login = () => harness.WebClient.Login(CancellationToken.None);

        var thrown = await login.Should().ThrowAsync<JinagaHttpException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        thrown.Which.Body.Should().Be(body);
    }

    private static Func<HttpRequestMessage, int, HttpResponseMessage> Always(HttpStatusCode status, string body, string contentType) =>
        (request, index) => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType)
        };

    private static Func<HttpRequestMessage, bool> Is(HttpMethod method, string absolutePath) =>
        request => request.Method == method && request.RequestUri!.AbsolutePath == absolutePath;

    /// <summary>
    /// Answers the first request matching <paramref name="isTarget"/> with
    /// <paramref name="status"/>, and every other request successfully. The retry that follows a
    /// re-authentication therefore succeeds.
    /// </summary>
    private static Func<HttpRequestMessage, int, HttpResponseMessage> StaleOnFirst(HttpStatusCode status, Func<HttpRequestMessage, bool> isTarget)
    {
        bool alreadySent = false;
        return (request, index) =>
        {
            if (!alreadySent && isTarget(request))
            {
                alreadySent = true;
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent("", Encoding.UTF8, "text/plain")
                };
            }
            return Success(request);
        };
    }

    private static HttpResponseMessage Success(HttpRequestMessage request)
    {
        string path = request.RequestUri!.AbsolutePath;

        if (request.Method == HttpMethod.Options)
        {
            var options = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("", Encoding.UTF8, "text/plain")
            };
            options.Headers.Add("Accept-Post", "application/x-jinaga-graph-v1");
            return options;
        }

        if (path == "/login")
        {
            return Json(new LoginResponse
            {
                UserFact = new FactRecord
                {
                    Type = "Jinaga.User",
                    Hash = "userHash",
                    Fields = new Dictionary<string, Records.FieldValue>
                    {
                        ["publicKey"] = Records.FieldValue.From("---PUBLIC KEY---")
                    }
                },
                Profile = new ProfileRequest { DisplayName = "Ada Lovelace" }
            });
        }

        if (path == "/feeds" && request.Method == HttpMethod.Post)
        {
            return Json(new FeedsResponse { Feeds = new List<string> { FeedHash } });
        }

        if (path.StartsWith("/feeds/") && request.Method == HttpMethod.Get)
        {
            return Json(new FeedResponse
            {
                references = new List<Records.FactReference>
                {
                    new Records.FactReference { Type = "Jinaga.User", Hash = "userHash" }
                },
                bookmark = "next-bookmark"
            });
        }

        if (path == "/load")
        {
            return Json(new LoadResponse
            {
                Facts = new List<FactRecord>
                {
                    new FactRecord
                    {
                        Type = "Jinaga.User",
                        Hash = "userHash",
                        Fields = new Dictionary<string, Records.FieldValue>
                        {
                            ["publicKey"] = Records.FieldValue.From("---PUBLIC KEY---")
                        }
                    }
                }
            });
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("", Encoding.UTF8, "text/plain")
        };
    }

    private static HttpResponseMessage Json<T>(T body) =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(MessageSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

    private static Harness GivenHarness(
        Func<HttpRequestMessage, int, HttpResponseMessage> respond,
        JinagaAuthenticationState reauthenticationResult = JinagaAuthenticationState.Authenticated)
    {
        var harness = new Harness();
        harness.Handler = new FakeHttpMessageHandler(respond);
        var connection = new HttpConnection(
            harness.Handler,
            ReplicatorUrl,
            NullLoggerFactory.Instance,
            headers => { },
            () =>
            {
                harness.ReauthenticateCount++;
                return Task.FromResult(reauthenticationResult);
            },
            state => harness.AuthenticationStates.Add(state),
            new RetryConfiguration { Enabled = false });
        harness.WebClient = new WebClient(connection);
        return harness;
    }

    private sealed class Harness
    {
        public FakeHttpMessageHandler Handler { get; set; } = null!;
        public WebClient WebClient { get; set; } = null!;
        public int ReauthenticateCount { get; set; }
        public List<JinagaAuthenticationState> AuthenticationStates { get; } = new List<JinagaAuthenticationState>();
    }
}
