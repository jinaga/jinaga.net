using Jinaga.Facts;
using Jinaga.Products;
using System;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    internal interface IWatchContext
    {
        void OnAdded(FactReferenceTuple anchor, string path, Func<object, Task<Func<Task>>> added);

        /// <summary>
        /// The tuple that identifies the row of <paramref name="product"/> at
        /// <paramref name="path"/>. A handler is anchored to this tuple, and the
        /// observer looks it up by the same tuple, so the two cannot disagree.
        /// </summary>
        FactReferenceTuple AnchorOf(string path, Product product);
    }
}