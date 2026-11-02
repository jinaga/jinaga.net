using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.UnitTest
{
    public class SimulatedNetwork : INetwork
    {
        private readonly string publicKey;

#pragma warning disable CS0067
        public event INetwork.AuthenticationStateChanged OnAuthenticationStateChanged;
#pragma warning restore CS0067

        public SimulatedNetwork(string publicKey)
        {
            this.publicKey = publicKey;
        }

        public Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            if (publicKey == null)
            {
                throw new Exception("No logged in user");
            }
            var userFact = Fact.Create("Jinaga.User",
                ImmutableList.Create(new Field("publicKey", new FieldValueString(publicKey))),
                ImmutableList<Predecessor>.Empty);
            var graph = FactGraph.Empty
                .Add(new FactEnvelope(userFact, ImmutableList<FactSignature>.Empty));
            var profile = new UserProfile("Simulated user");
            return Task.FromResult((graph, profile));
        }

        public Task Save(FactGraph graph, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<(ImmutableList<string> feeds, ImmutableList<FeedDecision> decisions)> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            var feeds = ImmutableList<string>.Empty;
            return Task.FromResult((feeds, AuthorizeAll(feeds)));
        }

        /// <summary>
        /// Reports every feed this network returns as authorized. There is no replicator
        /// here to deny one, and deriving the decisions from the feeds keeps that true of
        /// whatever this network returns.
        /// </summary>
        private static ImmutableList<FeedDecision> AuthorizeAll(ImmutableList<string> feeds)
        {
            return feeds
                .Select(feed => new FeedDecision
                {
                    Feed = feed,
                    Decision = DistributionDecision.Authorized,
                    Reason = "Simulated network."
                })
                .ToImmutableList();
        }

        public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
        {
            throw new NotImplementedException();
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
