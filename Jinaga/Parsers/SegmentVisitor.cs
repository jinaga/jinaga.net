using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class SegmentVisitor : ExperimentalVisitor
    {
        public string RootName { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            var headVisitor = new SegmentVisitor();
            headVisitor.Visit(node.Expression);
            RootName = headVisitor.RootName;

            var role = node.Member.Name;
            var predecessorType = node.Member.ReflectedType.FactTypeName();
            
            return node;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            RootName = node.Name;

            return node;
        }
    }
}
