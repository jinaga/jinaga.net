namespace Jinaga.Test.Model.Order
{
    [FactType("Catalog")]
    record Catalog(string identifier);

    [FactType("Product")]
    record Product(Catalog Catalog, string sku);

    [FactType("Price")]
    record Price(Product product, decimal value, Price[] prior);

    [FactType("Item")]
    record Item(Product product, int quantity);

    [FactType("Order")]
    record Order(Item[] items);
}
