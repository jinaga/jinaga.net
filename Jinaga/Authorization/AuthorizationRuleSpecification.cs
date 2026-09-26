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
        private Specification specification;

        public AuthorizationRuleSpecification(Specification specification)
        {
            this.specification = specification;
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
            var label = simpleProjection.Tag;

            var (head, tail) = specification.SplitBeforeFirstSuccessor();
            if (head == null)
            {
                throw new InvalidOperationException(
                    "The specification of an authorization rule must start with a predecessor join. Otherwise, it is unsatisfiable.");
            }

            var headProducts = head.Execute(
                FactReferenceTuple.Empty.Add(given.Label.Name, reference), graph);

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
