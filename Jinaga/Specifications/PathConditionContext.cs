using System.Collections.Generic;

namespace Jinaga.Specifications
{
    internal class PathConditionContext : ConditionContext
    {
        public ReferenceContext Left { get; }
        public ReferenceContext Right { get; }
        public override IEnumerable<string> Labels => new string[]{};
        
        public PathConditionContext(ReferenceContext left, ReferenceContext right)
        {
            Left = left;
            Right = right;
        }
    }
}