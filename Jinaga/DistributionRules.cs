using Jinaga.Projections;
using System;
using System.Collections.Immutable;
using System.Text;

namespace Jinaga
{
    public class DistributionRules
    {
        private ImmutableList<DistributionRule> distributionRules;

        internal DistributionRules(ImmutableList<DistributionRule> distributionRules)
        {
            this.distributionRules = distributionRules;
        }

        public ShareTarget<T> Share<T, U>(Specification<T, U> specification)
            where T : class
        {
            return new ShareTarget<T>(specification, distributionRules);
        }

        public static string Describe(Func<DistributionRules, DistributionRules> distribution)
        {
            var rules = distribution(new DistributionRules(ImmutableList<DistributionRule>.Empty));
            string description = rules.SaveToDescription();
            return description;
        }

        private string SaveToDescription()
        {
            var description = new StringBuilder();
            description.Append("distribution {\n");
            foreach (var rule in distributionRules)
            {
                string specificationDescription = rule.Specification.ToDescriptiveString(1).TrimStart();
                string userDescription = rule.User?.ToDescriptiveString(1).TrimStart() ?? "everyone\n";
                description.Append($"    share {specificationDescription}    with {userDescription}");
            }
            description.Append("}");
            return description.ToString();
        }
    }
}
