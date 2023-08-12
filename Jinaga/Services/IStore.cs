using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Services
{
    public interface IStore
    {
        Task<ImmutableList<Fact>> Save(FactGraph graph, System.Threading.CancellationToken cancellationToken);
        Task<ImmutableList<Product>> Read(ImmutableList<FactReference> givenReferences, Specification specification, CancellationToken cancellationToken);
        Task<FactGraph> Load(ImmutableList<FactReference> references, System.Threading.CancellationToken cancellationToken);
        Task<string> LoadBookmark(string feed);
        Task<ImmutableList<FactReference>> ListKnown(ImmutableList<FactReference> factReferences);
        Task SaveBookmark(string feed, string bookmark);
        Task<DateTime?> GetMruDate(string specificationHash);
        Task SetMruDate(string specificationHash, DateTime mruDate);
    }
}
