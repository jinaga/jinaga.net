using Jinaga.Products;
using System.Collections.Immutable;

namespace Jinaga.Managers
{
    public class ProjectedResult
    {
        public Product Product { get; }
        public object Projection { get; }
        public string Path { get; }
        public ImmutableList<ProjectedResultChildCollection> Collections { get; }

        public ProjectedResult(Product product, object projection, string path, ImmutableList<ProjectedResultChildCollection> collections)
        {
            Product = product;
            Projection = projection;
            Path = path;
            Collections = collections;
        }
    }
}