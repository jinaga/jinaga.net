using Jinaga.Projections;

namespace Jinaga
{
    public class DistributionRule
    {
        private readonly Specification specification;
        private readonly Specification? user;

        public DistributionRule(Specification specification, Specification? user)
        {
            this.specification = specification;
            this.user = user;
        }

        public Specification Specification => specification;

        public Specification? User => user;
    }
}