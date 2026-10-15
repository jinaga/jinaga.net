using Jinaga.Facts;
using Jinaga.Products;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Test.Products
{
    public class ProductEqualityTest
    {
        private static readonly FactReference site = new FactReference("Blog.Site", "site-hash");
        private static readonly FactReference first = new FactReference("Blog.Post", "first-hash");
        private static readonly FactReference second = new FactReference("Blog.Post", "second-hash");

        [Fact]
        public void ProductsWithEqualCollections_AreEqual()
        {
            var left = SiteWithPosts(first, second);
            var right = SiteWithPosts(first, second);

            left.Should().Be(right);
            left.GetHashCode().Should().Be(right.GetHashCode());
        }

        [Fact]
        public void ProductsWithCollectionsInADifferentOrder_AreEqual()
        {
            var left = SiteWithPosts(first, second);
            var right = SiteWithPosts(second, first);

            left.Should().Be(right);
            left.GetHashCode().Should().Be(right.GetHashCode());
        }

        [Fact]
        public void ProductsWithDifferentCollections_AreNotEqual()
        {
            var left = SiteWithPosts(first, second);
            var right = SiteWithPosts(first);

            left.Should().NotBe(right);
        }

        // Each call builds every list and product afresh, so no two products share an instance.
        private static Product SiteWithPosts(params FactReference[] posts)
        {
            var products = posts
                .Select(post => Product.Empty.With("post", new SimpleElement(post)))
                .ToImmutableList();
            return Product.Empty
                .With("site", new SimpleElement(site))
                .With("posts", new CollectionElement(products));
        }
    }
}
