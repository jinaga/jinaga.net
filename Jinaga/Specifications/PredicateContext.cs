using System.Collections.Immutable;

namespace Jinaga.Specifications
{
    public class PredicateContext
    {
        public ImmutableList<ConditionContext> Conditions { get; }
        
        public PredicateContext(ImmutableList<ConditionContext> conditions)
        {
            Conditions = conditions;
        }
    }
}