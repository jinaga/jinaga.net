using System.Collections.Immutable;
using System.Linq.Expressions;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public class SegmentVisitor : ExperimentalVisitor
    {
        public string RootName { get; private set; }
        public ImmutableList<Step> Steps { get; private set; } = ImmutableList<Step>.Empty;

        protected override Expression VisitMember(MemberExpression node)
        {
            var headVisitor = new SegmentVisitor();
            headVisitor.Visit(node.Expression);
            RootName = headVisitor.RootName;

            var successorType = node.Member.DeclaringType.FactTypeName();
            var role = node.Member.Name;
            var predecessorType = node.Member.ReflectedType.FactTypeName();
            Steps = headVisitor.Steps.Add(new PredecessorStep(successorType, role, predecessorType));
            
            return node;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            RootName = node.Name;

            return node;
        }
    }
}
