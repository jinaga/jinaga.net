using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
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
        /// they are among the results.
        ///
        /// It is the same specification <see cref="Describe"/> prints, so a rule cannot describe
        /// one policy and enforce another.
        /// </summary>
        public override async Task<bool> IsAuthorized(
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

            var given = specification.Givens.Single().Label.Name;
            var tuple = FactReferenceTuple.Empty.Add(given, reference);

            var products = await store
                .Read(tuple, specification, cancellationToken)
                .ConfigureAwait(false);

            return products
                .SelectMany(product => product.GetFactReferences())
                .Any(candidate => candidate.Equals(userReference));
        }
    }
}
