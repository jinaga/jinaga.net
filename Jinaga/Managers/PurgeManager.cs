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
                        var purgeRoot = result.GetFactReference(givenName);
                        var triggerSubset = purgeInverse.GivenSubset.Subtract(givenName);
                        var triggerTuple = triggerSubset.Of(result);
                        var triggers = triggerTuple.Names.Select(name => triggerTuple.Get(name))
                            .ToImmutableList();

                        await store.PurgeDescendants(purgeRoot, triggers).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}