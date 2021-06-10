using System;
using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class PredicateOperandBodyVisitor : ExperimentalVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Equal)
            {
                VisitEqual(node.Left, node.Right);
            }
            else
            {
                throw new NotImplementedException();
            }

            return node;
        }

        private void VisitEqual(Expression left, Expression right)
        {
            var leftVisitor = new SegmentVisitor();
            leftVisitor.Visit(left);
            var rightVisitor = new SegmentVisitor();
            rightVisitor.Visit(right);
        }
    }
}
