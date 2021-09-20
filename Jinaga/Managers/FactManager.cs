using Jinaga.Facts;
using Jinaga.Observers;
using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Projections;
using Jinaga.Serialization;
using Jinaga.Services;
using System;
using System.Collections.Generic;
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

        public async Task<ImmutableList<Product>> Query(FactReference startReference, Pipeline pipeline, CancellationToken cancellationToken)
        {
            var startReferences = ImmutableList<FactReference>.Empty.Add(startReference);
            return await store.Query(startReferences, pipeline, cancellationToken);
        }

        public async Task<ImmutableList<Product>> QueryAll(ImmutableList<FactReference> startReferences, Pipeline  pipeline, CancellationToken cancellationToken)
        {
            return await store.Query(startReferences, pipeline, cancellationToken);
        }

        public async Task<ImmutableList<ProductProjection<TProjection>>> ComputeProjections<TProjection>(Projection projection, ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            if (projection is SimpleProjection simple)
            {
                var productReferences = products
                    .Select(product => (product, reference: product.GetFactReference(simple.Tag)))
                    .ToImmutableList();
                var references = productReferences
                    .Select(pair => pair.reference)
                    .ToImmutableList();
                var graph = await store.Load(references, cancellationToken);
                var productProjections = productReferences
                    .Select(pair => new ProductProjection<TProjection>(pair.product,
                        Deserialize<TProjection>(graph, pair.reference)))
                    .ToImmutableList();
                return productProjections;
            }
            else if (projection is CompoundProjection compound)
            {
                var constructorInfos = typeof(TProjection).GetConstructors();
                if (constructorInfos.Length != 1)
                {
                    throw new NotImplementedException($"Multiple constructors for {typeof(TProjection).Name}");
                }
                var constructor = constructorInfos.Single();
                var parameters = constructor.GetParameters();
                var references = (
                    from product in products
                    from parameter in parameters
                    let value = compound.GetProjection(parameter.Name)
                    from reference in GetFactReferences(product, value)
                    select reference
                ).Distinct().ToImmutableList();
                var graph = await store.Load(references, cancellationToken);
                var productProjections =
                    from product in products
                    let result = constructor.Invoke((
                        from parameter in parameters
                        let value = compound.GetProjection(parameter.Name)
                        select DeserializeParameter(product, value, graph, parameter.ParameterType)
                    ).ToArray())
                    select new ProductProjection<TProjection>(product, (TProjection)result);
                return productProjections.ToImmutableList();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private object DeserializeParameter(Product product, Projection projection, FactGraph graph, Type parameterType)
        {
            if (parameterType.IsFactType())
            {
                var reference = GetFactReferences(product, projection).Single();
                return Deserialize(graph, reference, parameterType);
            }
            else if (parameterType.IsGenericType &&
                parameterType.GetGenericTypeDefinition() == typeof(IObservableCollection<>))
            {
                var elementType = parameterType.GetGenericArguments()[0];
                var elements =  GetFactReferences(product, projection)
                    .Select(reference => Deserialize(graph, reference, elementType))
                    .ToImmutableList();
                return ImmutableObservableCollection.Create(elementType, elements);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private IEnumerable<FactReference> GetFactReferences(Product product, Projection projection)
        {
            if (projection is SimpleProjection simple)
            {
                return new [] { product.GetFactReference(simple.Tag) };
            }
            else if (projection is CompoundProjection compound)
            {
                return compound.Names
                    .SelectMany(name => GetFactReferences(
                        product,
                        compound.GetProjection(name)
                    ));
            }
            else if (projection is CollectionProjection collection)
            {
                return new FactReference[0];
            }
            else
            {
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

        public object Deserialize(FactGraph graph, FactReference reference, Type type)
        {
            lock (this)
            {
                var emitter = new Emitter(graph, deserializerCache);
                var fact = emitter.DeserializeToType(reference, type);
                deserializerCache = emitter.DeserializerCache;
                return fact;
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
