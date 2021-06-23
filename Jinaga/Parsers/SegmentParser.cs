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
            var (valueTag, value) = ParseValue(symbolTable, expression);
            if (value != null)
            {
                if (value is SymbolValueSetDefinition setDefinitionValue)
                {
                    if (valueTag == "this")
                    {
                        return (true, "this", setDefinitionValue.SetDefinition, ImmutableList<Step>.Empty);
                    }
                    else if (setDefinitionValue.SetDefinition.Tag != null)
                    {
                        return (true, setDefinitionValue.SetDefinition.Tag, setDefinitionValue.SetDefinition, ImmutableList<Step>.Empty);
                    }
                    else
                    {
                        return (false, valueTag, setDefinitionValue.SetDefinition, ImmutableList<Step>.Empty);
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (expression is MemberExpression {
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
        }

        private static (string, SymbolValue) ParseValue(SymbolTable symbolTable, Expression expression)
        {
            if (expression is MemberExpression {
                Member: PropertyInfo propertyInfo
            } memberExpression)
            {
                var (tag, value) = ParseValue(symbolTable, memberExpression.Expression);
                if (value is SymbolValueComposite compositeValue)
                {
                    return (propertyInfo.Name, compositeValue.GetField(propertyInfo.Name));
                }
                else
                {
                    return (null, null);
                }
            }
            if (expression is ParameterExpression parameter)
            {
                return (parameter.Name, symbolTable.GetField(parameter.Name));
            }
            else if (expression is ConstantExpression)
            {
                return ("this", symbolTable.GetField("this"));
            }
            else
            {
                return (null, null);
            }
        }
    }
}
