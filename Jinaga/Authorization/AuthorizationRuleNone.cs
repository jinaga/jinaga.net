using Jinaga.Facts;
using Jinaga.Services;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    public class AuthorizationRuleNone : AuthorizationRule
    {
        public AuthorizationRuleNone()
        {
        }

        public override string Describe(string type)
        {
            return $"    no {type}\n";
        }

        /// <summary>Permits nobody. Present so a type can be explicitly closed rather than
        /// closed by the absence of a rule, which reads the same and means something else.</summary>
        internal override Task<bool> IsAuthorized(
            IStore store,
            FactGraph graph,
            FactReference reference,
            FactReference? userReference,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
    }
}
