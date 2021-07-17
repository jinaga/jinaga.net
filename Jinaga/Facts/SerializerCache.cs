using System.Collections.Immutable;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;
using Jinaga.Parsers;

namespace Jinaga.Facts
{
    class SerializerCache
    {
        private readonly ImmutableDictionary<Type, Delegate> serializerByType;

        public SerializerCache() : this(ImmutableDictionary<Type, Delegate>.Empty)
        {
        }

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
                serializer = CreateFact(type).Compile();
                after = new SerializerCache(serializerByType.Add(type, serializer));
            }
            return (after, (fact, collector) => (Fact)serializer.DynamicInvoke(fact, collector));
        }

        private static LambdaExpression CreateFact(Type type)
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
                .Where(property => IsField(property.PropertyType))
                .Select(property => FieldGetter(property, instanceParameter))
                .Aggregate(emptyList, (list, field) => Expression.Call(list, addMethod, field));
            return fieldList;
        }

        public static bool IsField(Type type)
        {
            return
                type == typeof(string) ||
                type == typeof(DateTime) ||
                type == typeof(int) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(bool);
        }

        private static Expression FieldGetter(PropertyInfo propertyInfo, ParameterExpression instanceParameter)
        {
            MemberExpression propertyGet = Expression.Property(instanceParameter, propertyInfo);
            NewExpression newFieldValue =
                propertyInfo.PropertyType == typeof(string)
                    ? Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), propertyGet)
                //: propertyInfo.PropertyType == typeof(DateTime)
                //    ? Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), propertyGet)
                //: propertyInfo.PropertyType == typeof(int)
                //    ? Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), propertyGet)
                //: propertyInfo.PropertyType == typeof(float)
                //    ? Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), propertyGet)
                //: propertyInfo.PropertyType == typeof(double)
                //    ? Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), propertyGet)
                //: propertyInfo.PropertyType == typeof(bool)
                //    ? Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), propertyGet)
                : throw new ArgumentException($"Unsupported field type {propertyInfo.PropertyType.Name} in {propertyInfo.DeclaringType.Name}.{propertyInfo.Name}");
            NewExpression newField = Expression.New(
                typeof(Field).GetConstructor(new[] { typeof(string), typeof(FieldValue) }),
                Expression.Constant(propertyInfo.Name),
                newFieldValue
            );
            return newField;
        }

        private static Expression PredecessorList(Type type, ParameterExpression instanceParameter, ParameterExpression collectorParameter)
        {
            Expression emptyList = Expression.Field(
               null,
               typeof(ImmutableList<Predecessor>),
               nameof(ImmutableList<Predecessor>.Empty)
            );
            return emptyList;
        }
    }
}