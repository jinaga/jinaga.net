using Jinaga.Products;
using System;
using System.Collections.Immutable;

namespace Jinaga.Observers
{
    internal class SpecificationListener
    {
        public Action<ImmutableList<Product>> OnResult { get; }

        public SpecificationListener(Action<ImmutableList<Product>> onResult)
        {
            OnResult = onResult;
        }
    }
}
