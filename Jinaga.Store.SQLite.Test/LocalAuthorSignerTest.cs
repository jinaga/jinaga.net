using Jinaga.Cryptography;
using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Services;
using Jinaga.Store.SQLite.Test.Fakes;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// The authorship record lives in the <c>fact_author</c> table, written when a fact is
/// queued and never serialized. Provenance reads it beside the <c>signature</c> table, so a
/// fact this device created is attributed to the user who created it even though no
/// replicator has signed it yet.
/// </summary>
public class LocalAuthorSignerTest
{
    /// <summary>
    /// Long enough that the debounce never elapses during a test, so nothing is uploaded and
    /// the facts stay queued with their authorship records intact.
    /// </summary>
    private const int DelayLongerThanAnyTest = 60000;

    public record PostSigners(Post post, IEnumerable<User> signers);

    private static readonly Specification<Site, PostSigners> postsWithSigners =
        Given<Site>.Match((site, facts) =>
            from post in facts.OfType<Post>()
            where post.site == site
            select new PostSigners(post, facts.SignersOf(post))
        );

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "LocalAuthorSignerTest.db");

    public LocalAuthorSignerTest()
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);
    }

    [Fact]
    public async Task SignersOf_ProjectsTheUserWhoSavedTheFact()
    {
        var (j, store, identity) = CreateClient();
        var author = new User(KeyPair.Generate().PublicKey);
        identity.SetCurrentUser(author);
        var site = await j.Fact(new Site("michaelperry.net"));
        await j.Fact(new Post(site, "2026-10-13T00:00:00Z"));

        var results = await j.Query(postsWithSigners, site);

        SignersIn(results).Should().Equal(author.publicKey);

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_WhenTheAuthorsOwnSignatureArrives_StillProjectsOneSigner()
    {
        var (j, store, identity) = CreateClient();
        var authorKey = KeyPair.Generate();
        identity.SetCurrentUser(new User(authorKey.PublicKey));
        var site = await j.Fact(new Site("michaelperry.net"));
        var post = await j.Fact(new Post(site, "2026-10-13T00:00:00Z"));

        await SignFact(j, store, post, authorKey);

        var results = await j.Query(postsWithSigners, site);

        SignersIn(results).Should().Equal(authorKey.PublicKey);

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_UnionsAStoredSignatureWithTheLocalAuthor()
    {
        var (j, store, identity) = CreateClient();
        var author = new User(KeyPair.Generate().PublicKey);
        identity.SetCurrentUser(author);
        var site = await j.Fact(new Site("michaelperry.net"));
        var post = await j.Fact(new Post(site, "2026-10-13T00:00:00Z"));
        var otherKey = KeyPair.Generate();

        await SignFact(j, store, post, otherKey);

        var results = await j.Query(postsWithSigners, site);

        SignersIn(results).Should().BeEquivalentTo(
            new[] { otherKey.PublicKey, author.publicKey });

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_WithNoCurrentUserAndNoSignatures_ProjectsNoSigners()
    {
        var (j, store, identity) = CreateClient();
        var site = await j.Fact(new Site("michaelperry.net"));
        await j.Fact(new Post(site, "2026-10-13T00:00:00Z"));

        var results = await j.Query(postsWithSigners, site);

        SignersIn(results).Should().BeEmpty();

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_AfterReopeningTheStore_StillProjectsTheLocalAuthor()
    {
        var author = new User(KeyPair.Generate().PublicKey);
        Site site;
        {
            var (j, _, identity) = CreateClient();
            identity.SetCurrentUser(author);
            site = await j.Fact(new Site("michaelperry.net"));
            await j.Fact(new Post(site, "2026-10-13T00:00:00Z"));
            await j.Unload();
        }

        // A new store over the same file, so the record is read back from fact_author
        // rather than from anything the first client held in memory.
        var (reopened, _, reopenedIdentity) = CreateClient();
        reopenedIdentity.SetCurrentUser(author);

        var results = await reopened.Query(postsWithSigners, site);

        SignersIn(results).Should().Equal(author.publicKey);

        await reopened.Unload();
    }

    private static IEnumerable<string> SignersIn(IEnumerable<PostSigners> results)
    {
        return results.Should().ContainSingle().Subject
            .signers.Select(signer => signer.publicKey)
            .ToImmutableList();
    }

    /// <summary>
    /// Saves the signatures beside a fact that is already in the store, the way a replica
    /// learns them: the envelope carries them, and the fact itself is untouched.
    /// </summary>
    private static async Task SignFact<TFact>(JinagaClient j, SQLiteStore store, TFact prototype, params KeyPair[] keyPairs)
    {
        var graph = j.Graph(prototype);
        var fact = graph.GetFact(graph.Last);
        var signatures = keyPairs
            .Select(keyPair => keyPair.SignFact(fact))
            .ToImmutableList();
        await ((IStore)store).Save(
            FactGraph.Empty.Add(new FactEnvelope(fact, signatures)),
            false,
            CancellationToken.None);
    }

    private static (JinagaClient j, SQLiteStore store, FakeIdentitySource identity) CreateClient()
    {
        var store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        var identity = new FakeIdentitySource();
        var options = new JinagaClientOptions
        {
            HttpAuthenticationProvider = identity,
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        var j = new JinagaClient(
            store,
            new LocalNetwork(),
            [],
            NullLoggerFactory.Instance,
            options);
        return (j, store, identity);
    }
}
