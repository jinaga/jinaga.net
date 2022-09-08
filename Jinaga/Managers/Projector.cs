using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Projections;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Managers
{
    class Projector
    {
        public static IEnumerable<FactReference> GetFactReferences(ProjectionOld projection, Product product, string name)
        {
            if (projection is SimpleProjection simple)
            {
                return new[] { product.GetFactReference(simple.Tag) };
            }
            else if (projection is CompoundProjection compound)
            {
                return compound.Names
                    .SelectMany(name => GetFactReferences(
                        compound.GetProjection(name),
                        product,
                        name));
            }
            else if (projection is CollectionProjection collection)
            {
                var element = product.GetElement(name);
                if (element is CollectionElement collectionElement)
                {
                    return GetFactReferences(collection.Specification.Projection, collectionElement.Products);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static ImmutableList<FactReference> GetFactReferences(ProjectionOld projection, ImmutableList<Product> products)
        {
            if (projection is SimpleProjection simple)
            {
                return products
                    .Select(product => product.GetFactReference(simple.Tag))
                    .ToImmutableList();
            }
            else if (projection is CompoundProjection compound)
            {
                var references = (
                    from product in products
                    from name in compound.Names
                    from reference in GetFactReferences(
                        compound.GetProjection(name),
                        product,
                        name)
                    select reference
                ).Distinct().ToImmutableList();
                return references;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static ImmutableList<FactReference> GetFactReferences(ProjectionOld projection, ImmutableList<Product> products, Type type)
        {
            if (projection is SimpleProjection simple)
            {
                return products
                    .Select(product => product.GetFactReference(simple.Tag))
                    .ToImmutableList();
            }
            else if (projection is CompoundProjection compound)
            {
                var constructorInfos = type.GetConstructors();
                if (constructorInfos.Length != 1)
                {
                    throw new NotImplementedException($"Multiple constructors for {type.Name}");
                }
                var constructor = constructorInfos.Single();
                var parameters = constructor.GetParameters();
                var properties = type.GetProperties();
                var names = parameters.Select(parameter => parameter.Name).Concat(
                    properties.Select(property => property.Name)
                ).Distinct();
                var references = (
                    from product in products
                    from name in names
                    from reference in GetFactReferences(
                        compound.GetProjection(name),
                        product,
                        name)
                    select reference
                ).Distinct().ToImmutableList();
                return references;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}