using System;
using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class PredicateVisitor : ExperimentalVisitor
    {
        protected override Expression VisitUnary(UnaryExpression node)
        {
            var operand = node.Operand;
            var predicateOperandVisitor = new PredicateOperandVisitor();
            predicateOperandVisitor.Visit(operand);

            return node;
        }
    }
}
