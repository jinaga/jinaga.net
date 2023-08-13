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
    public class Observer : IWatch
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
            addedHandlers = addedHandlers.Add(new AddedHandler(givenAnchor, "", "", onAdded));

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

        public void Stop()
        {
        }

        private async Task Read(CancellationToken cancellationToken)
        {
            var results = await factManager.Read(givenAnchor, specification, projectionType, cancellationToken);
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
                (ImmutableList<Product> results) => OnResult(inverse, results)
            )).ToImmutableList();
            this.listeners = listeners;
        }

        private void OnResult(Inverse inverse, ImmutableList<Product> results)
        {
            throw new NotImplementedException();
        }

        private async Task NotifyAdded(ImmutableList<ProductAnchorProjection> results, Projection projection, string path, Subset parentSubset)
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
    }
}
