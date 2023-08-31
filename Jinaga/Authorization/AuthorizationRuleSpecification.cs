using Jinaga.Projections;

namespace Jinaga
{
    public class AuthorizationRuleSpecification : AuthorizationRule
    {
        private Specification specification;

        public AuthorizationRuleSpecification(Specification specification)
        {
            this.specification = specification;
        }
    }
}