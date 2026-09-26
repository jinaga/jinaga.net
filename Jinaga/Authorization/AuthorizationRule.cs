using Jinaga.Facts;
using Jinaga.Services;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    public abstract class AuthorizationRule
    {
        public abstract string Describe(string type);

        /// <summary>
        /// Whether this rule permits the given user to author the given fact.
        ///
        /// Several rules may apply to one type; the engine accepts a fact if any of them
        /// permits it, so a rule that does not apply returns false rather than throwing.
        /// </summary>
        internal abstract Task<bool> IsAuthorized(
            IStore store,
            FactGraph graph,
            FactReference reference,
            FactReference? userReference,
            CancellationToken cancellationToken);
    }
}
