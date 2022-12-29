using Jinaga.Facts;
using Jinaga.Observers;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Repository;
using Jinaga.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Jinaga.Managers
{
    class Deserializer
    {
        public static ImmutableList<ProductAnchorProjection> Deserialize(
            Emitter emitter,
            Projection projection,
            Type type,
            ImmutableList<Product> products,
            Product anchor,
            string collectionName)
        {
            if (projection is SimpleProjection simpleProjection)
                return DeserializeSimpleProjection(emitter, simpleProjection, type, products, anchor, collectionName);
            else if (projection is CompoundProjection compoundProjection)
                return DeserializeCompoundProjection(emitter, compoundProjection, type, products, anchor, collectionName);
            else if (projection is CollectionProjection collectionProjection)
                return DeserializeCollectionProjection(emitter, collectionProjection, type, products, anchor, collectionName);
            else if (projection is FieldProjection fieldProjection)
                return DeserializeFieldProjection(emitter, fieldProjection, type, products, anchor, collectionName);
            else
                throw new ArgumentException($"Unknown projection type {projection.GetType().Name}");
        }

        private static ImmutableList<ProductAnchorProjection> DeserializeSimpleProjection(
            Emitter emitter,
            SimpleProjection simpleProjection,
            Type type,
            ImmutableList<Product> products,
            Product anchor,
            string collectionName)
        {
            var productProjections = products
                .Select(product => new ProductAnchorProjection(
                    product,
                    anchor,
                    emitter.DeserializeToType(product.GetFactReference(simpleProjection.Tag), type),
                    collectionName
                ))
                .ToImmutableList();
            return productProjections;
        }

        private static ImmutableList<ProductAnchorProjection> DeserializeCompoundProjection(
            Emitter emitter,
            CompoundProjection compoundProjection,
            Type type,
            ImmutableList<Product> products,
            Product anchor,
            string collectionName)
        {
            var constructorInfos = type.GetConstructors();
            if (constructorInfos.Length != 1)
            {
                throw new NotImplementedException($"Multiple constructors for {type.Name}");
            }
            var constructor = constructorInfos.Single();
            var parameters = constructor.GetParameters();
            if (parameters.Any())
            {
                var productProjections =
                    from product in products
                    let result = constructor.Invoke((
                        from parameter in parameters
                        let projection = compoundProjection.GetProjection(parameter.Name)
                        select DeserializeParameter(emitter, projection, parameter.ParameterType, parameter.Name, product)
                    ).ToArray())
                    select new ProductAnchorProjection(product, anchor, result, collectionName);
                return productProjections.ToImmutableList();
            }
            else
            {
                var properties = type.GetProperties();
                var productProjections = products.Select(product =>
                {
                    return DeserializeProducts(emitter, compoundProjection, type, product, properties, anchor, collectionName);
                });
                var childProductProjections =
                    from product in products
                    let parentAnchor = product.GetAnchor()
                    from property in properties
                    where
                        !property.PropertyType.IsFactType() &&
                        property.PropertyType.IsGenericType &&
                        property.PropertyType.GetGenericTypeDefinition() == typeof(IObservableCollection<>)
                    let projection = compoundProjection.GetProjection(property.Name)
                    where projection is CollectionProjection
                    let collectionProjection = (CollectionProjection)projection
                    where product.Names.Contains(property.Name)
                    let element = product.GetElement(property.Name)
                    where element is CollectionElement
                    let collectionElement = (CollectionElement)element
                    from childProductProjection in DeserializeChildParameters(emitter, collectionProjection.Projection, property.PropertyType, property.Name, collectionElement.Products, parentAnchor)
                    select childProductProjection;
                return productProjections.Concat(childProductProjections).ToImmutableList();
            }
        }

        private static ProductAnchorProjection DeserializeProducts(
            Emitter emitter,
            CompoundProjection compoundProjection,
            Type type,
            Product product,
            PropertyInfo[] properties,
            Product anchor,
            string collectionName)
        {
            var result = Activator.CreateInstance(type);
            foreach (var property in properties)
            {
                var projection = compoundProjection.GetProjection(property.Name);
                var value = DeserializeParameter(emitter, projection, property.PropertyType, property.Name, product);
                property.SetValue(result, value);
            }
            return new ProductAnchorProjection(product, anchor, result, collectionName);
        }

        private static ImmutableList<ProductAnchorProjection> DeserializeCollectionProjection(Emitter emitter, CollectionProjection collectionProjection, Type type, ImmutableList<Product> products, Product anchor, string collectionName)
        {
            throw new NotImplementedException();
        }

        private static ImmutableList<ProductAnchorProjection> DeserializeFieldProjection(Emitter emitter, FieldProjection fieldProjection, Type type, ImmutableList<Product> products, Product anchor, string collectionName)
        {
            var propertyInfo = fieldProjection.FactRuntimeType.GetProperty(fieldProjection.FieldName);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"Field {fieldProjection.FieldName} not found on type {fieldProjection.FactRuntimeType.Name}");
            }
            var productProjections = products
                .Select(product => new ProductAnchorProjection(
                    product,
                    anchor,
                    propertyInfo.GetValue(
                        emitter.DeserializeToType(
                            product.GetFactReference(fieldProjection.Tag),
                            fieldProjection.FactRuntimeType)),
                    collectionName
                ))
                .ToImmutableList();
            return productProjections;
        }

        private static IEnumerable<ProductAnchorProjection> DeserializeChildParameters(
            Emitter emitter,
            Projection projection,
            Type propertyType,
            string parameterName,
            ImmutableList<Product> products,
            Product anchor)
        {
            if (emitter.WatchContext != null)
            {
                var elementType = propertyType.GetGenericArguments()[0];
                var productProjections = Deserialize(emitter, projection, elementType, products, anchor, parameterName);
                return productProjections;
            }
            else
            {
                return Enumerable.Empty<ProductAnchorProjection>();
            }
        }

        private static object DeserializeParameter(Emitter emitter, Projection projection, Type parameterType, string parameterName, Product product)
        {
            if (parameterType.IsFactType())
            {
                var reference = Projector.GetFactReferences(projection, product, parameterName).Single();
                return emitter.DeserializeToType(reference, parameterType);
            }
            else if (parameterType.IsGenericType &&
                parameterType.GetGenericTypeDefinition() == typeof(IObservableCollection<>))
            {
                var elementType = parameterType.GetGenericArguments()[0];
                if (emitter.WatchContext == null)
                {
                    if (elementType.IsFactType())
                    {
                        var elements = Projector.GetFactReferences(projection, product, parameterName)
                            .Select(reference => emitter.DeserializeToType(reference, elementType))
                            .ToImmutableList();
                        return ImmutableObservableCollection.Create(elementType, elements);
                    }
                    else
                    {
                        var collectionProjection = (CollectionProjection)projection;
                        var collectionElement = (CollectionElement)product.GetElement(parameterName);
                        var elements = Deserialize(
                                emitter,
                                collectionProjection.Projection,
                                elementType,
                                collectionElement.Products,
                                product.GetAnchor(),
                                parameterName
                            )
                            .Select(p => p.Projection)
                            .ToImmutableList();
                        return ImmutableObservableCollection.Create(elementType, elements);
                    }
                }
                else
                {
                    return WatchedObservableCollection.Create(elementType, product.GetAnchor(), parameterName, emitter.WatchContext);
                }
            }
            else if (parameterType.IsGenericType &&
                parameterType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                var elementType = parameterType.GetGenericArguments()[0];
                if (elementType.IsFactType())
                {
                    var elements = Projector.GetFactReferences(projection, product, parameterName)
                        .Select(reference => emitter.DeserializeToType(reference, elementType))
                        .ToImmutableList();
                    return CreateQueryable(elementType, elements);
                }
                else if (product.Contains(parameterName))
                {
                    var collectionProjection = (CollectionProjection)projection;
                    var collectionElement = (CollectionElement)product.GetElement(parameterName);
                    var elements = Deserialize(
                            emitter,
                            collectionProjection.Projection,
                            elementType,
                            collectionElement.Products,
                            product.GetAnchor(),
                            parameterName
                        )
                        .Select(p => p.Projection)
                        .ToImmutableList();
                    return CreateQueryable(elementType, elements);
                }
                else
                {
                    return CreateQueryable(elementType, ImmutableList<object>.Empty);
                }
            }
            else if (projection is FieldProjection fieldProjection)
            {
                var reference = product.GetFactReference(fieldProjection.Tag);
                var fact = emitter.Graph.GetFact(reference);
                var field = fact.Fields.FirstOrDefault(f => f.Name == fieldProjection.FieldName);
                if (field == null)
                {
                    throw new ArgumentException($"Unknown field {fieldProjection.FieldName} in fact {reference.Type}");
                }
                var value = field.Value;
                if (value is FieldValueString fieldValueString)
                {
                    if (parameterType == typeof(string))
                    {
                        return fieldValueString.StringValue;
                    }
                    else if (parameterType == typeof(DateTime))
                    {
                        return FieldValue.FromIso8601String(fieldValueString.StringValue);
                    }
                    else
                    {
                        throw new ArgumentException($"Cannot convert string to {parameterType.Name}, reading field {parameterName} of {reference.Type}.");
                    }
                }
                else if (value is FieldValueNumber fieldValueNumber)
                {
                    if (parameterType == typeof(int))
                    {
                        return (int)fieldValueNumber.DoubleValue;
                    }
                    else if (parameterType == typeof(float))
                    {
                        return (float)fieldValueNumber.DoubleValue;
                    }
                    else if (parameterType == typeof(double))
                    {
                        return fieldValueNumber.DoubleValue;
                    }
                    else
                    {
                        throw new ArgumentException($"Cannot convert number to {parameterType.Name}, reading field {parameterName} of {reference.Type}.");
                    }
                }
                else if (value is FieldValueBoolean fieldValueBoolean)
                {
                    if (parameterType == typeof(bool))
                    {
                        return fieldValueBoolean.BoolValue;
                    }
                    else
                    {
                        throw new ArgumentException($"Cannot convert boolean to {parameterType.Name}, reading field {parameterName} of {reference.Type}.");
                    }
                }
                else
                {
                    throw new ArgumentException($"Unknown field type {value.GetType().Name}, reading field {parameterName} of {reference.Type}.");
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static object CreateQueryable(Type elementType, ImmutableList<object> elements)
        {
            var test = elements.AsQueryable();
            var method = typeof(Deserializer)
                .GetMethod(nameof(CreateQueryableGeneric), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(elementType);
            return method.Invoke(null, new[] { elements });
        }

        private static IQueryable<T> CreateQueryableGeneric<T>(ImmutableList<object> elements)
        {
            return elements.OfType<T>().AsQueryable();
        }
    }
}