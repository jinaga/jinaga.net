using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class SegmentVisitor : ExperimentalVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            var headVisitor = new SegmentVisitor();
            headVisitor.Visit(node.Expression);
            var role = node.Member.Name;
            var predecessorType = node.Member.ReflectedType.FactTypeName();
            
            return node;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            var parameterName = node.Name;

            return node;
        }
    }
}
