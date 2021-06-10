using System;
using System.Linq.Expressions;

namespace Jinaga.Parsers
{
    public class PredicateVisitor : ExperimentalVisitor
    {
        public string Tag { get; private set; }
        public string TargetType { get; private set; }
        public string StartingTag { get; private set; }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            var operand = node.Operand;
            var predicateOperandVisitor = new PredicateOperandVisitor();
            predicateOperandVisitor.Visit(operand);

            Tag = predicateOperandVisitor.ParameterName;
            TargetType = predicateOperandVisitor.ParameterTypeName;
            StartingTag = predicateOperandVisitor.ClosureName;

            return node;
        }
    }
}
