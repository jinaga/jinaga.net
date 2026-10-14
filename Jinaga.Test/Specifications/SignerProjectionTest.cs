using Jinaga.Cryptography;
using Jinaga.Facts;
using Jinaga.Http;
using Jinaga.Projections;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Jinaga.Test.Specifications
{
    using Comment = Jinaga.Test.Model.Comment;
    using Content = Jinaga.Test.Model.Content;
    using Site = Jinaga.Test.Model.Site;

    /// <summary>
    /// A fact's identity is its type, fields and predecessors. Signatures sit beside the fact,
    /// so any number of keys can sign one fact without changing its hash, and a specification
    /// reads who asserted something with <c>facts.SignersOf</c> rather than by making the
    /// signer a predecessor.
    /// </summary>
    public class SignerProjectionTest
    {
        public record ContentSigners(Content content, IEnumerable<User> signers);
        public record ContentOnly(Content content);

        private static readonly Specification<Site, ContentSigners> contentWithSigners =
            Given<Site>.Match((site, facts) =>
                from content in facts.OfType<Content>()
                where content.site == site
                select new ContentSigners(content, facts.SignersOf(content))
            );

        private static readonly Specification<Site, ContentOnly> contentWithoutSigners =
            Given<Site>.Match((site, facts) =>
                from content in facts.OfType<Content>()
                where content.site == site
                select new ContentOnly(content)
            );

        private readonly MemoryStore store = new MemoryStore();
        private readonly FakeNetwork network;
        private readonly JinagaClient j;

        public SignerProjectionTest(Xunit.Abstractions.ITestOutputHelper output)
        {
            network = new FakeNetwork(output);
            j = new JinagaClient(
                store,
                network,
                ImmutableList<Specification>.Empty,
                NullLoggerFactory.Instance,
                new JinagaClientOptions());
        }

        [Fact]
        public async Task SignersOf_ProjectsEveryKeyThatSigned()
        {
            var site = await GivenSite();
            var content = await j.Fact(new Content(site, "index.html"));
            var first = KeyPair.Generate();
            var second = KeyPair.Generate();
            await SignFact(content, first, second);

            var results = await j.Query(contentWithSigners, site);

            var result = results.Should().ContainSingle().Subject;
            result.signers.Select(signer => signer.publicKey)
                .Should().BeEquivalentTo(new[] { first.PublicKey, second.PublicKey });
        }

        [Fact]
        public async Task SignersOf_LeavesTheHashAlone()
        {
            var site = await GivenSite();
            var content = await j.Fact(new Content(site, "index.html"));
            // The hash of the same fact as it was before anybody signed it. Computed from the
            // prototype, so it moves only if the identity rules themselves move.
            var hashBeforeSigning = j.Hash(new Content(site, "index.html"));
            await SignFact(content, KeyPair.Generate(), KeyPair.Generate());

            var results = await j.Query(contentWithSigners, site);

            var result = results.Should().ContainSingle().Subject;
            result.signers.Should().HaveCount(2);
            j.Hash(result.content).Should().Be(hashBeforeSigning);
        }

        [Fact]
        public async Task SignersOf_UnsignedFactProjectsNoSigners()
        {
            var site = await GivenSite();
            await j.Fact(new Content(site, "index.html"));

            var results = await j.Query(contentWithSigners, site);

            results.Should().ContainSingle().Subject
                .signers.Should().BeEmpty();
        }

        [Fact]
        public void SignersOf_IsRefusedInAWhereClause()
        {
            Action buildSpecification = () => Given<Site>.Match((site, facts) =>
                from content in facts.OfType<Content>()
                where content.site == site
                where facts.SignersOf(content).Any()
                select content
            );

            // Named, and refused for the reason that makes it unrepresentable, rather than
            // falling through to the front end's generic "unsupported predicate" message.
            buildSpecification.Should().Throw<SpecificationException>()
                .WithMessage("*SignersOf*")
                .WithMessage("*cannot affect matching*");
        }

        [Fact]
        public void SignersOf_IsRefusedInAnExistentialCondition()
        {
            Action buildSpecification = () => Given<Site>.Match((site, facts) =>
                from content in facts.OfType<Content>()
                where content.site == site
                where facts.Any<Comment>(comment =>
                    comment.content == content &&
                    facts.SignersOf(comment).Any())
                select content
            );

            buildSpecification.Should().Throw<SpecificationException>()
                .WithMessage("*SignersOf*")
                .WithMessage("*cannot affect matching*");
        }

        [Fact]
        public void SignersOf_MakesADistinctSpecification()
        {
            // ObservableSource keys its listeners by the hash of this string, so two
            // specifications that differ only in projecting signers have to render
            // differently or they would share one listener entry.
            contentWithSigners.ToDescriptiveString()
                .Should().NotBe(contentWithoutSigners.ToDescriptiveString());
        }

        [Fact]
        public async Task SignersOf_DoesNotReachTheWire()
        {
            var site = await GivenSite();
            await j.Query(contentWithSigners, site);
            await j.Query(contentWithoutSigners, site);

            // The specifications the client actually handed to the network, rendered by the
            // request builder that talks to a replicator.
            var requested = network.RequestedFeeds;
            requested.Should().HaveCount(2);
            var withSigners = HttpNetwork.CreateFeedsRequest(
                requested[0].givenTuple, requested[0].specification);
            var withoutSigners = HttpNetwork.CreateFeedsRequest(
                requested[1].givenTuple, requested[1].specification);

            withSigners.Should().Be(withoutSigners);
        }

        [Fact]
        public async Task SignersOf_PredecessorQueryReadsTheStoredSignatures()
        {
            var site = await GivenSite();
            await j.Fact(new Content(site, "index.html"));
            var first = KeyPair.Generate();
            var second = KeyPair.Generate();
            await SignFact(new Content(site, "index.html"), first, second);

            // Walks only to a predecessor, so it would be routed to the graph path if
            // the projection allowed it. The given is a fresh prototype, so its graph
            // carries none of the signatures the store holds.
            var specification = Given<Content>.Match((content, facts) =>
                from s in facts.OfType<Site>()
                where content.site == s
                select new ContentSigners(content, facts.SignersOf(content)));

            var results = await j.Query(specification, new Content(site, "index.html"));

            results.Should().ContainSingle().Subject
                .signers.Select(signer => signer.publicKey)
                .Should().BeEquivalentTo(new[] { first.PublicKey, second.PublicKey });
        }

        [Fact]
        public async Task SignersOf_WatchAgreesWithQuery()
        {
            await j.SingleUse(async principal =>
            {
                var site = await j.Fact(new Site(principal, "michaelperry.net"));
                var observed = new List<ContentSigners>();
                var observer = j.Watch(contentWithSigners, site,
                    projection => observed.Add(projection));
                await observer.Loaded;

                // Saved locally while the watch is running, and signed by the single-use
                // principal on the way in.
                await j.Fact(new Content(site, "index.html"));
                observer.Stop();

                var queried = await j.Query(contentWithSigners, site);

                var watched = observed.Should().ContainSingle().Subject;
                watched.signers.Select(signer => signer.publicKey)
                    .Should().BeEquivalentTo(
                        queried.Should().ContainSingle().Subject
                            .signers.Select(signer => signer.publicKey));
                watched.signers.Should().ContainSingle()
                    .Which.publicKey.Should().Be(principal.publicKey);
                return true;
            });
        }

        private async Task<Site> GivenSite()
        {
            var creator = await j.Fact(new User("--- PUBLIC KEY ---"));
            return await j.Fact(new Site(creator, "michaelperry.net"));
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
            await store.Save(
                FactGraph.Empty.Add(new FactEnvelope(fact, signatures)),
                false,
                CancellationToken.None);
        }
    }
}
