using Jinaga.Cryptography;
using Jinaga.Facts;
using Jinaga.Services;
using Jinaga.Store.SQLite.Test.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using Xunit.Abstractions;

namespace Jinaga.Store.SQLite.Test;

/// <summary>
/// The saver relation lives in the <c>fact_saver</c> table, written when a fact is saved
/// and never serialized. Provenance reads it beside the <c>signature</c> table, and counts a
/// saver only when the configured rules would sign the fact with their key, so a fact this
/// device created is attributed to the user who created it before any replicator signs it.
/// </summary>
public class SaverSignerTest
{
    /// <summary>
    /// Long enough that the debounce never elapses during a test, so nothing is uploaded
    /// unless a test asks.
    /// </summary>
    private const int DelayLongerThanAnyTest = 60000;

    private static readonly KeyPair KeyA = KeyPair.Generate();
    private static readonly KeyPair KeyB = KeyPair.Generate();
    private static readonly User UserA = new User(KeyA.PublicKey);
    private static readonly User UserB = new User(KeyB.PublicKey);

    public record PostSigners(SignerPost post, IEnumerable<User> signers);
    public record SiteSigners(SignerSite site, IEnumerable<User> signers);
    public record TagSigners(SignerTag tag, IEnumerable<User> signers);
    public record UserSigners(User user, IEnumerable<User> signers);

    private static readonly Specification<SignerSite, PostSigners> postsWithSigners =
        Given<SignerSite>.Match((site, facts) =>
            from post in facts.OfType<SignerPost>()
            where post.site == site
            select new PostSigners(post, facts.SignersOf(post))
        );

    private static readonly Specification<SignerPost, SiteSigners> siteOfPostWithSigners =
        Given<SignerPost>.Match((post, facts) =>
            from site in facts.OfType<SignerSite>()
            where site == post.site
            select new SiteSigners(site, facts.SignersOf(site))
        );

    private static readonly Specification<SignerSite, TagSigners> tagsWithSigners =
        Given<SignerSite>.Match((site, facts) =>
            from tag in facts.OfType<SignerTag>()
            where tag.site == site
            select new TagSigners(tag, facts.SignersOf(tag))
        );

