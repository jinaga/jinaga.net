using Jinaga.Products;

namespace Jinaga.Managers
{
    public class ProductAnchorProjection
    {
        public Product Product { get; }
        public Product Anchor { get; }
        public object Projection { get; }

        public ProductAnchorProjection(Product product, Product anchor, object projection)
        {
            this.Product = product;
            this.Anchor = anchor;
            this.Projection = projection;
        }
    }
}