using System;
using System.Collections.Immutable;
using System.Linq.Expressions;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public class PredicateVisitor : ExperimentalVisitor
    {
        public string Tag { get; private set; }
        public string TargetType { get; private set; }
        public string StartingTag { get; private set; }
        public ImmutableList<Step> Steps { get; private set; }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            var operand = node.Operand;
            var predicateOperandVisitor = new PredicateOperandVisitor();
            predicateOperandVisitor.Visit(operand);

            Tag = predicateOperandVisitor.ParameterName;
            TargetType = predicateOperandVisitor.ParameterTypeName;
            StartingTag = predicateOperandVisitor.ClosureName;
            Steps = predicateOperandVisitor.Steps;

            return node;
        }
    }
}
