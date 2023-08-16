using Jinaga.Facts;
using System;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    internal interface IWatchContext
    {
        void OnAdded(FactReferenceTuple anchor, string path, Func<object, Task<Func<Task>>> added);
    }
}