using Jinaga.Facts;

namespace Jinaga.Observers
{
    internal interface IWatchContext
    {
        void OnAdded(FactReferenceTuple anchor, string path, OnAddedHandler added);
    }
}