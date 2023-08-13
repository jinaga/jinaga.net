using Jinaga.Facts;
using Jinaga.Identity;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public SpecificationListener AddSpecificationListener(Specification specification, Func<ImmutableList<Product>, CancellationToken, Task> onResult)
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

        public async Task Notify(FactGraph graph, ImmutableList<Fact> facts, CancellationToken cancellationToken)
        {
            foreach (var fact in facts)
            {
                await NotifyFactSaved(graph, fact, cancellationToken);
            }
        }

        private async Task NotifyFactSaved(FactGraph graph, Fact fact, CancellationToken cancellationToken)
        {
            if (listenersByTypeAndHash.TryGetValue(fact.Reference.Type, out var listenersByHash))
            {
                foreach (var specificationWithListeners in listenersByHash.Values)
                {
                    var listeners = specificationWithListeners.Listeners;
                    if (listeners.Any())
                    {
                        var specification = specificationWithListeners.Specification;
                        var givenReference = fact.Reference;
                        string name = specification.Given.Single().Name;
                        var givenProduct = FactReferenceTuple.Empty.Add(name, givenReference);
                        var products = specification.CanRunOnGraph
                            ? specification.Execute(givenProduct, graph)
                            : await store.Read(givenProduct, specification, cancellationToken);
                        foreach (var listener in listeners)
                        {
                            await listener.OnResult(products, cancellationToken);
                        }
                    }
                }
            }
        }
    }
}
