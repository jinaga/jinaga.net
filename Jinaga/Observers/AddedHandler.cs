using Jinaga.Facts;
using System;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    internal class AddedHandler
    {
        public FactReferenceTuple Anchor { get; }
        public string Path { get; }
        public Func<object, Task<Func<Task>>> Added { get; }

        public AddedHandler(FactReferenceTuple anchor, string path, Func<object, Task<Func<Task>>> added)
        {
            Anchor = anchor;
            Path = path;
            Added = added;
        }
    }
}