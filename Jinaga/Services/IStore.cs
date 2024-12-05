using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Services
{
    public interface IStore
    {
        bool IsPersistent { get; }

        Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, CancellationToken cancellationToken);
        Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken);
        Task<FactGraph> Load(ImmutableList<FactReference> references, CancellationToken cancellationToken);
        Task<string> LoadBookmark(string feed);
        Task<ImmutableList<FactReference>> ListKnown(ImmutableList<FactReference> factReferences);
        Task SaveBookmark(string feed, string bookmark);
        Task<DateTime?> GetMruDate(string specificationHash);
        Task SetMruDate(string specificationHash, DateTime mruDate);
        Task<QueuedFacts> GetQueue();
        Task SetQueueBookmark(string bookmark);
        Task<IEnumerable<Fact>> GetAllFacts();
        Task Purge(ImmutableList<Specification> purgeConditions);
    }
}
