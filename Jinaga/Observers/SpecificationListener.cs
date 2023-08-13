using Jinaga.Products;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    internal class SpecificationListener
    {
        public Func<ImmutableList<Product>, CancellationToken, Task> OnResult { get; }

        public SpecificationListener(Func<ImmutableList<Product>, CancellationToken, Task> onResult)
        {
            OnResult = onResult;
        }
    }
}
