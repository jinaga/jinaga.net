#nullable enable

using Jinaga.Cryptography;
using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Test.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace Jinaga.Test.Specifications
{
    /// <summary>
    /// A client cannot sign a fact. The replicator signs on upload, and neither feed path
    /// returns a signature for a fact this replica already holds. So the signers of a fact
    /// are its stored signatures, together with its savers on this replica, whichever user
    /// is logged in and whatever the application's rules say. A saver of a
    /// <c>Jinaga.User</c> is the one exception: a replicator keeps no signature for a user,
    /// and introducing one asserts nothing about them.
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
        public record UserSigners(User user, IEnumerable<User> signers);

        private static readonly Specification<SignerSite, PostSigners> postsWithSigners =
            Given<SignerSite>.Match((site, facts) =>
                from post in facts.OfType<SignerPost>()
                where post.site == site
                select new PostSigners(post, facts.SignersOf(post))
            );

        /// <summary>
        /// A predecessor-only specification. Its matches can be answered from the given's own
        /// graph, which is where a provenance source that lives outside the graph is easiest
        /// to lose.
        /// </summary>
        private static readonly Specification<SignerPost, SiteSigners> siteOfPostWithSigners =
            Given<SignerPost>.Match((post, facts) =>
                from site in facts.OfType<SignerSite>()
                where site == post.site
                select new SiteSigners(site, facts.SignersOf(site))
            );

        private static readonly Specification<SignerSite, UserSigners> membersWithSigners =
            Given<SignerSite>.Match((site, facts) =>
                from member in facts.OfType<SignerMember>()
                where member.site == site
                from user in facts.OfType<User>()
                where user == member.member
                select new UserSigners(user, facts.SignersOf(user))
            );

        private static readonly Specification<SignerSite, UserSigners> creatorWithSigners =
            Given<SignerSite>.Match((site, facts) =>
                from user in facts.OfType<User>()
                where user == site.creator
                select new UserSigners(user, facts.SignersOf(user))
            );

        private readonly ITestOutputHelper output;

        public SaverSignerTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task SignersOf_AFactSavedByAUser_ProjectsTheSaverBeforeAnyUpload()
        {
            var (j, store, network, identity) = CreateClient();
            identity.SetCurrentUser(UserA);
            var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
            var post = await j.Fact(new SignerPost(site, "Hello"));

            var results = await j.Query(postsWithSigners, site);

            // Nothing has been uploaded, so there is no signature anywhere. The only thing
            // attributing this fact is the record of who saved it.
            network.UploadedUnderUsers.Should().BeEmpty();
            (await store.GetQueue()).Facts.Select(fact => fact.Reference)
                .Should().Contain(j.Graph(post).Last);
            SignersIn(results).Should().Equal(UserA.publicKey);
        }

        [Fact]
        public async Task SignersOf_WhenTheSaversOwnSignatureArrives_ProjectsOneSigner()
        {
            var (j, store, _, identity) = CreateClient();
            identity.SetCurrentUser(UserA);
            var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
            var post = await j.Fact(new SignerPost(site, "Hello"));

            // The same fact saved again, now carrying the replicator's signature for the
            // saver's own key.
            await SignFact(j, store, post, KeyA);

            var results = await j.Query(postsWithSigners, site);

            // Keyed by public key, so the signature and the saver are one signer.
            SignersIn(results).Should().Equal(UserA.publicKey);
        }

        [Fact]
        public async Task SignersOf_CombinesAStoredSignatureFromAnotherKeyWithTheSaver()
        {
            var (j, store, _, identity) = CreateClient();
            identity.SetCurrentUser(UserA);
            var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
            var post = await j.Fact(new SignerPost(site, "Hello"));
            var otherKey = KeyPair.Generate();

            await SignFact(j, store, post, otherKey);

            var results = await j.Query(postsWithSigners, site);

            SignersIn(results).Should().BeEquivalentTo(
                new[] { otherKey.PublicKey, UserA.publicKey });
        }

        /// <summary>
        /// Any user may talk about any other user, so a <c>Jinaga.User</c> has no privileged
        /// author. Saving a fact that introduces another user's <c>User</c> records the
        /// saver, and still makes them no signer.
        /// </summary>
        [Fact]
        public async Task SignersOf_AUserIntroducedByTheSaver_HasNoSigners()
        {
            var (j, store, _, identity) = CreateClient();
            identity.SetCurrentUser(UserA);
            var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
            await j.Fact(new SignerMember(site, UserB));

            // The saver is recorded, so it is the fact's type that leaves B's User unsigned.
            var userB = j.Graph(UserB).Last;
            (await store.LoadSavers(ImmutableList.Create(userB), CancellationToken.None))
                .Should().ContainKey(userB);

            var members = await j.Query(membersWithSigners, site);
            members.Should().ContainSingle().Subject.signers.Should().BeEmpty();

            // So is the saver's own User, introduced by the first save.
            var creator = await j.Query(creatorWithSigners, site);
            creator.Should().ContainSingle().Subject.signers.Should().BeEmpty();
        }

        /// <summary>
        /// Scenario 1 of #288, the documented over-report. A and B both save the same post on
        /// this device. The post is uploaded once, under one of them, so a replicator records
        /// one signer while this replica reports both.
        /// </summary>
        [Fact]
        public async Task SignersOf_TwoAuthorizedSaversOnOneDevice_OverReportsBoth()
        {
            var (j, _, _, identity) = CreateClient();
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
        }

        [Fact]
        public async Task SignersOf_AFactLearnedFromAFeed_HasNoSaversAdded()
        {
            var (j, store, _, identity) = CreateClient();
            identity.SetCurrentUser(UserA);
            var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));

            // Written to the store the way a feed delivers it, so this replica saw nobody
            // save it, even though A is logged in.
            await SaveAsThoughSynced(j, store, new SignerPost(site, "From a feed"));

            var results = await j.Query(postsWithSigners, site);

            SignersIn(results).Should().BeEmpty();
        }

        [Fact]
        public async Task SignersOf_OnAPredecessorQuery_ProjectsTheSaver()
        {
            var (j, _, _, identity) = CreateClient();
            identity.SetCurrentUser(UserA);
            var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
            var post = await j.Fact(new SignerPost(site, "Hello"));

            var results = await j.Query(siteOfPostWithSigners, post);

            results.Should().ContainSingle().Subject
                .signers.Select(signer => signer.publicKey)
                .Should().Equal(UserA.publicKey);
        }

        [Fact]
        public async Task SignersOf_InAWatch_ProjectsTheSaver()
        {
            var (j, _, _, identity) = CreateClient();
            identity.SetCurrentUser(UserA);
            var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
            await j.Fact(new SignerPost(site, "Before the watch"));

            var observed = new List<PostSigners>();
            var observer = j.Watch(postsWithSigners, site,
                projection => observed.Add(projection));
            await observer.Loaded;

            // Saved while the watch is running.
            await j.Fact(new SignerPost(site, "During the watch"));
            observer.Stop();

            observed.Select(result => result.post.title)
                .Should().BeEquivalentTo("Before the watch", "During the watch");
            observed.Should().AllSatisfy(result =>
                result.signers.Select(signer => signer.publicKey)
                    .Should().Equal(UserA.publicKey));
        }

        [Fact]
        public async Task SignersOf_InsideAChildCollection_ProjectsTheSaver()
        {
            var (j, _, _, identity) = CreateClient();
            identity.SetCurrentUser(UserA);
            var site = await j.Fact(new SignerSite(UserA, "michaelperry.net"));
            await j.Fact(new SignerPost(site, "Hello"));

            var results = await j.Query(Given<SignerSite>.Match((site, facts) =>
                from s in facts.OfType<SignerSite>()
                where s == site
                select new
                {
                    site = s,
                    posts = facts.Observable(s, postsWithSigners)
                }
            ), site);

            results.Should().ContainSingle().Subject
                .posts.Should().ContainSingle().Subject
                .signers.Select(signer => signer.publicKey)
                .Should().Equal(UserA.publicKey);
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
        private static async Task SignFact<TFact>(JinagaClient j, PersistentMemoryStore store, TFact prototype, params KeyPair[] keyPairs)
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

        /// <summary>
        /// Facts this replica learned from elsewhere, written straight to the store as a sync
        /// would, without being saved by anyone here.
        /// </summary>
        private static async Task SaveAsThoughSynced(JinagaClient j, PersistentMemoryStore store, object fact)
        {
            await ((IStore)store).Save(j.Graph(fact), false, CancellationToken.None);
        }

        private (JinagaClient j, PersistentMemoryStore store, FakeNetwork network, FakeIdentitySource identity) CreateClient()
        {
            var store = new PersistentMemoryStore();
            var network = new FakeNetwork(output);
            var identity = new FakeIdentitySource();
            network.CurrentUser = () => identity.CurrentUser;
            var options = new JinagaClientOptions
            {
                HttpAuthenticationProvider = identity,
                QueueProcessingDelay = DelayLongerThanAnyTest
            };
            var j = new JinagaClient(
                store,
                network,
                ImmutableList<Specification>.Empty,
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

}
