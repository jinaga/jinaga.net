using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public class PredicateOperandBodyVisitor : ExperimentalVisitor
    {
        private readonly string parameterName;

        public string ClosureName { get; private set; }
        public ImmutableList<Step> Steps { get; private set; }

        public PredicateOperandBodyVisitor(string parameterName)
        {
            this.parameterName = parameterName;
        }

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

            if (leftVisitor.RootName == parameterName)
            {
                ClosureName = rightVisitor.RootName;
                Steps = rightVisitor.Steps.AddRange(ReflectAll(leftVisitor.Steps));
            }
            else if (rightVisitor.RootName == parameterName)
            {
                ClosureName = leftVisitor.RootName;
                Steps = leftVisitor.Steps.AddRange(ReflectAll(rightVisitor.Steps));
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private IEnumerable<Step> ReflectAll(ImmutableList<Step> steps)
        {
            return steps.Reverse().Select(step => step.Reflect()).ToImmutableList();
        }
    }
}
