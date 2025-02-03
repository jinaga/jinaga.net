using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Specifications
{
    internal class PredicateContext
    {
        public ImmutableList<ConditionContext> Conditions { get; }
        public IEnumerable<string> Labels => Conditions.SelectMany(c => c.Labels);

        public PredicateContext(ImmutableList<ConditionContext> conditions)
        {
            Conditions = conditions;
        }
    }
}