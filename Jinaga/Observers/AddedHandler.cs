using System;
using System.Threading.Tasks;
using Jinaga.Products;

namespace Jinaga.Observers
{
    internal class AddedHandler
    {
        public Product Anchor { get; }
        private string parameterName;
        private Type projectionType;
        public Func<object, Task<Func<Task>>> Added { get; }

        public AddedHandler(Product anchor, string parameterName, Type projectionType, Func<object, Task<Func<Task>>> added)
        {
            this.Anchor = anchor;
            this.parameterName = parameterName;
            this.projectionType = projectionType;
            this.Added = added;
        }
    }
}