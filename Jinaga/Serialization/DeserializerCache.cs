using Jinaga.Facts;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Serialization
{
    class DeserializerCache
    {
        private readonly ImmutableDictionary<Type, Delegate> deserializerByType;

        public DeserializerCache() : this(ImmutableDictionary<Type, Delegate>.Empty)
        {
        }

        private DeserializerCache(ImmutableDictionary<Type, Delegate> deserializerByType)
        {
            this.deserializerByType = deserializerByType;
        }

        public (DeserializerCache, Func<Fact, Emitter, object>) GetDeserializer(Type type)
        {
            DeserializerCache after = this;
            if (!deserializerByType.TryGetValue(type, out var deserializer))
            {
                deserializer = Deserialize(type).Compile();
                after = new DeserializerCache(deserializerByType.Add(type, deserializer));
            }
            return (after, (fact, emitter) => deserializer.DynamicInvoke(fact, emitter));
        }

        private static LambdaExpression Deserialize(Type type)
        {
            var factParameter = Expression.Parameter(typeof(Fact));
            var emitterParameter = Expression.Parameter(typeof(Emitter));
            return Expression.Lambda(
                CreateObject(type, factParameter, emitterParameter),
                factParameter,
                emitterParameter
            );
        }

        private static Expression CreateObject(Type type, ParameterExpression factParameter, ParameterExpression emitterParameter)
        {
            var constructor = type.GetConstructors().Single();
            var parameters = constructor.GetParameters();
            var parameterExpressions = parameters
                .Select(parameter =>
                    Interrogate.IsField(parameter.ParameterType)
                        ? GetFieldValue(parameter.Name, parameter.ParameterType, factParameter) :
                    Interrogate.IsFactType(parameter.ParameterType)
                        ? GetPredecessor(parameter.Name, parameter.ParameterType, factParameter, emitterParameter) :
                    Interrogate.IsArrayOfFactType(parameter.ParameterType)
                        ? GetPredecessorArray(parameter.Name, parameter.ParameterType.GetElementType(), factParameter, emitterParameter) :
                    throw new NotImplementedException()
                )
                .ToArray();
            return Expression.New(
                constructor,
                parameterExpressions
            );
        }

        private static Expression GetFieldValue(string name, Type parameterType, ParameterExpression factParameter)
        {
            var getFieldValueMethod = typeof(Fact).GetMethod(nameof(Fact.GetFieldValue));
            var getFieldValue = Expression.Call(
                factParameter,
                getFieldValueMethod,
                Expression.Constant(name)
            );
            if (parameterType == typeof(string))
            {
                return Expression.Property(
                    Expression.Convert(
                        getFieldValue,
                        typeof(FieldValueString)
                    ),
                    nameof(FieldValueString.StringValue)
                );
            }
            else if (parameterType == typeof(DateTime))
            {
                return Expression.Call(
                    typeof(FieldValue).GetMethod(nameof(FieldValue.FromIso8601String)),
                    Expression.Property(
                        Expression.Convert(
                            getFieldValue,
                            typeof(FieldValueString)
                        ),
                        nameof(FieldValueString.StringValue)
                    )
                );
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static Expression GetPredecessor(string role, Type parameterType, ParameterExpression factParameter, ParameterExpression emitterParameter)
        {
            var predecessorSingle = Expression.Call(
                factParameter,
                typeof(Fact).GetMethod(nameof(Fact.GetPredecessorSingle)),
                Expression.Constant(role)
            );
            var deserialize = Expression.Call(
                emitterParameter,
                typeof(Emitter).GetMethod(nameof(Emitter.Deserialize)).MakeGenericMethod(parameterType),
                predecessorSingle
            );
            return deserialize;
        }

        private static Expression GetPredecessorArray(string name, Type elementType, ParameterExpression factParameter, ParameterExpression emitterParameter)
        {
            throw new NotImplementedException();
        }

        /*
        private static object GetFieldValue(Type parameterType, FieldValue value)
        {
            switch (value)
            {
                case FieldValueString str:
                    return
                        parameterType == typeof(string)
                            ? str.StringValue :
                        parameterType == typeof(DateTime)
                            ? (object)DateTime.Parse(str.StringValue).ToUniversalTime() :
                        throw new NotImplementedException();
                case FieldValueNumber number:
                    return
                        parameterType == typeof(int)
                            ? (object)(int)number.DoubleValue :
                        parameterType == typeof(float)
                            ? (object)(float)number.DoubleValue :
                        parameterType == typeof(double)
                            ? (object)(double)number.DoubleValue :
                        throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }

        private static object GetPredecessorValue(Type parameterType, Predecessor predecessor, Emitter emitter)
        {
            switch (predecessor)
            {
                case PredecessorSingle single:
                    return emitter.Deserialize(single.Reference, parameterType);
                case PredecessorMultiple multiple:
                    var elementType = parameterType.GetElementType();
                    var facts = multiple.References.Select(r =>
                        emitter.Deserialize(r, elementType)
                    ).ToArray();
                    var value = (Array)Activator.CreateInstance(parameterType, facts.Length);
                    Array.Copy(facts, value, facts.Length);
                    return value;
                default:
                    throw new NotImplementedException();
            }
        }
        */
    }
}
