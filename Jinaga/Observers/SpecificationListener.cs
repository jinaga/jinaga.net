using Jinaga.Products;
using System;

namespace Jinaga.Observers
{
    internal class SpecificationListener
    {
        public Action<Product[]> OnResult { get; }

        public SpecificationListener(Action<Product[]> onResult)
        {
            OnResult = onResult;
        }
    }
}
