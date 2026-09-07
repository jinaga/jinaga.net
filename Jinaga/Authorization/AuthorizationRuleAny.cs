using Jinaga.Facts;
using Jinaga.Services;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga
{
    public class AuthorizationRuleAny : AuthorizationRule
    {
        public AuthorizationRuleAny()
        {
        }

        public override string Describe(string type)
        {
            return $"    any {type}\n";
        }

        /// <summary>Permits everyone, including an unauthenticated client.</summary>
        public override Task<bool> IsAuthorized(
            IStore store,
            FactGraph graph,
            FactReference reference,
            FactReference? userReference,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }
}
