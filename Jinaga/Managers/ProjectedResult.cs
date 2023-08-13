using Jinaga.Products;

namespace Jinaga.Managers
{
    public class ProjectedResult
    {
        public Product Product { get; }
        public object Projection { get; }
        public string Path { get; }

        public ProjectedResult(Product product, object projection, string path)
        {
            this.Product = product;
            this.Projection = projection;
            this.Path = path;
        }
    }
}