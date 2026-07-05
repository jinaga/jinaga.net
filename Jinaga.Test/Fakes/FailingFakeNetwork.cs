using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes
{
    /// <summary>
    /// A fake network whose Save method always fails, simulating an unreachable endpoint.
    /// Used to test that JinagaClient.Push() surfaces upload failures instead of hanging.
    /// </summary>
    internal class FailingFakeNetwork : INetwork
    {
        public int SaveCallCount { get; private set; } = 0;

#pragma warning disable CS0067 // Event is never used; required to satisfy INetwork
        public event INetwork.AuthenticationStateChanged OnAuthenticationStateChanged;
#pragma warning restore CS0067

        public Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task Save(FactGraph graph, CancellationToken cancellationToken)
        {
            SaveCallCount++;
            throw new InvalidOperationException("Simulated network failure: endpoint unreachable.");
        }

        public void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError)
        {
            throw new NotImplementedException();
        }
    }
}