    private static readonly Specification<SignerSite, UserSigners> membersWithSigners =
        Given<SignerSite>.Match((site, facts) =>
            from member in facts.OfType<SignerMember>()
            where member.site == site
            from user in facts.OfType<User>()
            where user == member.member
            select new UserSigners(user, facts.SignersOf(user))
        );

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "SaverSignerTest.db");

    private readonly ITestOutputHelper output;

    public SaverSignerTest(ITestOutputHelper output)
    {
        this.output = output;
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);
    }

    [Fact]
    public async Task SignersOf_AFactSavedByAnAuthorizedUser_ProjectsTheSaverBeforeAnyUpload()
    {
        var (j, store, network, identity) = CreateClient(SignerModel.Rules);
        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
        var post = await j.Fact(new SignerPost(site, "Hello"));

        var results = await j.Query(postsWithSigners, site);

        network.UploadedUnderUsers.Should().BeEmpty();
        (await store.GetQueue()).Facts.Select(fact => fact.Reference)
            .Should().Contain(j.Graph(post).Last);
        SignersIn(results).Should().Equal(UserA.publicKey);

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_WhenTheSaversOwnSignatureArrives_ProjectsOneSigner()
    {
        var (j, store, _, identity) = CreateClient(SignerModel.Rules);
        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
        var post = await j.Fact(new SignerPost(site, "Hello"));

        await SignFact(j, store, post, KeyA);

        var results = await j.Query(postsWithSigners, site);

        SignersIn(results).Should().Equal(UserA.publicKey);

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_CombinesAStoredSignatureFromAnotherKeyWithTheSaver()
    {
        var (j, store, _, identity) = CreateClient(SignerModel.Rules);
        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
        var post = await j.Fact(new SignerPost(site, "Hello"));
        var otherKey = KeyPair.Generate();

        await SignFact(j, store, post, otherKey);

        var results = await j.Query(postsWithSigners, site);

        SignersIn(results).Should().BeEquivalentTo(
            new[] { otherKey.PublicKey, UserA.publicKey });

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_WithoutRules_ProjectsStoredSignaturesOnly()
    {
        var (j, store, _, identity) = CreateClient(rules: null);
        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
        var post = await j.Fact(new SignerPost(site, "Hello"));

        var beforeSigning = await j.Query(postsWithSigners, site);
        SignersIn(beforeSigning).Should().BeEmpty();

        var otherKey = KeyPair.Generate();
        await SignFact(j, store, post, otherKey);

        var afterSigning = await j.Query(postsWithSigners, site);
        SignersIn(afterSigning).Should().Equal(otherKey.PublicKey);

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_AFactTheRulesAdmitFromAnyone_HasNoSigners()
    {
        var (j, _, _, identity) = CreateClient(SignerModel.Rules);
        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
        await j.Fact(new SignerTag(site, "dotnet"));

        var results = await j.Query(tagsWithSigners, site);

        results.Should().ContainSingle().Subject.signers.Should().BeEmpty();

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_AUserIntroducedByTheSaver_HasNoSigners()
    {
        var (j, store, _, identity) = CreateClient(SignerModel.Rules);
        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
        await j.Fact(new SignerMember(site, UserB));

        var userB = j.Graph(UserB).Last;
        (await ((IStore)store).LoadSavers(ImmutableList.Create(userB), CancellationToken.None))
            .Should().ContainKey(userB);

        var members = await j.Query(membersWithSigners, site);

        members.Should().ContainSingle().Subject.signers.Should().BeEmpty();

        await j.Unload();
    }

    /// <summary>
    /// Scenario 1 of #288, the documented over-report: the post goes up once, under one
    /// token, while this replica reports both savers.
    /// </summary>
    [Fact]
    public async Task SignersOf_TwoAuthorizedSaversOnOneDevice_OverReportsBoth()
    {
        var (j, _, _, identity) = CreateClient(SignerModel.Rules);
        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
        await j.Fact(new SignerMember(site, UserB));
        await j.Fact(new SignerPost(site, "Hello"));

        identity.SetCurrentUser(null);
        identity.SetCurrentUser(UserB);
        await j.Fact(new SignerPost(site, "Hello"));

        var results = await j.Query(postsWithSigners, site);

        SignersIn(results).Should().BeEquivalentTo(
            new[] { UserA.publicKey, UserB.publicKey });

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_AFactLearnedFromAFeed_HasNoSaversAdded()
    {
        var (j, store, _, identity) = CreateClient(SignerModel.Rules);
        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));

        await ((IStore)store).Save(
            j.Graph(new SignerPost(site, "From a feed")), false, CancellationToken.None);

        var results = await j.Query(postsWithSigners, site);

        SignersIn(results).Should().BeEmpty();

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_OnAPredecessorQuery_ProjectsTheSaver()
    {
        var (j, _, _, identity) = CreateClient(SignerModel.Rules);
        identity.SetCurrentUser(UserA);
        var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
        var post = await j.Fact(new SignerPost(site, "Hello"));

        var results = await j.Query(siteOfPostWithSigners, post);

        results.Should().ContainSingle().Subject
            .signers.Select(signer => signer.publicKey)
            .Should().Equal(UserA.publicKey);

        await j.Unload();
    }

    [Fact]
    public async Task SignersOf_AfterReopeningTheStore_StillProjectsTheSaver()
    {
        SignerSite site;
        {
            var (j, _, _, identity) = CreateClient(SignerModel.Rules);
            identity.SetCurrentUser(UserA);
            site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
            await j.Fact(new SignerPost(site, "Hello"));
            await j.Unload();
        }

        // A new store over the same file, with nobody logged in, so the saver is read back
        // from fact_saver rather than from anything the first client held in memory.
        var (reopened, _, _, _) = CreateClient(SignerModel.Rules);

        var results = await reopened.Query(postsWithSigners, site);

        SignersIn(results).Should().Equal(UserA.publicKey);

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

    private (JinagaClient j, SQLiteStore store, FakeNetwork network, FakeIdentitySource identity) CreateClient(AuthorizationRules? rules)
    {
        var store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        var network = new FakeNetwork(output);
        var identity = new FakeIdentitySource();
        network.CurrentUser = () => identity.CurrentUser;
        var options = new JinagaClientOptions
        {
            HttpAuthenticationProvider = identity,
            AuthorizationRules = rules,
            QueueProcessingDelay = DelayLongerThanAnyTest
        };
        var j = new JinagaClient(
            store,
            network,
            ImmutableList<Jinaga.Projections.Specification>.Empty,
            NullLoggerFactory.Instance,
            options);
        return (j, store, network, identity);
    }
}

[FactType("Test.Signers.Site")]
public record SignerSite(User creator, string name);

[FactType("Test.Signers.Member")]
public record SignerMember(SignerSite site, User member);

[FactType("Test.Signers.Post")]
public record SignerPost(SignerSite site, string title);

[FactType("Test.Signers.Tag")]
public record SignerTag(SignerSite site, string label);

/// <summary>
/// The rules a replicator for this model would run. A site's creator creates it and adds
/// members. The creator or any member may post. Anyone may tag a site, so a replicator signs
/// a tag with no key.
/// </summary>
public static class SignerModel
{
    public static AuthorizationRules Rules { get; } = AuthorizationRules.Build(rules => rules
        .Any<User>()
        .Type<SignerSite>(site => site.creator)
        .Type<SignerMember>(member => member.site.creator)
        .Type<SignerPost>(post => post.site.creator)
        .Type<SignerPost>((post, facts) =>
            from member in facts.OfType<SignerMember>()
            where member.site == post.site
            from user in facts.OfType<User>()
            where user == member.member
            select user)
        .Any<SignerTag>());
}
