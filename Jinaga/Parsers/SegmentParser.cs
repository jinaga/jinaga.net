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
            switch (ValueParser.ParseValue(symbolTable, expression))
            {
                case (string valueTag, SymbolValueSetDefinition setDefinitionValue):
                    if (valueTag == "this")
                    {
                        return (true, "this", setDefinitionValue.SetDefinition, ImmutableList<Step>.Empty);
                    }
                    else if (setDefinitionValue.SetDefinition.IsInitialized)
                    {
                        return (true, setDefinitionValue.SetDefinition.Tag, setDefinitionValue.SetDefinition, ImmutableList<Step>.Empty);
                    }
                    else
                    {
                        return (false, valueTag, setDefinitionValue.SetDefinition, ImmutableList<Step>.Empty);
                    }
                case null:
                    if (expression is MemberExpression {
                        Member: PropertyInfo propertyInfo
                    } memberExpression)
                    {
                        var (head, tag, startingSet, steps) = ParseSegment(symbolTable, memberExpression.Expression);

                        var successorType = memberExpression.Member.DeclaringType.FactTypeName();
                        var role = memberExpression.Member.Name;
                        var predecessorType = propertyInfo.PropertyType.FactTypeName();
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
                    else
                    {
                        throw new NotImplementedException();
                    }
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
