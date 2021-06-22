using System;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using Jinaga.Definitions;
using Jinaga.Pipelines;

namespace Jinaga.Parsers
{
    public static class SegmentParser
    {
        public static (bool, string, ImmutableList<Step>) ParseSegment(string setName, SetDefinition set, string initialFactName, string initialFactType, Expression expression)
        {
            if (expression is MemberExpression memberExpression)
            {
                if (memberExpression.Member is PropertyInfo proprtyInfo)
                {
                    if (memberExpression.Expression is ParameterExpression node && node.Name == setName && set is CompositeSetDefinition compositeSet)
                    {
                        var memberSet = compositeSet.GetField(memberExpression.Member.Name);
                        var tag = memberSet.Tag;
                        if (tag != null)
                        {
                            return (true, tag, ImmutableList<Step>.Empty);
                        }
                        else
                        {
                            return (false, memberExpression.Member.Name, ImmutableList<Step>.Empty);
                        }
                    }
                    else
                    {
                        var (head, tag, steps) = ParseSegment(setName, set, initialFactName, initialFactType, memberExpression.Expression);

                        var successorType = memberExpression.Member.DeclaringType.FactTypeName();
                        var role = memberExpression.Member.Name;
                        var predecessorType = proprtyInfo.PropertyType.FactTypeName();
                        if (head)
                        {
                            steps = steps.Add(new PredecessorStep(successorType, role, predecessorType));
                        }
                        else
                        {
                            steps = steps.Insert(0, new SuccessorStep(predecessorType, role, successorType));
                        }

                        return (head, tag, steps);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (expression is ParameterExpression node)
            {
                if (node.Name == initialFactName)
                {
                    return (true, node.Name, ImmutableList<Step>.Empty);
                }
                else if (node.Name == setName && set is SimpleSetDefinition)
                {
                    return (false, node.Name, ImmutableList<Step>.Empty);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (expression is ConstantExpression && initialFactName == "this")
            {
                return (true, "this", ImmutableList<Step>.Empty);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
