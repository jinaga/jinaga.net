using System;
using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class PredicateVisitor : ExperimentalVisitor
    {
        public string Tag { get; private set; }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            var operand = node.Operand;
            var predicateOperandVisitor = new PredicateOperandVisitor();
            predicateOperandVisitor.Visit(operand);

            Tag = predicateOperandVisitor.ParameterName;

            return node;
        }
    }
}
