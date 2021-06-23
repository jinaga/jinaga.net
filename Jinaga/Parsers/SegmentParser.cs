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
        public static (bool, string, SetDefinition, ImmutableList<Step>) ParseSegment(SymbolTable symbolTable, Expression expression)
        {
            if (expression is MemberExpression memberExpression)
            {
                if (memberExpression.Member is PropertyInfo proprtyInfo)
                {
                    if (memberExpression.Expression is ParameterExpression node)
                    {
                        var value = symbolTable.GetField(node.Name);
                        if (value is SymbolValueComposite compositeValue)
                        {
                            var memberSet = compositeValue.GetField(memberExpression.Member.Name);
                            var tag = memberSet.Tag;
                            if (tag != null)
                            {
                                return (true, tag, memberSet, ImmutableList<Step>.Empty);
                            }
                            else
                            {
                                return (false, memberExpression.Member.Name, null, ImmutableList<Step>.Empty);
                            }
                        }
                        else if (value is SymbolValueSetDefinition setDefinitionValue)
                        {
                            var successorType = memberExpression.Member.DeclaringType.FactTypeName();
                            var role = memberExpression.Member.Name;
                            var predecessorType = proprtyInfo.PropertyType.FactTypeName();
                            var tag = setDefinitionValue.SetDefinition.Tag;
                            if (tag != null)
                            {
                                return (true, tag, setDefinitionValue.SetDefinition, ImmutableList<Step>.Empty.Add(new PredecessorStep(successorType, role, predecessorType)));
                            }
                            else
                            {
                                return (false, node.Name, null, ImmutableList<Step>.Empty.Insert(0, new SuccessorStep(predecessorType, role, successorType)));
                            }
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        var (head, tag, startingSet, steps) = ParseSegment(symbolTable, memberExpression.Expression);

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

                        return (head, tag, startingSet, steps);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (expression is ParameterExpression node)
            {
                var value = symbolTable.GetField(node.Name);
                if (value is SymbolValueSetDefinition setDefinitionValue)
                {
                    var tag = setDefinitionValue.SetDefinition.Tag;
                    if (tag != null)
                    {
                        return (true, tag, setDefinitionValue.SetDefinition, ImmutableList<Step>.Empty);
                    }
                    else
                    {
                        return (false, node.Name, null, ImmutableList<Step>.Empty);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (expression is ConstantExpression)
            {
                return (true, "this", ((SymbolValueSetDefinition)symbolTable.GetField("this")).SetDefinition, ImmutableList<Step>.Empty);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
