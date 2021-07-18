﻿using Jinaga.Facts;
using Jinaga.Pipelines;
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

        private SerializerCache serializerCache = new SerializerCache();
        private DeserializerCache deserializerCache = new DeserializerCache();

        public async Task<ImmutableList<Fact>> Save(FactGraph graph, CancellationToken cancellationToken)
        {
            return await store.Save(graph, cancellationToken);
        }

        public async Task<ImmutableList<Product>> Query(FactReference startReference, string initialTag, ImmutableList<Path> paths, CancellationToken cancellationToken)
        {
            return await store.Query(startReference, initialTag, paths, cancellationToken);
        }

        public async Task<ImmutableList<TProjection>> ComputeProjections<TProjection>(Projection projection, ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            switch (projection)
            {
                case SimpleProjection simple:
                    var references = products
                        .Select(product => product.GetFactReference(simple.Tag))
                        .ToImmutableList();
                    var graph = await store.Load(references, cancellationToken);
                    var projections = references
                        .Select(reference => Deserialize<TProjection>(graph, reference))
                        .ToImmutableList();
                    return projections;
                default:
                    throw new NotImplementedException();
            }
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
    }
}
