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
    public class AuthorizationEngine
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
            // Which facts are new has to be settled before anything is written, or saving the
            // graph would make every fact in it look already-accepted and skip the check.
            var known = await store.ListKnown(graph.FactReferences).ConfigureAwait(false);

            // Rules navigate from the fact being authorized, and the fact is not in the store
            // yet — a rule reaching a predecessor, or asking whether some other fact exists,
            // has nothing to read otherwise. This mirrors a real client, which writes locally
            // and learns from the replicator that a fact was refused.
            await store.Save(graph, false, cancellationToken).ConfigureAwait(false);

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
