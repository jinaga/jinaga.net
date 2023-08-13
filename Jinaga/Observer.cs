using Jinaga.Facts;
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
    public class Observer : IWatch, IWatchContext
    {
        private readonly Specification specification;
        private readonly string specificationHash;
        private readonly Product givenAnchor;
        private readonly Type projectionType;
        private readonly FactManager factManager;

        private CancellationTokenSource cancelInitialize = new CancellationTokenSource();

        private Task<bool>? cachedTask;
        private Task? loadedTask;

        private ImmutableList<SpecificationListener> listeners =
            ImmutableList<SpecificationListener>.Empty;
        private ImmutableDictionary<Product, Func<Task>> removalsByProduct =
            ImmutableDictionary<Product, Func<Task>>.Empty;
        private ImmutableList<AddedHandler> addedHandlers =
            ImmutableList<AddedHandler>.Empty;
        private ImmutableHashSet<Product> notifiedTuples =
            ImmutableHashSet<Product>.Empty;

        internal Observer(Specification specification, Product givenAnchor, Type projectionType, FactManager factManager, Func<object, Task<Func<Task>>> onAdded)
        {
            this.specification = specification;
            this.givenAnchor = givenAnchor;
            this.projectionType = projectionType;
            this.factManager = factManager;

            // Add the initial handler.
            addedHandlers = addedHandlers.Add(new AddedHandler(givenAnchor, "", onAdded));

            // Identify a specification by its hash.
            specificationHash = IdentityUtilities.ComputeSpecificationHash(specification, givenAnchor);
        }

        public Task Initialized => loadedTask!;

        internal void Start()
        {
            var cancellationToken = cancelInitialize.Token;
            cachedTask = Task.Run(async () =>
                await ReadFromStore(cancellationToken));
            loadedTask = Task.Run(async () =>
            {
                bool cached = await cachedTask;
                await FetchFromNetwork(cached, cancellationToken);
            });
        }

        private async Task<bool> ReadFromStore(CancellationToken cancellationToken)
        {
            DateTime? mruDate = await factManager.GetMruDate(specificationHash);
            if (mruDate == null)
            {
                return false;
            }

            // Read from local storage.
            await Read(cancellationToken);
            return true;
        }

        private async Task FetchFromNetwork(bool cached, CancellationToken cancellationToken)
        {
            if (!cached)
            {
                // Fetch from the network first,
                // then read from local storage.
                await Fetch(cancellationToken);
                await Read(cancellationToken);
            }
            else
            {
                // Already read from local storage.
                // Fetch from the network to update the cache.
                await Fetch(cancellationToken);
            }
            await factManager.SetMruDate(specificationHash, DateTime.UtcNow);
        }

        public void OnAdded(Product anchor, string path, Func<object, Task<Func<Task>>> added)
        {
            addedHandlers = addedHandlers.Add(new AddedHandler(anchor, path, added));
        }

        public void Stop()
        {
        }

        private async Task Read(CancellationToken cancellationToken)
        {
            var results = await factManager.Read(givenAnchor, specification, projectionType, this, cancellationToken);
            AddSpecificationListeners();
            var givenSubset = specification.Given
                .Select(label => label.Name)
                .Aggregate(Subset.Empty, (subset, name) => subset.Add(name));
            await NotifyAdded(results, specification.Projection, "", givenSubset);
        }

        private Task Fetch(CancellationToken cancellationToken)
        {
            return factManager.Fetch(givenAnchor, specification, cancellationToken);
        }

        private void AddSpecificationListeners()
        {
            var inverses = specification.ComputeInverses();
            ImmutableList<SpecificationListener> listeners = inverses.Select(inverse => factManager.AddSpecificationListener(
                inverse.InverseSpecification,
                async (ImmutableList<Product> results, CancellationToken cancellationToken) => await OnResult(inverse, results, cancellationToken)
            )).ToImmutableList();
            this.listeners = listeners;
        }

        private async Task OnResult(Inverse inverse, ImmutableList<Product> products, CancellationToken cancellationToken)
        {
            // Filter out results that do not match the given.
            var givenSubset = inverse.InverseSpecification.Given
                .Select(label => label.Name)
                .Aggregate(Subset.Empty, (subset, name) => subset.Add(name));
            var matchingProducts = products
                .Where(result => givenSubset.Of(result).Equals(givenAnchor))
                .ToImmutableList();
            if (matchingProducts.IsEmpty)
            {
                return;
            }

            if (inverse.Operation == InverseOperation.Add || inverse.Operation == InverseOperation.MaybeAdd)
            {
                var results = await factManager.ComputeProjections(inverse.InverseSpecification.Projection, matchingProducts, projectionType, this, inverse.Path, cancellationToken);
                await NotifyAdded(results, inverse.InverseSpecification.Projection, inverse.Path, inverse.ParentSubset);
            }
            else if (inverse.Operation == InverseOperation.Remove || inverse.Operation == InverseOperation.MaybeRemove)
            {
                await NotifyRemoved(matchingProducts, inverse.ResultSubset);
            }
        }

        private async Task NotifyAdded(ImmutableList<ProjectedResult> results, Projection projection, string path, Subset parentSubset)
        {
            foreach (var result in results)
            {
                var parentTuple = parentSubset.Of(result.Product);
                var matchingAddedHandlers = addedHandlers
                    .Where(hander => hander.Anchor.Equals(parentTuple) && hander.Path == path);
                foreach (var addedHandler in matchingAddedHandlers)
                {
                    var resultAdded = addedHandler.Added;
                    // Don't call result added if we have already called it for this tuple.
                    if (!notifiedTuples.Contains(result.Product))
                    {
                        var removal = await resultAdded(result.Projection);
                        notifiedTuples.Add(result.Product);
                        removalsByProduct = removalsByProduct.Add(result.Product, removal);
                    }
                }

                // Recursively notify added for specification results.
                if (result.Projection is CompoundProjection compoundProjection)
                {
                    foreach (var name in compoundProjection.Names)
                    {
                        var component = compoundProjection.GetProjection(name);
                        if (component is CollectionProjection collectionProjection)
                        {
                            var childPath = path + "." + name;
                            // TODO: await NotifyAdded(result.Result[component.Name], specificationProjection, childPath, result.Product);
                        }
                    }
                }
            }
        }

        private Task NotifyRemoved(ImmutableList<Product> matchingProducts, Subset resultSubset)
        {
            throw new NotImplementedException();
        }
    }
}
