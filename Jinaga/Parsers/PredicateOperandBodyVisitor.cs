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
            var (leftRootName, leftSteps) = SegmentParser.ParseSegment(left);
            var (rightRootName, rightSteps) = SegmentParser.ParseSegment(right);

            if (leftRootName == parameterName)
            {
                ClosureName = rightRootName;
                Steps = rightSteps.AddRange(ReflectAll(leftSteps));
            }
            else if (rightRootName == parameterName)
            {
                ClosureName = leftRootName;
                Steps = leftSteps.AddRange(ReflectAll(rightSteps));
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
