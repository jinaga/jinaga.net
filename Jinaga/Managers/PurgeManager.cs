using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Jinaga.Managers
{
    class PurgeManager
    {
        private readonly IStore store;
        private readonly ImmutableList<Specification> purgeConditions;

        public PurgeManager(IStore store, ImmutableList<Specification> purgeConditions)
        {
            this.store = store;
            this.purgeConditions = purgeConditions;
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
    }
}