using System;
using System.Threading.Tasks;
using Jinaga.Products;

namespace Jinaga.Observers
{
    internal interface IWatchContext
    {
        void OnAdded(Product anchor, string parameterName, Type projectionType, Func<object, Task<Func<Task>>> added);
    }
}