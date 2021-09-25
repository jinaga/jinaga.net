using Jinaga.Facts;
using Jinaga.Observers;
using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Products;
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

        public async Task<ImmutableList<Product>> Query(ImmutableList<FactReference> startReferences, Pipeline  pipeline, CancellationToken cancellationToken)
        {
            return await store.Query(startReferences, pipeline, cancellationToken);
        }

        public async Task<ImmutableList<ProductProjection<TProjection>>> ComputeProjections<TProjection>(Projection projection, ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            var references = GetFactReferences(projection, products, typeof(TProjection));
            var graph = await store.Load(references, cancellationToken);
            var productProjections = DeserializeProductsFromGraph(graph, projection, products, typeof(TProjection));
            return productProjections
                .Select(pair => new ProductProjection<TProjection>(pair.Product, (TProjection)pair.Projection))
                .ToImmutableList();
        }

        private IEnumerable<FactReference> GetFactReferences(Product product, Projection projection)
        {
            if (projection is SimpleProjection simple)
            {
                return product.GetElement(simple.Tag).GetFactReferences();
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

        private ImmutableList<FactReference> GetFactReferences(Projection projection, ImmutableList<Product> products, Type type)
        {
            if (projection is SimpleProjection simple)
            {
                return products
                    .Select(product => product.GetFactReference(simple.Tag))
                    .ToImmutableList();
            }
            else if (projection is CompoundProjection compound)
            {
                var constructorInfos = type.GetConstructors();
                if (constructorInfos.Length != 1)
                {
                    throw new NotImplementedException($"Multiple constructors for {type.Name}");
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
                return references;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private ImmutableList<ProductProjection> DeserializeProductsFromGraph(FactGraph graph, Projection projection, ImmutableList<Product> products, Type type)
        {
            lock (this)
            {
                var emitter = new Emitter(graph, deserializerCache);
                ImmutableList<ProductProjection> results = Deserialize(emitter, projection, products, type);
                deserializerCache = emitter.DeserializerCache;
                return results;
            }
        }

        private ImmutableList<ProductProjection> Deserialize(Emitter emitter, Projection projection, ImmutableList<Product> products, Type type)
        {
            if (projection is SimpleProjection simpleProjection)
                return DeserializeSimpleProjection(simpleProjection, emitter, products, type);
            else if (projection is CompoundProjection compoundProjection)
                return DeserializeCompoundProjection(compoundProjection, emitter, products, type);
            else if (projection is CollectionProjection collectionProjection)
                return DeserializeCollectionProjection(collectionProjection, emitter, products, type);
            else
                throw new NotImplementedException();
        }

        private ImmutableList<ProductProjection> DeserializeSimpleProjection(SimpleProjection simpleProjection, Emitter emitter, ImmutableList<Product> products, Type type)
        {
            var productProjections = products
                .Select(product => new ProductProjection(product,
                    emitter.DeserializeToType(product.GetFactReference(simpleProjection.Tag), type)
                ))
                .ToImmutableList();
            return productProjections;
        }

        private ImmutableList<ProductProjection> DeserializeCompoundProjection(CompoundProjection compoundProjection, Emitter emitter, ImmutableList<Product> products, Type type)
        {
            var constructorInfos = type.GetConstructors();
            if (constructorInfos.Length != 1)
            {
                throw new NotImplementedException($"Multiple constructors for {type.Name}");
            }
            var constructor = constructorInfos.Single();
            var parameters = constructor.GetParameters();
            var productProjections =
                from product in products
                let result = constructor.Invoke((
                    from parameter in parameters
                    let projection = compoundProjection.GetProjection(parameter.Name)
                    select DeserializeParameter(product, projection, emitter, parameter.ParameterType)
                ).ToArray())
                select new ProductProjection(product, result);
            return productProjections.ToImmutableList();
        }

        private ImmutableList<ProductProjection> DeserializeCollectionProjection(CollectionProjection collectionProjection, Emitter emitter, ImmutableList<Product> products, Type type)
        {
            throw new NotImplementedException();
        }

        private object DeserializeParameter(Product product, Projection projection, Emitter emitter, Type parameterType)
        {
            if (parameterType.IsFactType())
            {
                var reference = GetFactReferences(product, projection).Single();
                return emitter.DeserializeToType(reference, parameterType);
            }
            else if (parameterType.IsGenericType &&
                parameterType.GetGenericTypeDefinition() == typeof(IObservableCollection<>))
            {
                var elementType = parameterType.GetGenericArguments()[0];
                var elements = GetFactReferences(product, projection)
                    .Select(reference => emitter.DeserializeToType(reference, parameterType))
                    .ToImmutableList();
                return ImmutableObservableCollection.Create(elementType, elements);
            }
            else
            {
                throw new NotImplementedException();
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
