using System;
using System.Threading.Tasks;
using Jinaga.Products;

namespace Jinaga.Observers
{
    internal class AddedHandler
    {
        private Product anchor;
        private string parameterName;
        private Type projectionType;
        private Func<object, Task<Func<Task>>> added;

        public AddedHandler(Product anchor, string parameterName, Type projectionType, Func<object, Task<Func<Task>>> added)
        {
            this.anchor = anchor;
            this.parameterName = parameterName;
            this.projectionType = projectionType;
            this.added = added;
        }
    }
}