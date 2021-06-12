using System;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public static class SegmentParser
    {
        public static (string, ImmutableList<Step>) ParseSegment(Expression expression)
        {
            if (expression is MemberExpression memberExpression)
            {
                var (rootName, steps) = ParseSegment(memberExpression.Expression);

                var successorType = memberExpression.Member.DeclaringType.FactTypeName();
                var role = memberExpression.Member.Name;
                if (memberExpression.Member is PropertyInfo proprtyInfo)
                {
                    var predecessorType = proprtyInfo.PropertyType.FactTypeName();
                    steps = steps.Add(new PredecessorStep(successorType, role, predecessorType));

                    return (rootName, steps);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (expression is ParameterExpression node)
            {
                return (node.Name, ImmutableList<Step>.Empty);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
