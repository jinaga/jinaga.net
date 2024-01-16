using Jinaga.Facts;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;

namespace Jinaga.Serialization
{
    class DeserializerCache
    {
        public static DeserializerCache Empty = new DeserializerCache(
            ImmutableDictionary<Type, Delegate>.Empty
        );

        private readonly ImmutableDictionary<Type, Delegate> deserializerByType;

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
            var constructorInfos = type.GetConstructors();
            if (constructorInfos.Length != 1)
            {
                throw new NotImplementedException($"More than one constructor for {type.Name}");
            }
            var constructor = constructorInfos.Single();
            var parameters = constructor.GetParameters();
            var parameterExpressions = parameters
                .Select(parameter =>
                    Interrogate.IsField(parameter.ParameterType)
                        ? GetFieldValue(parameter.Name, parameter.ParameterType, factParameter) :
                    Interrogate.IsFactType(parameter.ParameterType)
                        ? GetPredecessor(parameter.Name, parameter.ParameterType, factParameter, emitterParameter) :
                    Interrogate.IsArrayOfFactType(parameter.ParameterType)
                        ? GetPredecessorArray(parameter.Name, parameter.ParameterType.GetElementType(), factParameter, emitterParameter) :
                    throw new ArgumentException($"Unknown parameter type {parameter.ParameterType.Name} for {type.Name} constructor, parameter {parameter.Name}")
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
                    getFieldValue,
                    nameof(FieldValue.StringValue)
                );
            }
            else if (parameterType == typeof(DateTime))
            {
                return Expression.Call(
                    typeof(FieldValue).GetMethod(nameof(FieldValue.FromIso8601String)),
                    Expression.Property(
                        getFieldValue,
                        nameof(FieldValue.StringValue)
                    )
                );
            }
            else if (parameterType == typeof(int))
            {
                return Expression.Convert(
                    Expression.Property(
                        getFieldValue,
                        nameof(FieldValue.DoubleValue)
                    ),
                    typeof(int)
                );
            }
            else if (parameterType == typeof(float))
            {
                return Expression.Convert(
                    Expression.Property(
                        getFieldValue,
                        nameof(FieldValue.DoubleValue)
                    ),
                    typeof(float)
                );
            }
            else if (parameterType == typeof(double))
            {
                return Expression.Property(
                    getFieldValue,
                    nameof(FieldValue.DoubleValue)
                );
            }
            else if (parameterType == typeof(bool))
            {
                return Expression.Property(
                    getFieldValue,
                    nameof(FieldValue.BoolValue)
                );
            }
            else
            {
                throw new ArgumentException($"Unknown field type {parameterType.Name}");
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

        private static Expression GetPredecessorArray(string role, Type elementType, ParameterExpression factParameter, ParameterExpression emitterParameter)
        {
            var predecessorMultiple = Expression.Call(
                factParameter,
                typeof(Fact).GetMethod(nameof(Fact.GetPredecessorMultiple)),
                Expression.Constant(role)
            );
            var deserialize = Expression.Call(
                emitterParameter,
                typeof(Emitter).GetMethod(nameof(Emitter.DeserializeArray)).MakeGenericMethod(elementType),
                predecessorMultiple
            );
            return deserialize;
        }
    }
}
