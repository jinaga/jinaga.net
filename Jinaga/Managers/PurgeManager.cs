using Jinaga.Projections;
using Jinaga.Services;
using Jinaga.Facts;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Jinaga.Pipelines;
using System.Threading;

namespace Jinaga.Managers
{
    class PurgeManager
    {
        private readonly IStore store;
        private readonly ImmutableList<Specification> purgeConditions;
        private readonly ImmutableList<Inverse> purgeInverses;

        public PurgeManager(IStore store, ImmutableList<Specification> purgeConditions)
        {
            this.store = store;
            this.purgeConditions = purgeConditions;
            this.purgeInverses = purgeConditions.SelectMany(pc => pc.ComputeInverses()).ToImmutableList();
        }

        public async Task Purge()
        {
            await store.Purge(purgeConditions).ConfigureAwait(false);
        }

        public void CheckCompliance(Specification specification)
        {
            IEnumerable<string> failures = PurgeFunctions.TestSpecificationForCompliance(purgeConditions, specification);
            if (failures.Any())
            {
                string message = string.Join(Environment.NewLine, failures);
                throw new InvalidOperationException(message);
            }
        }

        public async Task TriggerPurge(IEnumerable<Fact> factsAdded, CancellationToken cancellationToken)
        {
            foreach (var fact in factsAdded)
            {
                foreach (var purgeInverse in purgeInverses)
                {
                    var givenLabel = purgeInverse.InverseSpecification.Givens.Single().Label;
                    if (givenLabel.Type != fact.Reference.Type)
                    {
                        continue;
                    }

                    string givenName = givenLabel.Name;
                    var givenReference = new FactReference(fact.Reference.Type, fact.Reference.Hash);
                    var givenTuple = FactReferenceTuple.Empty
                        .Add(givenName, givenReference);
                    var results = await store.Read(givenTuple, purgeInverse.InverseSpecification, cancellationToken).ConfigureAwait(false);
                    foreach (var result in results)
                    {
                        var purgeRootTuple = purgeInverse.GivenSubset.Of(result);
                        var purgeRootName = purgeRootTuple.Names.Single();
                        var purgeRoot = purgeRootTuple.Get(purgeRootName);
                        var triggers = await ReadTriggers(purgeRoot, cancellationToken).ConfigureAwait(false);

                        await store.PurgeDescendants(purgeRoot, triggers).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Every trigger of every purge condition whose given is the purge root's type,
        /// read with that given bound to the root.
        /// </summary>
        /// <remarks>
        /// The inverse that fired names only the trigger that just arrived. Every other
        /// trigger of the root is a descendant of it, so a purge that spared only the one
        /// that fired deleted the ones that had fired before, and a later condition on the
        /// same type deleted an earlier condition's trigger. Purge sends no notification,
        /// so a watch went on holding what the store had dropped.
        ///
        /// A condition's result binds the root along with the facts the matches found, and
        /// all of them are passed. The root is spared either way, as the ancestor of its own
        /// triggers, and naming it costs nothing.
        /// </remarks>
        private async Task<ImmutableList<FactReference>> ReadTriggers(FactReference purgeRoot, CancellationToken cancellationToken)
        {
            var triggers = ImmutableList.CreateBuilder<FactReference>();
            var named = new HashSet<FactReference>();
            foreach (var purgeCondition in purgeConditions)
            {
                var givenLabel = purgeCondition.Givens.Single().Label;
                if (givenLabel.Type != purgeRoot.Type)
                {
                    continue;
                }

                var givenTuple = FactReferenceTuple.Empty
                    .Add(givenLabel.Name, purgeRoot);
                var results = await store.Read(givenTuple, purgeCondition, cancellationToken).ConfigureAwait(false);
                foreach (var result in results)
                {
                    foreach (var reference in result.GetFactReferences())
                    {
                        if (named.Add(reference))
                        {
                            triggers.Add(reference);
                        }
                    }
                }
            }
            return triggers.ToImmutable();
        }
    }
}