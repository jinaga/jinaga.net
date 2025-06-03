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
            ValidateType(type);
            /*
            (T instance, Collector collector) =>
            {
                var reference = collector.Serialize(instance);
                return Fact.Create(
                    type.FactTypeName(),
                    FieldList(type, instance),
                    PredecessorList(type, instance, collector)
                );
            }
            */
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

        private static void ValidateType(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (properties.Any(property => property.Name == "type"))
            {
                throw new ArgumentException($"The type {type.Name} has a property named 'type'. That property name is reserved.");
            }
            if (properties.Any(property => property.Name == "Graph" && property.PropertyType != typeof(FactGraph)))
            {
                throw new ArgumentException($"The type {type.Name} has a property named 'Graph'. That property name is reserved.");
            }
            var unsupportedProperties = properties
                .Where(property =>
                    !Interrogate.IsField(property.PropertyType) &&
                    !Interrogate.IsPredecessor(property.PropertyType) &&
                    !Interrogate.IsHelper(property.PropertyType)
                )
                .ToImmutableList();
            if (unsupportedProperties.Any())
            {
                var propertyTypesAndNames = unsupportedProperties
                    .Select(property => $"{property.PropertyType.Name} {property.Name}")
                    .ToImmutableList();
                var propertyTypesAndNamesString = string.Join(", ", propertyTypesAndNames);
                throw new ArgumentException($"Unsupported properties ({propertyTypesAndNamesString}) in {type.Name}. Only fields and predecessors are supported.");
            }
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
            /*
            If the property type is string, then build the expression:
            new Field(
                "property",
                new FieldValueString(instance.Property)
            )

            If the property type is int?, then build the expression:
            new Field(
                "property",
                FieldValueNumber.From(instance.Property)
            )
            */
            MemberExpression propertyGet = Expression.Property(instanceParameter, propertyInfo);
            Type underlyingType = Nullable.GetUnderlyingType(propertyInfo.PropertyType);
            var fieldValueExpression = underlyingType != null
                ? FieldValueFromNullableExpression(propertyInfo, propertyGet, underlyingType)
                : FieldValueFromExpression(propertyInfo, propertyGet);
            NewExpression newField = Expression.New(
                typeof(Field).GetConstructor(new[] { typeof(string), typeof(FieldValue) }),
                Expression.Constant(propertyInfo.Name),
                fieldValueExpression
            );
            return newField;
        }

        private static Expression FieldValueFromNullableExpression(PropertyInfo propertyInfo, MemberExpression propertyGet, Type underlyingType)
        {
            // String-based types (stored as FieldValueString)
            if (underlyingType == typeof(string))
            {
                return Expression.Call(
                    typeof(FieldValueString).GetMethod(nameof(FieldValueString.From)),
                    propertyGet
                );
            }
            
            // Date/time types (stored as FieldValueString with ISO 8601 format)
            if (underlyingType == typeof(DateTime))
            {
                return Expression.Call(
                    typeof(FieldValueString).GetMethod(nameof(FieldValueString.From)),
                    CallNullableDateTimeToNullableIso8601String(propertyGet)
                );
            }
            if (underlyingType == typeof(DateTimeOffset))
            {
                return Expression.Call(
                    typeof(FieldValueString).GetMethod(nameof(FieldValueString.From)),
                    CallNullableDateTimeOffsetToNullableIso8601String(propertyGet)
                );
            }
            if (underlyingType == typeof(TimeSpan))
            {
                return Expression.Call(
                    typeof(FieldValueString).GetMethod(nameof(FieldValueString.From)),
                    CallNullableTimeSpanToNullableIso8601String(propertyGet)
                );
            }
            
            // Numeric types (stored as FieldValueNumber)
            if (underlyingType == typeof(int) || underlyingType == typeof(float) ||
                underlyingType == typeof(double) || underlyingType == typeof(decimal))
            {
                return Expression.Call(
                    typeof(FieldValueNumber).GetMethod(nameof(FieldValueNumber.From)),
                    ConvertToNullableDouble(propertyGet)
                );
            }
            
            // Boolean type (stored as FieldValueBoolean)
            if (underlyingType == typeof(bool))
            {
                return Expression.Call(
                    typeof(FieldValueBoolean).GetMethod(nameof(FieldValueBoolean.From)),
                    propertyGet
                );
            }
            
            // Guid type (stored as FieldValueString)
            if (underlyingType == typeof(Guid))
            {
                return Expression.Call(
                    typeof(FieldValueString).GetMethod(nameof(FieldValueString.From)),
                    CallNullableGuidToNullableString(propertyGet)
                );
            }
            
            throw new ArgumentException($"Unsupported nullable field type {underlyingType.Name} in {propertyInfo.DeclaringType?.Name}.{propertyInfo.Name}");
        }

        private static Expression FieldValueFromExpression(PropertyInfo propertyInfo, MemberExpression propertyGet)
        {
            var propertyType = propertyInfo.PropertyType;
            
            // String-based types (stored as FieldValueString)
            if (propertyType == typeof(string))
            {
                return Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), propertyGet);
            }
            
            // Date/time types (stored as FieldValueString with ISO 8601 format)
            if (propertyType == typeof(DateTime))
            {
                return Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), CallDateTimeToIso8601String(propertyGet));
            }
            if (propertyType == typeof(DateTimeOffset))
            {
                return Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), CallDateTimeOffsetToIso8601String(propertyGet));
            }
            if (propertyType == typeof(TimeSpan))
            {
                return Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), CallTimeSpanToIso8601String(propertyGet));
            }
            
            // Numeric types (stored as FieldValueNumber)
            if (propertyType == typeof(int) || propertyType == typeof(float) || propertyType == typeof(decimal))
            {
                return Expression.New(typeof(FieldValueNumber).GetConstructor(new[] { typeof(double) }), ConvertToDouble(propertyGet));
            }
            if (propertyType == typeof(double))
            {
                return Expression.New(typeof(FieldValueNumber).GetConstructor(new[] { typeof(double) }), propertyGet);
            }
            
            // Boolean type (stored as FieldValueBoolean)
            if (propertyType == typeof(bool))
            {
                return Expression.New(typeof(FieldValueBoolean).GetConstructor(new[] { typeof(bool) }), propertyGet);
            }
            
            // Guid type (stored as FieldValueString)
            if (propertyType == typeof(Guid))
            {
                return Expression.New(typeof(FieldValueString).GetConstructor(new[] { typeof(string) }), CallGuidToString(propertyGet));
            }
            
            throw new ArgumentException($"Unsupported field type {propertyType.Name} in {propertyInfo.DeclaringType?.Name}.{propertyInfo.Name}");
        }

        private static Expression CallDateTimeToIso8601String(MemberExpression propertyGet)
        {
            return Expression.Call(
                typeof(FieldValue).GetMethod(nameof(FieldValue.DateTimeToIso8601String)),
                propertyGet
            );
        }

        private static Expression CallDateTimeOffsetToIso8601String(MemberExpression propertyGet)
        {
            return Expression.Call(
                typeof(FieldValue).GetMethod(nameof(FieldValue.DateTimeOffsetToIso8601String)),
                propertyGet
            );
        }

        private static Expression CallTimeSpanToIso8601String(MemberExpression propertyGet)
        {
            return Expression.Call(
                typeof(FieldValue).GetMethod(nameof(FieldValue.TimeSpanToIso8601String)),
                propertyGet
            );
        }

        private static Expression CallNullableDateTimeToNullableIso8601String(MemberExpression propertyGet)
        {
            return Expression.Call(
                typeof(FieldValue).GetMethod(nameof(FieldValue.NullableDateTimeToNullableIso8601String)),
                propertyGet
            );
        }

        private static Expression CallNullableDateTimeOffsetToNullableIso8601String(MemberExpression propertyGet)
        {
            return Expression.Call(
                typeof(FieldValue).GetMethod(nameof(FieldValue.NullableDateTimeOffsetToNullableIso8601String)),
                propertyGet
            );
        }

        private static Expression CallNullableTimeSpanToNullableIso8601String(MemberExpression propertyGet)
        {
            return Expression.Call(
                typeof(FieldValue).GetMethod(nameof(FieldValue.NullableTimeSpanToNullableIso8601String)),
                propertyGet
            );
        }

        private static Expression CallGuidToString(MemberExpression propertyGet)
        {
            return Expression.Call(
                typeof(FieldValue).GetMethod(nameof(FieldValue.GuidToString)),
                propertyGet
            );
        }

        private static Expression CallNullableGuidToNullableString(MemberExpression propertyGet)
        {
            return Expression.Call(
                typeof(FieldValue).GetMethod(nameof(FieldValue.NullableGuidToNullableString)),
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

        private static Expression ConvertToNullableDouble(MemberExpression propertyGet)
        {
            return Expression.Convert(
                propertyGet,
                typeof(double?)
            );
        }

        private static Expression PredecessorList(Type type, ParameterExpression instanceParameter, ParameterExpression collectorParameter)
        {
            /*
            ImmutableList<Predecessor> predecessors = ImmutableList<Predecessor>.Empty;
            predecessors = predecessors.Add(new PredecessorSingle("predecessor", collector.Serialize(instance.Predecessor)));
            predecessors = predecessors.Add(new PredecessorMultiple("predecessors", collector.Serialize(instance.Predecessors)));
            return predecessors;
            */
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
            /*
            instance.Predecessor == null ? (PredecessorSingle)null : new PredecessorSingle(
                "predecessor",
                collector.Serialize(instance.Predecessor)
            )
            */
            return Expression.Condition(
                Expression.Equal(
                    Expression.Property(instanceParameter, propertyInfo),
                    Expression.Constant(null)
                ),
                Expression.Constant(null, typeof(PredecessorSingle)),
                Expression.New(
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
                )
            );
        }

        private static Expression PredecessorMultipleGetter(PropertyInfo propertyInfo, ParameterExpression instanceParameter, ParameterExpression collectorParameter)
        {
            /*
            new PredecessorMultiple(
                "predecessors",
                SerializerCache.SerializePredecessors(instance.Predecessors, collector)
            )
            */
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
            if (predecessors == null)
            {
                return ImmutableList<FactReference>.Empty;
            }
            return predecessors
                .OfType<object>()
                .Select(obj => collector.Serialize(obj))
                .ToImmutableList();

        }
    }
}