using Jinaga.Products;

namespace Jinaga.Managers
{
    public class ProductProjection
    {
        private readonly Product product;
        private readonly object projection;

        public ProductProjection(Product product, object projection)
        {
            this.product = product;
            this.projection = projection;
        }

        public Product Product => product;
        public object Projection => projection;
    }

    public class ProductProjection<TProjection>
    {
        private readonly Product product;
        private readonly TProjection projection;

        public ProductProjection(Product product, TProjection projection)
        {
            this.product = product;
            this.projection = projection;
        }

        public Product Product => product;
        public TProjection Projection => projection;
    }
}