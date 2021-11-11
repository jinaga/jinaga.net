using Jinaga;

namespace Jinaga.Test.Model.Order
{
    [FactType("Catalog")]
    record Catalog(string identifier);

    [FactType("Product")]
    record Product(Catalog Catalog, string sku);

    [FactType("Item")]
    record Item(Product product, int quantity);

    [FactType("Order")]
    record Order(Item[] items);
}
