using Jinaga.Observers;
using Jinaga.Parsers;
using Jinaga.Products;
using Jinaga.Projections;
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
        public static ImmutableList<ProductProjection> Deserialize(Emitter emitter, Projection projection, Type type, ImmutableList<Product> products)
        {
            if (projection is SimpleProjection simpleProjection)
                return DeserializeSimpleProjection(emitter, simpleProjection, type, products);
            else if (projection is CompoundProjection compoundProjection)
                return DeserializeCompoundProjection(emitter, compoundProjection, type, products);
            else if (projection is CollectionProjection collectionProjection)
                return DeserializeCollectionProjection(emitter, collectionProjection, type, products);
            else
                throw new NotImplementedException();
        }

        private static ImmutableList<ProductProjection> DeserializeSimpleProjection(Emitter emitter, SimpleProjection simpleProjection, Type type, ImmutableList<Product> products)
        {
            var productProjections = products
                .Select(product => new ProductProjection(product,
                    emitter.DeserializeToType(product.GetFactReference(simpleProjection.Tag), type)
                ))
                .ToImmutableList();
            return productProjections;
        }

        private static ImmutableList<ProductProjection> DeserializeCompoundProjection(Emitter emitter, CompoundProjection compoundProjection, Type type, ImmutableList<Product> products)
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
                    select new ProductProjection(product, result);
                return productProjections.ToImmutableList();
            }
            else
            {
                var properties = type.GetProperties();
                var productProjections = products.Select(product =>
                {
                    return DeserializeProducts(emitter, compoundProjection, type, product, properties);
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
                    let element = product.GetElement(property.Name)
                    where element is CollectionElement
                    let collectionElement = (CollectionElement)element
                    from childProduct in collectionElement.Products
                    from childProductProjection in DeserializeChildParameters(emitter, collectionProjection.Specification.Projection, property.PropertyType, property.Name, childProduct)
                    select childProductProjection;
                return productProjections.Concat<ProductProjection>(childProductProjections).ToImmutableList();
            }
        }

        private static ProductProjection DeserializeProducts(Emitter emitter, CompoundProjection compoundProjection, Type type, Product product, PropertyInfo[] properties)
        {
            var result = Activator.CreateInstance(type);
            foreach (var property in properties)
            {
                var projection = compoundProjection.GetProjection(property.Name);
                var value = DeserializeParameter(emitter, projection, property.PropertyType, property.Name, product);
                property.SetValue(result, value);
            }
            return new ProductProjection(product, result);
        }

        private static ImmutableList<ProductProjection> DeserializeCollectionProjection(Emitter emitter, CollectionProjection collectionProjection, Type type, ImmutableList<Product> products)
        {
            throw new NotImplementedException();
        }

        private static IEnumerable<ProductProjection> DeserializeChildParameters(Emitter emitter, Projection projection, Type parameterType, string parameterName, Product product)
        {
            if (emitter.WatchContext != null)
            {
                var elementType = parameterType.GetGenericArguments()[0];
                var productProjections = Projector.GetFactReferences(projection, product, parameterName)
                    .Select(reference => emitter.DeserializeToType(reference, elementType))
                    .Select(obj => new ProductProjection(product, obj));
                return productProjections;
            }
            else
            {
                return Enumerable.Empty<ProductProjection>();
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
                    var elements = Projector.GetFactReferences(projection, product, parameterName)
                        .Select(reference => emitter.DeserializeToType(reference, elementType))
                        .ToImmutableList();
                    return ImmutableObservableCollection.Create(elementType, elements);
                }
                else
                {
                    return WatchedObservableCollection.Create(elementType, product.GetAnchor(), parameterName, emitter.WatchContext);
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}