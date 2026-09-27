using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    public class AuthorizationRuleSpecification : AuthorizationRule
    {
        private readonly Specification specification;
        private readonly string givenName;
        private readonly string label;
        private readonly Specification head;
        private readonly Specification? tail;

        /// <summary>
        /// A rule is written once and run on every save of the type it governs, so everything its
        /// evaluation relies on is checked here, where the rule is written.
        /// </summary>
        /// <exception cref="InvalidOperationException">The specification cannot be a rule.</exception>
        public AuthorizationRuleSpecification(Specification specification)
        {
            var wellFormed = WellFormedSpecification.Check(
                specification, "The specification of an authorization rule");
            if (specification.Givens.Count != 1)
            {
                throw new InvalidOperationException(
                    "The specification of an authorization rule must be given a single fact.");
            }
            var given = specification.Givens[0];
            if (given.ExistentialConditions.Any())
            {
                throw new InvalidOperationException(
                    "The given of an authorization rule cannot have existential conditions.");
            }
            if (!(specification.Projection is SimpleProjection simpleProjection))
            {
                throw new InvalidOperationException(
                    "The specification of an authorization rule must project a single user.");
            }

            // The head is deterministic, and runs on the graph being authorized.
            // The tail seeks successors, and runs on the store.
            var (head, tail) = wellFormed.SplitBeforeFirstSuccessor();
            if (head == null)
            {
                throw new InvalidOperationException(
                    "The specification of an authorization rule must start with a predecessor join. Otherwise, it is unsatisfiable.");
            }

            this.specification = specification;
            this.givenName = given.Label.Name;
            this.label = simpleProjection.Tag;
            this.head = head;
            this.tail = tail;
        }

        public override string Describe(string type)
        {
            return specification.ToDescriptiveString(1);
        }

        /// <summary>
        /// Runs the rule's specification with the fact as its given, and permits the user if
        /// they are the fact it projects.
        ///
        /// It is the same specification <see cref="Describe"/> prints, so a rule cannot describe
        /// one policy and enforce another. The specification is split before its first successor
        /// join: the head walks predecessors within the graph being authorized, which is not yet
        /// in the store, and the tail runs on the store from where the head left off.
        /// </summary>
        internal override async Task<bool> IsAuthorized(
            IStore store,
            FactGraph graph,
            FactReference reference,
            FactReference? userReference,
            CancellationToken cancellationToken)
        {
            // An unauthenticated client satisfies no specification: every one of them projects
            // a user, and there is nobody to match.
            if (userReference is null)
            {
                return false;
            }

            var headProducts = head.Execute(
                FactReferenceTuple.Empty.Add(givenName, reference), graph);

            var candidates = ImmutableList<FactReference>.Empty;
            if (tail == null)
            {
                candidates = headProducts
                    .Select(product => product.GetFactReference(label))
                    .ToImmutableList();
            }
            else
            {
                foreach (var headProduct in headProducts)
                {
                    var tailTuple = tail.Givens.Aggregate(
                        FactReferenceTuple.Empty,
                        (tuple, tailGiven) => tuple.Add(
                            tailGiven.Label.Name,
                            headProduct.GetFactReference(tailGiven.Label.Name)));
                    var tailProducts = await store
                        .Read(tailTuple, tail, cancellationToken)
                        .ConfigureAwait(false);
                    candidates = candidates.AddRange(tailProducts
                        .Select(product => product.GetFactReference(label)));
                }
            }

            return candidates.Any(candidate => candidate.Equals(userReference));
        }
    }
}
