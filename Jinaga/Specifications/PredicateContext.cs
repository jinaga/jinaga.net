using System.Collections.Immutable;

namespace Jinaga.Specifications
{
    internal class PredicateContext
    {
        public ImmutableList<ConditionContext> Conditions { get; }
        
        public PredicateContext(ImmutableList<ConditionContext> conditions)
        {
            Conditions = conditions;
        }
    }
}