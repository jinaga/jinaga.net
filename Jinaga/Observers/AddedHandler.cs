using System;
using System.Threading.Tasks;
using Jinaga.Products;

namespace Jinaga.Observers
{
    internal class AddedHandler
    {
        public Product Anchor { get; }
        public string CollectionName { get; }
        public Func<object, Task<Func<Task>>> Added { get; }

        public AddedHandler(Product anchor, string collectionName, Func<object, Task<Func<Task>>> added)
        {
            this.Anchor = anchor;
            this.CollectionName = collectionName;
            this.Added = added;
        }
    }
}