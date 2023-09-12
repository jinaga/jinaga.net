using Jinaga.Projections;
using System;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace Jinaga
{
    public class ShareTarget
    {
        protected readonly Specification specification;
        protected readonly ImmutableList<DistributionRule> distributionRules;

        protected ShareTarget(Specification specification, ImmutableList<DistributionRule> distributionRules)
        {
            this.specification = specification;
            this.distributionRules = distributionRules;
        }

        public DistributionRules WithEveryone()
        {
            return new DistributionRules(distributionRules.Add(
                new DistributionRule(specification, null)));
        }
    }

    public class ShareTarget<T> : ShareTarget
        where T : class
    {
        internal ShareTarget(Specification specification, ImmutableList<DistributionRule> distributionRules) : base(specification, distributionRules)
        {
        }

        public DistributionRules With(Expression<Func<T, User>> userSelector)
        {
            Specification userSpecification = Given<T>.Match(userSelector);
            return new DistributionRules(distributionRules.Add(
                new DistributionRule(specification, userSpecification)));
        }

        public DistributionRules With(Specification<T, User> userSpecification)
        {
            return new DistributionRules(distributionRules.Add(
                new DistributionRule(specification, userSpecification)));
        }
    }

    public class ShareTarget<T, U>: ShareTarget
        where T : class
        where U : class
    {
        internal ShareTarget(Specification specification, ImmutableList<DistributionRule> distributionRules) : base(specification, distributionRules)
        {
        }

        public DistributionRules With(Expression<Func<T, U, User>> userSelector)
        {
            Specification userSpecification = Given<T, U>.Match(userSelector);
            return new DistributionRules(distributionRules.Add(
                new DistributionRule(specification, userSpecification)));
        }

        public DistributionRules With(Specification<T, U, User> userSpecification)
        {
            return new DistributionRules(distributionRules.Add(
                new DistributionRule(specification, userSpecification)));
        }
    }
}