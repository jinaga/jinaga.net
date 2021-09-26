using Jinaga.Observers;
using Jinaga.Parsers;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Serialization;
using System;
using System.Collections.Immutable;
using System.Linq;

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

        private static ImmutableList<ProductProjection> DeserializeCollectionProjection(Emitter emitter, CollectionProjection collectionProjection, Type type, ImmutableList<Product> products)
        {
            throw new NotImplementedException();
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
                var elements = Projector.GetFactReferences(projection, product, parameterName)
                    .Select(reference => emitter.DeserializeToType(reference, elementType))
                    .ToImmutableList();
                return ImmutableObservableCollection.Create(elementType, elements);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}