using System;
using System.Threading.Tasks;
using Jinaga.Products;

namespace Jinaga.Observers
{
    internal class AddedHandler
    {
        public Product Anchor { get; }
        public string Path { get; }
        public Func<object, Task<Func<Task>>> Added { get; }

        public AddedHandler(Product anchor, string path, Func<object, Task<Func<Task>>> added)
        {
            Anchor = anchor;
            Path = path;
            Added = added;
        }
    }
}