using System;
using System.Threading.Tasks;
using Jinaga.Products;

namespace Jinaga.Observers
{
    internal class AddedHandler
    {
        public Product Anchor { get; }
        public string CollectionName { get; }
        public string Path { get; }
        public Func<object, Task<Func<Task>>> Added { get; }

        public AddedHandler(Product anchor, string collectionName, string path, Func<object, Task<Func<Task>>> added)
        {
            Anchor = anchor;
            CollectionName = collectionName;
            Path = path;
            Added = added;
        }
    }
}