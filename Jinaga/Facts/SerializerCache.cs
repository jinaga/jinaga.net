using System.Collections.Immutable;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Jinaga.Facts
{
    class SerializerCache
    {
        public int TypeCount { get; private set; }

        private static LambdaExpression FieldGetter(PropertyInfo propertyInfo)
        {
            ParameterExpression instanceParam = Expression.Parameter(propertyInfo.DeclaringType);
            MethodInfo getMethod = propertyInfo.GetGetMethod();
            MethodCallExpression methodCall = Expression.Call(instanceParam, getMethod);
            if (!fieldSerializers.TryGetValue(propertyInfo.PropertyType, out var fieldSerializer))
            {
                throw new ArgumentException($"Unsupported field type {propertyInfo.PropertyType.Name} in {propertyInfo.DeclaringType.Name}.{propertyInfo.Name}");
            }
            LambdaExpression lambda = Expression.Lambda(methodCall, new [] { instanceParam });
            return lambda;
        }

        private static ImmutableDictionary<Type, Expression> fieldSerializers =
            ImmutableDictionary<Type, Expression>.Empty;

        static SerializerCache()
        {
            AddFieldSerializer(
                typeof(string),
                (name, value) => new Field(name, new FieldValueString((string)value))
            );
        }

        private static void AddFieldSerializer(Type type, Expression<Func<string, object, Field>> serializer)
        {
            fieldSerializers = fieldSerializers.Add(type, serializer);
        }
    }
}