using Jinaga.Cryptography;
using Jinaga.DefaultImplementations;
using Jinaga.Facts;
using Jinaga.Services;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// Signatures live in the <c>signature</c> table, keyed by fact and public key, so a fact
/// signed by two keys is still one row in <c>fact</c>. These tests read the signers back
/// through a specification to show that the store returns them and the hash does not move.
/// </summary>
public class SignerProjectionTest
{
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
        "SignerProjectionTest.db");

    private readonly SQLiteStore store;
    private readonly JinagaClient j;

    public SignerProjectionTest()
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        j = new JinagaClient(store, new LocalNetwork(), [], NullLoggerFactory.Instance, new JinagaClientOptions());
    }

    [Fact]
    public async Task SignersOf_ProjectsEveryKeyThatSigned()
    {
        var site = await j.Fact(new Site("michaelperry.net"));
        var post = await j.Fact(new Post(site, "2026-10-13T00:00:00Z"));
        // The hash of the same fact as it was before anybody signed it. Computed from the
        // prototype, so it moves only if the identity rules themselves move.
        var hashBeforeSigning = j.Hash(new Post(site, "2026-10-13T00:00:00Z"));
        var first = KeyPair.Generate();
        var second = KeyPair.Generate();
        await SignFact(post, first, second);

        var results = await j.Query(postsWithSigners, site);

        var result = results.Should().ContainSingle().Subject;
        result.signers.Select(signer => signer.publicKey)
            .Should().BeEquivalentTo(new[] { first.PublicKey, second.PublicKey });
        j.Hash(result.post).Should().Be(hashBeforeSigning);

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_UnsignedFactProjectsNoSigners()
    {
        var site = await j.Fact(new Site("michaelperry.net"));
        await j.Fact(new Post(site, "2026-10-13T00:00:00Z"));

        var results = await j.Query(postsWithSigners, site);

        results.Should().ContainSingle().Subject
            .signers.Should().BeEmpty();

        await j.Unload();
    }

    /// <summary>
    /// Saves the signatures beside a fact that is already in the store, the way a replica
    /// learns them: the envelope carries them, and the fact itself is untouched.
    /// </summary>
    private async Task SignFact<TFact>(TFact prototype, params KeyPair[] keyPairs)
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
}
