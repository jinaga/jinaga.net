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
            Validate(specification);
            return new ShareTarget<T>(specification, distributionRules);
        }

        public ShareTarget<T, U> Share<T, U, V>(Specification<T, U, V> specification)
            where T : class
            where U : class
        {
            Validate(specification);
            return new ShareTarget<T, U>(specification, distributionRules);
        }

        /// <summary>
        /// Refuses a rule a replicator could not load, at the call that writes it.
        ///
        /// <para>
        /// A replicator decides whether to serve a feed by comparing its skeleton with the
        /// skeletons of the feeds of every distribution rule, so a rule whose feeds or whose
        /// skeletons cannot be built is a rule it cannot answer with. Storing it and
        /// discovering that at <see cref="Describe"/> time, or on a deployed client as a 403
        /// or an empty result, puts the failure arbitrarily far from the mistake; jinaga.js
        /// builds the feeds of a rule in <c>share</c> for the same reason
        /// (<c>buildFeedsOfRule</c> in <c>src/distribution/distribution-rules.ts</c>).
        /// </para>
        /// </summary>
        /// <exception cref="SpecificationException">
        /// The specification has no feeds (<see cref="FeedBuilder.BuildFeeds"/> says why), or
        /// one of them has no skeleton.
        /// </exception>
        private static void Validate(Specification specification)
        {
            ImmutableList<Specification> feeds;
            try
            {
                feeds = FeedBuilder.BuildFeeds(specification);
            }
            catch (SpecificationException exception)
            {
                throw new SpecificationException(Refusal(specification, exception.Message), exception);
            }
            foreach (var feed in feeds)
            {
                try
                {
                    Skeleton.Of(feed);
                }
                catch (SpecificationException exception)
                {
                    throw new SpecificationException(Refusal(specification, exception.Message), exception);
                }
            }
        }

        /// <summary>
        /// The refusal, naming the rule by the specification it shares. A rule has no other
        /// name: <see cref="Share{T, U}"/> is handed a specification and nothing else, and the
        /// user it is shared with is not chosen until <c>With</c> or <c>WithEveryone</c>.
        /// </summary>
        private static string Refusal(Specification specification, string reason) =>
            "A replicator could not load this distribution rule, so it is refused here, " +
            $"where it is written. {reason}\n\nThe rule shares:\n{specification.ToDescriptiveString(1)}";

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
