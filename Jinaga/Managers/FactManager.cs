﻿using Jinaga.Facts;
using Jinaga.Observers;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class FactManager
    {
        private readonly IStore store;

        public FactManager(IStore store)
        {
            this.store = store;
        }

        private SerializerCache serializerCache = SerializerCache.Empty;
        private DeserializerCache deserializerCache = DeserializerCache.Empty;
        private ImmutableList<IObserver> observers = ImmutableList<IObserver>.Empty;

        public async Task<ImmutableList<Fact>> Save(FactGraph graph, CancellationToken cancellationToken)
        {
            var added = await store.Save(graph, cancellationToken);
            foreach (var observer in observers)
            {
                await observer.FactsAdded(added, graph, cancellationToken);
            }
            return added;
        }

        public async Task<ImmutableList<Product>> Query(ImmutableList<FactReference> startReferences, Specification specification, CancellationToken cancellationToken)
        {
            return await store.Query(startReferences, specification, cancellationToken);
        }

        public async Task<FactGraph> LoadProducts(ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            var references = products
                .SelectMany(product => product.GetFactReferences())
                .ToImmutableList();
            return await store.Load(references, cancellationToken);
        }

        public async Task<ImmutableList<ProductAnchorProjection>> ComputeProjections(
            Projection projection,
            ImmutableList<Product> products,
            Type type,
            IWatchContext? watchContext,
            Product anchor,
            string collectionName,
            CancellationToken cancellationToken)
        {
            var references = Projector.GetFactReferences(projection, products, type);
            var graph = await store.Load(references, cancellationToken);
            return DeserializeProductsFromGraph(graph, projection, products, type, anchor, collectionName, watchContext);
        }

        public FactGraph Serialize(object prototype)
        {
            lock (this)
            {
                var collector = new Collector(serializerCache);
                collector.Serialize(prototype);
                serializerCache = collector.SerializerCache;
                return collector.Graph;
            }
        }

        public TFact Deserialize<TFact>(FactGraph graph, Element element)
        {
            if (element is SimpleElement simple)
            {
                lock (this)
                {
                    var emitter = new Emitter(graph, deserializerCache);
                    var fact = emitter.Deserialize<TFact>(simple.FactReference);
                    deserializerCache = emitter.DeserializerCache;
                    return fact;
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public TFact Deserialize<TFact>(FactGraph graph, FactReference reference)
        {
            lock (this)
            {
                var emitter = new Emitter(graph, deserializerCache);
                var fact = emitter.Deserialize<TFact>(reference);
                deserializerCache = emitter.DeserializerCache;
                return fact;
            }
        }

        public ImmutableList<ProductAnchorProjection> DeserializeProductsFromGraph(
            FactGraph graph,
            Projection projection,
            ImmutableList<Product> products,
            Type type,
            Product anchor,
            string collectionName,
            IWatchContext? watchContext)
        {
            lock (this)
            {
                var emitter = new Emitter(graph, deserializerCache, watchContext);
                ImmutableList<ProductAnchorProjection> results = Deserializer.Deserialize(emitter, projection, type, products, anchor, collectionName);
                deserializerCache = emitter.DeserializerCache;
                return results;
            }
        }

        public void AddObserver(IObserver observer)
        {
            lock (this)
            {
                observers = observers.Add(observer);
            }
        }

        public void RemoveObserver(IObserver observer)
        {
            lock (this)
            {
                observers = observers.Remove(observer);
            }
        }
    }
}
