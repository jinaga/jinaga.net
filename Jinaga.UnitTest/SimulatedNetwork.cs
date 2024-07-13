using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
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

        public Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            return Task.FromResult(ImmutableList<string>.Empty);
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
