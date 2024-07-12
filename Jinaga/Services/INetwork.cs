using Jinaga.Facts;
using Jinaga.Projections;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Services
{
    public interface INetwork
    {
        delegate void AuthenticationStateChanged(JinagaAuthenticationState state);
        event AuthenticationStateChanged? OnAuthenticationStateChanged;

        Task<(FactGraph graph, UserProfile profile)> Login(CancellationToken cancellationToken);
        Task<ImmutableList<string>> Feeds(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken);
        Task<(ImmutableList<Facts.FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken);
        void StreamFeed(string feed, string bookmark, CancellationToken cancellationToken, Func<ImmutableList<FactReference>, string, Task> onResponse, Action<Exception> onError);
        Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken);
        Task Save(FactGraph graph, CancellationToken cancellationToken);
    }
}
