using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Test.Fakes;
internal class FakeNetwork : INetwork
{
    public async Task<ImmutableList<string>> Feeds(ImmutableList<FactReference> givenReferences, Specification specification, CancellationToken cancellationToken)
    {
        return ImmutableList<string>.Empty;
    }

    public Task<(ImmutableList<FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
