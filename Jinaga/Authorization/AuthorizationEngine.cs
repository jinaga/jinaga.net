using Jinaga.Facts;
using Jinaga.Services;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Authorization
{
    /// <summary>
    /// Decides whether a user is permitted to author the facts in a graph.
    ///
    /// A replicator makes this decision for real. This brings the same decision in process so
    /// that a test can observe a rule refusing a fact, rather than only inspecting the text the
    /// rule describes itself with. A rule that reads correctly and behaves incorrectly is not a
    /// hypothetical: the two can disagree, and only running it tells you which.
    /// </summary>
    internal class AuthorizationEngine
    {
        private readonly AuthorizationRules rules;
        private readonly IStore store;

        public AuthorizationEngine(AuthorizationRules rules, IStore store)
        {
            this.rules = rules;
            this.store = store;
        }

        /// <summary>
        /// Checks every fact in the graph that the store does not already have.
        ///
        /// Facts already known are skipped rather than re-checked, matching the replicator's
        /// behaviour: authorization is evaluated when a fact is first accepted, and a fact that
        /// has already been accepted stays accepted. Re-checking would make a later revocation
        /// retroactively invalidate history.
        /// </summary>
        public async Task Authorize(
            FactGraph graph,
            FactReference? userReference,
            CancellationToken cancellationToken)
        {
            // The engine only reads. The caller saves the graph once it is authorized, so that
            // the save reports every new fact to observers and queues it for the network, and so
            // that a refused fact never reaches the store.
            var known = await store.ListKnown(graph.FactReferences).ConfigureAwait(false);

            foreach (var reference in graph.FactReferences)
            {
                if (known.Contains(reference))
                {
                    continue;
                }

                var authorized = await IsAuthorized(
                    graph, reference, userReference, cancellationToken).ConfigureAwait(false);

                if (!authorized)
                {
                    throw new AuthorizationException(reference.Type);
                }
            }
        }

        private async Task<bool> IsAuthorized(
            FactGraph graph,
            FactReference reference,
            FactReference? userReference,
            CancellationToken cancellationToken)
        {
            var applicable = rules.RulesForType(reference.Type);

            // No rule for a type means nobody may author it. Defaulting to permitted would make
            // a forgotten rule invisible, which is the opposite of what a policy is for.
            if (applicable.IsEmpty)
            {
                return false;
            }

            foreach (var rule in applicable)
            {
                var permitted = await rule.IsAuthorized(
                    store, graph, reference, userReference, cancellationToken).ConfigureAwait(false);

                if (permitted)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
