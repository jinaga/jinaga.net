using System.Collections.Generic;

namespace Jinaga.Specifications
{
    internal abstract class ConditionContext
    {
        public abstract IEnumerable<string> Labels { get; }
    }
}