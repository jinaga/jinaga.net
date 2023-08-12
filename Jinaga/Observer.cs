﻿using Jinaga.Facts;
using Jinaga.Identity;
using Jinaga.Managers;
using Jinaga.Observers;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    public class Observer<TProjection> : IObserver, IWatch
    {
        private readonly Specification specification;
        private readonly string specificationHash;
        private readonly Product givenAnchor;
        private readonly FactManager factManager;
        private readonly IObservation observation;
        private readonly ImmutableList<Inverse> inverses;

        private CancellationTokenSource cancelInitialize = new CancellationTokenSource();

        private Task<bool>? cachedTask;
        private Task? loadedTask;

        private ImmutableDictionary<Product, Func<Task>> removalsByProduct =
            ImmutableDictionary<Product, Func<Task>>.Empty;

        internal Observer(Specification specification, Product givenAnchor, FactManager factManager, FunctionObservation<TProjection> observation)
        {
            this.specification = specification;
            this.givenAnchor = givenAnchor;
            this.factManager = factManager;
            this.observation = observation;
            this.inverses = specification.ComputeInverses();

            // Identify a specification by its hash.
            string declarationString = specification.GenerateDeclarationString(givenAnchor);
            string specificationString = specification.ToDescriptiveString();
            string request = $"{declarationString}\n${specificationString}";
            specificationHash = IdentityUtilities.ComputeStringHash(request);
        }

        public Task Initialized => loadedTask!;

        internal void Start()
        {
            var cancellationToken = cancelInitialize.Token;
            cachedTask = Task.Run(ReadFromStore);
            loadedTask = Task.Run(async () =>
            {
                bool cached = await cachedTask;
                await FetchFromNetwork(cached);
            });
        }

        private async Task<bool> ReadFromStore()
        {
            DateTime? mruDate = await factManager.GetMruDate(specificationHash);
            if (mruDate == null)
            {
                return false;
            }

            // Read from local storage.
            await Read();
            return true;
        }

        private async Task FetchFromNetwork(bool cached)
        {
            if (!cached)
            {
                // Fetch from the network first,
                // then read from local storage.
                await Fetch();
                await Read();
            }
            else
            {
                // Already read from local storage.
                // Fetch from the network to update the cache.
                await Fetch();
            }
            await factManager.SetMruDate(specificationHash, DateTime.UtcNow);
        }

        public void Stop()
        {
        }

        private Task Read()
        {
            throw new NotImplementedException();
        }

        private Task Fetch()
        {
            throw new NotImplementedException();
        }

        private async Task RunInitialQuery(CancellationToken cancellationToken)
        {
            var givenReferences = givenAnchor.GetFactReferences().ToImmutableList();
            var products = await factManager.Query(givenReferences, specification, cancellationToken);
            var productAnchorProjections = await factManager.ComputeProjections(specification.Projection, products, typeof(TProjection), observation, givenAnchor, string.Empty, cancellationToken);
            var removals = await observation.NotifyAdded(productAnchorProjections);
            lock (this)
            {
                removalsByProduct = removalsByProduct.AddRange(removals);
            }
        }

        public async Task FactsAdded(ImmutableList<Fact> added, FactGraph graph, CancellationToken cancellationToken)
        {
            var productsAdded = ImmutableList<(Product product, Inverse inverse)>.Empty;
            var productsRemoved = ImmutableList<Product>.Empty;
            var givenReferences = added.Select(a => a.Reference).ToImmutableList();
            foreach (var inverse in inverses)
            {
                var inverseSpecification = inverse.InverseSpecification;
                var matchingReferences = givenReferences
                    .Where(r => inverseSpecification.Given.Any(start => r.Type == start.Type))
                    .ToImmutableList();
                if (matchingReferences.Any())
                {
                    var products = inverseSpecification.CanRunOnGraph
                        ? inverseSpecification.Execute(matchingReferences, graph)
                        : await factManager.Query(
                            matchingReferences,
                            inverseSpecification,
                            cancellationToken);
                    foreach (var product in products)
                    {
                        var givenProduct = inverse.GivenSubset.Of(product);
                        var resultProduct = inverse.ResultSubset.Of(product);
                        if (givenProduct.Equals(this.givenAnchor))
                        {
                            if (inverse.Operation == Pipelines.InverseOperation.Add)
                            {
                                productsAdded = productsAdded.Add((resultProduct, inverse));
                            }
                            else if (inverse.Operation == Pipelines.InverseOperation.Remove)
                            {
                                productsRemoved = productsRemoved.Add(resultProduct);
                            }
                        }
                    }
                }
            }
            if (productsAdded.Any())
            {
                var products = productsAdded.Select(p => p.product).ToImmutableList();
                var addedGraph = await factManager.LoadProducts(products, cancellationToken);
                var productAnchorProjections = DeserializeAllProducts(graph, productsAdded);
                var removals = await observation.NotifyAdded(productAnchorProjections);
                lock (this)
                {
                    removalsByProduct = removalsByProduct.AddRange(removals);
                }
            }
            if (productsRemoved.Any())
            {
                var removals = productsRemoved
                    .Select(product => removalsByProduct.GetValueOrDefault(product)!)
                    .Where(identity => identity != null)
                    .ToImmutableList();
                foreach (var removal in removals)
                {
                    await removal();
                }
                lock (this)
                {
                    removalsByProduct = removalsByProduct.RemoveRange(productsRemoved);
                }
            }
        }

        private ImmutableList<ProductAnchorProjection> DeserializeAllProducts(FactGraph graph, ImmutableList<(Product product, Inverse inverse)> productsAdded)
        {
            var productAnchorProjections =
                from pair in productsAdded
                let product = pair.product
                let inverse = pair.inverse
                let projection = inverse.InverseSpecification.Projection
                let type = ElementType(typeof(TProjection), inverse.CollectionIdentifiers)
                let collectionName = inverse.CollectionIdentifiers.Select(id => id.CollectionName).LastOrDefault()
                let subset = inverse.CollectionIdentifiers.Select(c => c.IntermediateSubset).LastOrDefault() ?? inverse.GivenSubset
                let anchor = subset.Of(product)
                from productProjection in factManager.DeserializeProductsFromGraph(graph, projection, ImmutableList<Product>.Empty.Add(product), type, anchor, collectionName, observation)
                select productProjection;
            return productAnchorProjections.ToImmutableList();
        }

        private static Type ElementType(Type type, IEnumerable<CollectionIdentifier> collectionIdentifiers) => collectionIdentifiers
            .Aggregate(type, (t, c) => GetCollectionType(t, c.CollectionName));

        private static Type GetCollectionType(Type type, string collectionName)
        {
            var propertyType = type.GetProperty(collectionName).PropertyType;
            if (propertyType.IsGenericType &&
                (propertyType.GetGenericTypeDefinition() == typeof(IObservableCollection<>) ||
                 propertyType.GetGenericTypeDefinition() == typeof(IQueryable<>)))
            {
                return propertyType.GetGenericArguments()[0];
            }
            else
            {
                throw new InvalidOperationException($"Collection {collectionName} is not an IObservableCollection or IQueryable");
            }
        }
    }
}
