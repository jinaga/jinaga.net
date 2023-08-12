using Jinaga.Identity;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;

namespace Jinaga.Observers
{
    internal class ObservableSource
    {
        private class SpecificationWithListeners
        {
            public Specification Specification { get; }
            public ImmutableList<SpecificationListener> Listeners { get; }

            public SpecificationWithListeners(Specification specification, ImmutableList<SpecificationListener> listeners)
            {
                Specification = specification;
                Listeners = listeners;
            }

            public SpecificationWithListeners Add(SpecificationListener specificationListener)
            {
                return new SpecificationWithListeners(
                    Specification,
                    Listeners.Add(specificationListener)
                );
            }
        }
        
        private readonly IStore store;

        private ImmutableDictionary<string, ImmutableDictionary<string, SpecificationWithListeners>> listenersByTypeAndHash =
            ImmutableDictionary<string, ImmutableDictionary<string, SpecificationWithListeners>>.Empty;

        public ObservableSource(IStore store)
        {
            this.store = store;
        }

        public SpecificationListener AddSpecificationListener(Specification specification, Action<Product[]> onResult)
        {
            if (specification.Given.Count != 1)
            {
                throw new ArgumentException("The specification must have exactly one given.");
            }
            var givenType = specification.Given[0].Type;
            var specificationKey = IdentityUtilities.ComputeStringHash(specification.ToDescriptiveString());

            var specificationListener = new SpecificationListener(onResult);
            if (!listenersByTypeAndHash.TryGetValue(givenType, out var listenersByHash))
            {
                listenersByHash = ImmutableDictionary<string, SpecificationWithListeners>.Empty;
            }
            if (!listenersByHash.TryGetValue(specificationKey, out var specificationWithListeners))
            {
                specificationWithListeners = new SpecificationWithListeners(specification, ImmutableList<SpecificationListener>.Empty);
            }
            specificationWithListeners = specificationWithListeners.Add(specificationListener);
            listenersByHash = listenersByHash.SetItem(specificationKey, specificationWithListeners);
            listenersByTypeAndHash = listenersByTypeAndHash.SetItem(givenType, listenersByHash);

            return specificationListener;
        }
    }
}
