﻿using Jinaga.Facts;
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
        public static ImmutableList<ProjectedResult> Deserialize(
            Emitter emitter,
            Projection projection,
            Type type,
            ImmutableList<Product> products,
            string path)
        {
            if (projection is SimpleProjection simpleProjection)
                return DeserializeSimpleProjection(emitter, simpleProjection, type, products, path);
            else if (projection is CompoundProjection compoundProjection)
                return DeserializeCompoundProjection(emitter, compoundProjection, type, products, path);
            else if (projection is CollectionProjection collectionProjection)
                return DeserializeCollectionProjection(emitter, collectionProjection, type, products, path);
            else if (projection is FieldProjection fieldProjection)
                return DeserializeFieldProjection(emitter, fieldProjection, type, products, path);
            else if (projection is HashProjection hashProjection)
                return DeserializeHashProjection(emitter, hashProjection, type, products, path);
            else
                throw new ArgumentException($"Unknown projection type {projection.GetType().Name}");
        }

        private static ImmutableList<ProjectedResult> DeserializeSimpleProjection(
            Emitter emitter,
            SimpleProjection simpleProjection,
            Type type,
            ImmutableList<Product> products,
            string path)
        {
            var productProjections = ImmutableList<ProjectedResult>.Empty;
            foreach (var product in products)
            {
                var projectedResult = new ProjectedResult(
                    product,
                    emitter.DeserializeToType(product.GetFactReference(simpleProjection.Tag), type),
                    path,
                    ImmutableList<ProjectedResultChildCollection>.Empty
                );
                productProjections = productProjections.Add(projectedResult);
            }
            return productProjections;
        }

        private static ImmutableList<ProjectedResult> DeserializeCompoundProjection(
            Emitter emitter,
            CompoundProjection compoundProjection,
            Type type,
            ImmutableList<Product> products,
            string path)
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
                var productProjections = ImmutableList<ProjectedResult>.Empty;
                foreach (var product in products)
                {
                    var args = new List<object?>();
                    var collections = ImmutableList<ProjectedResultChildCollection>.Empty;
                    foreach (var parameter in parameters)
                    {
                        var projection = compoundProjection.GetProjection(parameter.Name);
                        (var obj, var children) = DeserializeParameter(emitter, projection, path, parameter.ParameterType, parameter.Name, product);
                        args.Add(obj);
                        if (children != null)
                        {
                            collections = collections.Add(children);
                        }
                    }
                    var result = constructor.Invoke(args.ToArray());
                    var projectedResult = new ProjectedResult(product, result, path, collections);
                    productProjections = productProjections.Add(projectedResult);
                }
                return productProjections;
            }
            else
            {
                var properties = type.GetProperties();
                var productProjections = products.Select(product =>
                {
                    return DeserializeProducts(emitter, compoundProjection, type, product, properties, path);
                });
                var childProductProjections =
                    from product in products
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
                    from childProductProjection in DeserializeChildParameters(emitter, collectionProjection.Projection, path, property.PropertyType, property.Name, collectionElement.Products)
                    select childProductProjection;
                return productProjections.Concat(childProductProjections).ToImmutableList();
            }
        }

        private static ProjectedResult DeserializeProducts(
            Emitter emitter,
            CompoundProjection compoundProjection,
            Type type,
            Product product,
            PropertyInfo[] properties,
            string path)
        {
            var result = Activator.CreateInstance(type);
            var collections = ImmutableList<ProjectedResultChildCollection>.Empty;
            foreach (var property in properties)
            {
                var projection = compoundProjection.GetProjection(property.Name);
                (var obj, var children) = DeserializeParameter(emitter, projection, path, property.PropertyType, property.Name, product);
                property.SetValue(result, obj);
                if (children != null)
                {
                    collections = collections.Add(children);
                }
            }
            return new ProjectedResult(product, result, path, collections);
        }

        private static ImmutableList<ProjectedResult> DeserializeCollectionProjection(Emitter emitter, CollectionProjection collectionProjection, Type type, ImmutableList<Product> products, string collectionName)
        {
            throw new NotImplementedException();
        }

        private static ImmutableList<ProjectedResult> DeserializeFieldProjection(Emitter emitter, FieldProjection fieldProjection, Type type, ImmutableList<Product> products, string path)
        {
            var propertyInfo = fieldProjection.FactRuntimeType.GetProperty(fieldProjection.FieldName);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"Field {fieldProjection.FieldName} not found on type {fieldProjection.FactRuntimeType.Name}");
            }
            var productProjections = ImmutableList<ProjectedResult>.Empty;
            foreach (var product in products)
            {
                var projectedResult = new ProjectedResult(
                    product,
                    propertyInfo.GetValue(
                        emitter.DeserializeToType(
                            product.GetFactReference(fieldProjection.Tag),
                            fieldProjection.FactRuntimeType)),
                    path,
                    ImmutableList<ProjectedResultChildCollection>.Empty
                );
                productProjections = productProjections.Add(projectedResult);
            }
            return productProjections;
        }

        private static ImmutableList<ProjectedResult> DeserializeHashProjection(Emitter emitter, HashProjection hashProjection, Type type, ImmutableList<Product> products, string path)
        {
            var productProjections = ImmutableList<ProjectedResult>.Empty;
            foreach (var product in products)
            {
                var projectedResult = new ProjectedResult(
                    product,
                    product.GetFactReference(hashProjection.Tag).Hash,
                    path,
                    ImmutableList<ProjectedResultChildCollection>.Empty
                );
                productProjections = productProjections.Add(projectedResult);
            }
            return productProjections;
        }

        private static IEnumerable<ProjectedResult> DeserializeChildParameters(
            Emitter emitter,
            Projection projection,
            string parentPath,
            Type propertyType,
            string parameterName,
            ImmutableList<Product> products)
        {
            if (emitter.WatchContext != null)
            {
                var elementType = propertyType.GetGenericArguments()[0];
                var path = string.IsNullOrEmpty(parentPath) ? parameterName : $"{parentPath}.{parameterName}";
                var productProjections = Deserialize(emitter, projection, elementType, products, path);
                return productProjections;
            }
            else
            {
                return Enumerable.Empty<ProjectedResult>();
            }
        }

        private static (object? obj, ProjectedResultChildCollection? children) DeserializeParameter(Emitter emitter, Projection projection, string parentPath, Type parameterType, string parameterName, Product product)
        {
            if (parameterType.IsFactType())
            {
                var reference = Projector.GetFactReferences(projection, product, parameterName).Single();
                var obj = emitter.DeserializeToType(reference, parameterType);
                return (obj, null);
                
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
                        var obj = ImmutableObservableCollection.Create(elementType, elements);
                        // TODO: Populate children
                        var children = new ProjectedResultChildCollection(
                            parameterName,
                            ImmutableList<ProjectedResult>.Empty
                        );
                        return (obj, children);
                    }
                    else
                    {
                        var collectionProjection = (CollectionProjection)projection;
                        var collectionElement = (CollectionElement)product.GetElement(parameterName);
                        var path = string.IsNullOrEmpty(parentPath) ? parameterName : $"{parentPath}.{parameterName}";
                        var projectedResults = Deserialize(
                            emitter,
                            collectionProjection.Projection,
                            elementType,
                            collectionElement.Products,
                            path
                        );
                        var elements = projectedResults
                            .Select(p => p.Projection)
                            .ToImmutableList();
                        var obj = ImmutableObservableCollection.Create(elementType, elements);
                        var children = new ProjectedResultChildCollection(
                            parameterName,
                            projectedResults
                        );
                        return (obj, children);
                    }
                }
                else
                {
                    var collectionProjection = (CollectionProjection)projection;
                    var collectionElement = (CollectionElement)product.GetElement(parameterName);
                    var path = string.IsNullOrEmpty(parentPath) ? parameterName : $"{parentPath}.{parameterName}";
                    var projectedResults = Deserialize(
                        emitter,
                        collectionProjection.Projection,
                        elementType,
                        collectionElement.Products,
                        path
                    );
                    var elements = projectedResults
                        .Select(p => p.Projection)
                        .ToImmutableList();
                    var obj = WatchedObservableCollection.Create(elementType, product.GetAnchor(), path, emitter.WatchContext);
                    var children = new ProjectedResultChildCollection(
                        parameterName,
                        projectedResults
                    );
                    return (obj, children);
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
                    var obj = CreateQueryable(elementType, elements);
                    // TODO: Populate children
                    var children = new ProjectedResultChildCollection(
                        parameterName,
                        ImmutableList<ProjectedResult>.Empty
                    );
                    return (obj, children);
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
                            parameterName
                        )
                        .Select(p => p.Projection)
                        .ToImmutableList();
                    var obj = CreateQueryable(elementType, elements);
                    // TODO: Populate children
                    var children = new ProjectedResultChildCollection(
                        parameterName,
                        ImmutableList<ProjectedResult>.Empty
                    );
                    return (obj, children);
                }
                else
                {
                    var obj = CreateQueryable(elementType, ImmutableList<object>.Empty);
                    var children = new ProjectedResultChildCollection(
                        parameterName,
                        ImmutableList<ProjectedResult>.Empty
                    );
                    return (obj, children);
                }
            }
            else if (projection is FieldProjection fieldProjection)
            {
                var reference = product.GetFactReference(fieldProjection.Tag);
                var fact = emitter.Graph.GetFact(reference);
                var field = fact.Fields.FirstOrDefault(f => f.Name == fieldProjection.FieldName);
                var value = field?.Value ?? FieldValue.Undefined;
                
                return DeserializeFieldValue(parameterType, parameterName, reference, value);
            }
            else if (projection is HashProjection hashProjection)
            {
                var reference = product.GetFactReference(hashProjection.Tag);
                var value = reference.Hash;
                if (parameterType == typeof(string))
                {
                    var obj = value;
                    return (obj, null);
                }
                else
                {
                    throw new ArgumentException($"A hash is expected to be a string.");
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Deserializes a field value to the specified parameter type.
        /// </summary>
        /// <param name="parameterType">The target type for deserialization.</param>
        /// <param name="parameterName">The name of the parameter being deserialized.</param>
        /// <param name="reference">The fact reference for error reporting.</param>
        /// <param name="value">The field value to deserialize.</param>
        /// <returns>A tuple containing the deserialized object and null for children.</returns>
        private static (object? obj, ProjectedResultChildCollection? children) DeserializeFieldValue(
            Type parameterType,
            string parameterName,
            FactReference reference,
            FieldValue value)
        {
            // String-based types
            if (parameterType == typeof(string))
            {
                return (value.StringValue, null);
            }
            
            // Guid types
            if (parameterType == typeof(Guid))
            {
                return (FieldValue.GuidFromString(value.StringValue), null);
            }
            if (parameterType == typeof(Guid?))
            {
                return (FieldValue.FromNullableGuidString(value.StringValue), null);
            }
            
            // Date/time types
            if (parameterType == typeof(DateTime))
            {
                return (FieldValue.FromIso8601String(value.StringValue), null);
            }
            if (parameterType == typeof(DateTime?))
            {
                return (FieldValue.FromNullableIso8601String(value.StringValue), null);
            }
            if (parameterType == typeof(DateTimeOffset))
            {
                return (FieldValue.FromIso8601StringToDateTimeOffset(value.StringValue), null);
            }
            if (parameterType == typeof(DateTimeOffset?))
            {
                return (FieldValue.FromNullableIso8601StringToNullableDateTimeOffset(value.StringValue), null);
            }
            if (parameterType == typeof(TimeSpan))
            {
                return (FieldValue.FromIso8601StringToTimeSpan(value.StringValue), null);
            }
            if (parameterType == typeof(TimeSpan?))
            {
                return (FieldValue.FromNullableIso8601StringToNullableTimeSpan(value.StringValue), null);
            }
            
            // Numeric types
            if (parameterType == typeof(int))
            {
                return ((int)value.DoubleValue, null);
            }
            if (parameterType == typeof(float))
            {
                return ((float)value.DoubleValue, null);
            }
            if (parameterType == typeof(double))
            {
                return (value.DoubleValue, null);
            }
            
            // Boolean type
            if (parameterType == typeof(bool))
            {
                return (value.BoolValue, null);
            }
            
            throw new ArgumentException($"Unknown field type {parameterType.Name}, reading field {parameterName} of {reference.Type}.");
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