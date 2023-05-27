using Jinaga.Facts;
using Jinaga.Repository;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Jinaga.Serialization
{
    public class SerializerCache
    {
        public static SerializerCache Empty = new SerializerCache(
            ImmutableDictionary<Type, Delegate>.Empty
        );

        private readonly ImmutableDictionary<Type, Delegate> serializerByType;

        private SerializerCache(ImmutableDictionary<Type, Delegate> serializerByType)
        {
            this.serializerByType = serializerByType;
        }

        public int TypeCount => serializerByType.Count;

        public (SerializerCache, Func<object, Collector, Fact>) GetSerializer(Type type)
        {
            SerializerCache after = this;
            if (!serializerByType.TryGetValue(type, out var serializer))
            {
                serializer = Serialize(type).Compile();
                after = new SerializerCache(serializerByType.Add(type, serializer));
            }
            return (after, (fact, collector) => (Fact)serializer.DynamicInvoke(fact, collector));
        }

        private static LambdaExpression Serialize(Type type)
        {
            var instanceParameter = Expression.Parameter(type);
            var collectorParameter = Expression.Parameter(typeof(Collector));
            var createFactCall = Expression.Call(
                typeof(Fact).GetMethod(nameof(Fact.Create)),
                Expression.Constant(type.FactTypeName()),
                FieldList(type, instanceParameter),
                PredecessorList(type, instanceParameter, collectorParameter)
            );
            var lambda = Expression.Lambda(
                createFactCall,
                instanceParameter,
                collectorParameter
            );
            return lambda;
        }

        private static Expression FieldList(Type type, ParameterExpression instanceParameter)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Expression emptyList = Expression.Field(
                null,
                typeof(ImmutableList<Field>),
                nameof(ImmutableList<Field>.Empty)
            );
            var addMethod = typeof(ImmutableList<Field>).GetMethod(nameof(ImmutableList<Field>.Add));
            var fieldList = properties
                .Where(property => Interrogate.IsField(property.PropertyType))
                .Select(property => FieldGetter(property, instanceParameter))
                .Aggregate(emptyList, (list, field) => Expression.Call(list, addMethod, field));
            return fieldList;
        }

        private static Expression FieldGetter(PropertyInfo propertyInfo, ParameterExpression instanceParameter)
        {
            MemberExpression propertyGet = Expression.Property(instanceParameter, propertyInfo);
            NewExpression newFieldValue =
                propertyInfo.PropertyType == typeof(string)
                    ? Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), propertyGet)
                : propertyInfo.PropertyType == typeof(DateTime)
                    ? Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), CallToISO8601String(propertyGet))
                : propertyInfo.PropertyType == typeof(int)
                    ? Expression.New(typeof(FieldValueNumber).GetConstructor(new[] { typeof(double) }), ConvertToDouble(propertyGet))
                : propertyInfo.PropertyType == typeof(float)
                    ? Expression.New(typeof(FieldValueNumber).GetConstructor(new[] { typeof(double) }), ConvertToDouble(propertyGet))
                : propertyInfo.PropertyType == typeof(double)
                    ? Expression.New(typeof(FieldValueNumber).GetConstructor(new[] { typeof(double) }), propertyGet)
                : propertyInfo.PropertyType == typeof(bool)
                    ? Expression.New(typeof(FieldValueBoolean).GetConstructor(new[] { typeof(bool) }), propertyGet)
                : throw new ArgumentException($"Unsupported field type {propertyInfo.PropertyType.Name} in {propertyInfo.DeclaringType.Name}.{propertyInfo.Name}");
            NewExpression newField = Expression.New(
                typeof(Field).GetConstructor(new[] { typeof(string), typeof(FieldValue) }),
                Expression.Constant(propertyInfo.Name),
                newFieldValue
            );
            return newField;
        }

        private static Expression CallToISO8601String(MemberExpression propertyGet)
        {
            return Expression.Call(
                typeof(FieldValue).GetMethod(nameof(FieldValue.ToIso8601String)),
                propertyGet
            );
        }

        private static Expression ConvertToDouble(MemberExpression propertyGet)
        {
            return Expression.Convert(
                propertyGet,
                typeof(double)
            );
        }

        private static Expression PredecessorList(Type type, ParameterExpression instanceParameter, ParameterExpression collectorParameter)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Expression emptyList = Expression.Field(
               null,
               typeof(ImmutableList<Predecessor>),
               nameof(ImmutableList<Predecessor>.Empty)
            );
            var addMethod = typeof(ImmutableList<Predecessor>).GetMethod(nameof(ImmutableList<Predecessor>.Add));
            var predecessorList = properties
                .Where(property => Interrogate.IsPredecessor(property.PropertyType))
                .Select(property => Interrogate.IsFactType(property.PropertyType)
                    ? PredecessorSingleGetter(property, instanceParameter, collectorParameter)
                    : PredecessorMultipleGetter(property, instanceParameter, collectorParameter))
                .Aggregate(emptyList, (list, predecessor) => Expression.Call(list, addMethod, predecessor));
            return predecessorList;
        }

        private static Expression PredecessorSingleGetter(PropertyInfo propertyInfo, ParameterExpression instanceParameter, ParameterExpression collectorParameter)
        {
            return Expression.New(
                typeof(PredecessorSingle).GetConstructor(new[] { typeof(string), typeof(FactReference) }),
                Expression.Constant(propertyInfo.Name),
                Expression.Call(
                    collectorParameter,
                    typeof(Collector).GetMethod(nameof(Collector.Serialize)),
                    Expression.Convert(
                        Expression.Property(instanceParameter, propertyInfo),
                        typeof(object)
                    )
                )
            );
        }

        private static Expression PredecessorMultipleGetter(PropertyInfo propertyInfo, ParameterExpression instanceParameter, ParameterExpression collectorParameter)
        {
            var serializeMethod = typeof(SerializerCache)
                .GetMethod(nameof(SerializerCache.SerializePredecessors))
                .MakeGenericMethod(propertyInfo.PropertyType.GetElementType());

            return Expression.New(
                typeof(PredecessorMultiple).GetConstructor(new[] { typeof(string), typeof(ImmutableList<FactReference>) }),
                Expression.Constant(propertyInfo.Name),
                Expression.Call(
                    null,
                    serializeMethod,
                    Expression.Property(instanceParameter, propertyInfo),
                    collectorParameter
                )
            );
        }

        public static ImmutableList<FactReference> SerializePredecessors<T>(T[] predecessors, Collector collector)
        {
            return predecessors
                .OfType<object>()
                .Select(obj => collector.Serialize(obj))
                .ToImmutableList();

        }
    }
}