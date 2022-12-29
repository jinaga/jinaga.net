using Jinaga.Facts;
using Jinaga.Projections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Services
{
    public interface INetwork
    {
        Task<ImmutableList<string>> Feeds(ImmutableList<FactReference> givenReferences, Specification specification, CancellationToken cancellationToken);
        Task<(ImmutableList<Facts.FactReference> references, string bookmark)> FetchFeed(string feed, string bookmark, CancellationToken cancellationToken);
        Task<FactGraph> Load(ImmutableList<FactReference> factReferences, CancellationToken cancellationToken);
        Task Save(ImmutableList<Fact> facts, CancellationToken cancellationToken);
    }
}
