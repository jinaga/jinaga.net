using System;
using System.Threading.Tasks;
using Jinaga.Products;

namespace Jinaga.Observers
{
    internal class AddedHandler
    {
        public Product Anchor { get; }
        public string ParameterName { get; }
        private Type ProjectionType { get; }
        public Func<object, Task<Func<Task>>> Added { get; }

        public AddedHandler(Product anchor, string parameterName, Type projectionType, Func<object, Task<Func<Task>>> added)
        {
            this.Anchor = anchor;
            this.ParameterName = parameterName;
            this.ProjectionType = projectionType;
            this.Added = added;
        }
    }
}