#nullable enable

using Jinaga.Cryptography;
using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

namespace Jinaga.Test.Specifications
{
    using Content = Jinaga.Test.Model.Content;
    using Site = Jinaga.Test.Model.Site;

    /// <summary>
    /// A client cannot sign a fact. The replicator signs on upload with the uploading user's
    /// key, and neither feed path returns a signature for a fact this replica already holds,
    /// so the facts a user creates on their own device carry no signature here. Provenance is
    /// what this replica knows, and it knows it witnessed the save, so the local author counts
    /// as a signer alongside the stored signatures.
    /// </summary>
    public class LocalAuthorSignerTest
    {
        /// <summary>
        /// Long enough that the debounce never elapses during a test, so nothing is uploaded
        /// and the facts stay queued with their authorship records intact.
        /// </summary>
        private const int DelayLongerThanAnyTest = 60000;

        public record ContentSigners(Content content, IEnumerable<User> signers);

        private static readonly Specification<Site, ContentSigners> contentWithSigners =
            Given<Site>.Match((site, facts) =>
                from content in facts.OfType<Content>()
                where content.site == site
                select new ContentSigners(content, facts.SignersOf(content))
            );

        public record SiteSigners(Site site, IEnumerable<User> signers);

        /// <summary>
        /// A predecessor-only specification. Its matches can be answered from the given's own
        /// graph, so the client takes the graph-only path rather than reading the store, which
        /// is where a provenance source that lives outside the graph is easiest to lose.
        /// </summary>
        private static readonly Specification<Content, SiteSigners> siteOfContentWithSigners =
            Given<Content>.Match((content, facts) =>
                from site in facts.OfType<Site>()
                where site == content.site
                select new SiteSigners(site, facts.SignersOf(site))
            );

        private readonly ITestOutputHelper output;

        public LocalAuthorSignerTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task SignersOf_ProjectsTheUserWhoSavedTheFact()
        {
            var (j, store, identity) = CreateClient();
            var author = new User(KeyPair.Generate().PublicKey);
            identity.SetCurrentUser(author);
            var site = await GivenSite(j);
            await j.Fact(new Content(site, "index.html"));

            var results = await j.Query(contentWithSigners, site);

            // Nothing has been uploaded, so there is no signature anywhere. The only thing
            // attributing this fact is the record of who saved it.
            SignersIn(results).Should().Equal(author.publicKey);
        }

        [Fact]
        public async Task SignersOf_WhenTheAuthorsOwnSignatureArrives_StillProjectsOneSigner()
        {
            var (j, store, identity) = CreateClient();
            var authorKey = KeyPair.Generate();
            identity.SetCurrentUser(new User(authorKey.PublicKey));
            var site = await GivenSite(j);
            var content = await j.Fact(new Content(site, "index.html"));

            // The fact comes back the way a feed would deliver it, now carrying the
            // replicator's signature for the author's own key.
            await SignFact(j, store, content, authorKey);

            var results = await j.Query(contentWithSigners, site);

            // Keyed by public key, so the signature and the authorship record are one signer
            // rather than two.
            SignersIn(results).Should().Equal(authorKey.PublicKey);
        }

        [Fact]
        public async Task SignersOf_UnionsAStoredSignatureWithTheLocalAuthor()
        {
            var (j, store, identity) = CreateClient();
            var author = new User(KeyPair.Generate().PublicKey);
            identity.SetCurrentUser(author);
            var site = await GivenSite(j);
            var content = await j.Fact(new Content(site, "index.html"));
            var otherKey = KeyPair.Generate();

            await SignFact(j, store, content, otherKey);

            var results = await j.Query(contentWithSigners, site);

            SignersIn(results).Should().BeEquivalentTo(
                new[] { otherKey.PublicKey, author.publicKey });
        }

        [Fact]
        public async Task SignersOf_WithNoCurrentUserAndNoSignatures_ProjectsNoSigners()
        {
            var (j, store, identity) = CreateClient();
            // Nobody is logged in, so the save records no author.
            var site = await GivenSite(j);
            await j.Fact(new Content(site, "index.html"));

            var results = await j.Query(contentWithSigners, site);

            SignersIn(results).Should().BeEmpty();
        }

        [Fact]
        public async Task SignersOf_WithAnIdentitySource_DoesNotAttributeAFactFromAFeed()
        {
            var (j, store, identity) = CreateClient();
            var author = new User(KeyPair.Generate().PublicKey);
            identity.SetCurrentUser(author);
            var site = await GivenSite(j);

            // Learned from a feed rather than saved here, so this replica witnessed no save
            // and has no author to add. SaveLocal does not queue, which is what the
            // authorship record keys on.
            var content = new Content(site, "from-a-feed.html");
            await j.Local.Fact(content);

            var results = await j.Query(contentWithSigners, site);

            SignersIn(results).Should().BeEmpty();
        }

        [Fact]
        public async Task SignersOf_OnAPredecessorQuery_StillProjectsTheLocalAuthor()
        {
            var (j, store, identity) = CreateClient();
            var author = new User(KeyPair.Generate().PublicKey);
            identity.SetCurrentUser(author);
            var site = await GivenSite(j);
            var content = await j.Fact(new Content(site, "index.html"));

            var results = await j.Query(siteOfContentWithSigners, content);

            // The authorship record does not travel in the graph, so a specification answered
            // from the graph alone has to fall back to the store to find it.
            results.Should().ContainSingle().Subject
                .signers.Select(signer => signer.publicKey)
                .Should().Equal(author.publicKey);
        }

        private static IEnumerable<string> SignersIn(IEnumerable<ContentSigners> results)
        {
            return results.Should().ContainSingle().Subject
                .signers.Select(signer => signer.publicKey)
                .ToImmutableList();
        }

        private static async Task<Site> GivenSite(JinagaClient j)
        {
            var creator = await j.Fact(new User("--- SITE CREATOR ---"));
            return await j.Fact(new Site(creator, "michaelperry.net"));
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

        private (JinagaClient j, PersistentMemoryStore store, FakeIdentitySource identity) CreateClient()
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
            return (j, store, identity);
        }
    }
}
