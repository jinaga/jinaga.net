using System;
using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class PredicateOperandBodyVisitor : ExperimentalVisitor
    {
        private readonly string parameterName;

        public PredicateOperandBodyVisitor(string parameterName)
        {
            this.parameterName = parameterName;
        }

        public string ClosureName { get; private set; }

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
            if (leftVisitor.RootName != parameterName)
            {
                ClosureName = leftVisitor.RootName;
            }

            var rightVisitor = new SegmentVisitor();
            rightVisitor.Visit(right);
            if (rightVisitor.RootName != parameterName)
            {
                ClosureName = rightVisitor.RootName;
            }
        }
    }
}
