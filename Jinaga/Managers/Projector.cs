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
        public static IEnumerable<FactReference> GetFactReferences(Projection projection, Product product)
        {
            if (projection is SimpleProjection simple)
            {
                return product.GetElement(simple.Tag).GetFactReferences();
            }
            else if (projection is CompoundProjection compound)
            {
                return compound.Names
                    .SelectMany(name => GetFactReferences(
                        compound.GetProjection(name),
                        product));
            }
            else if (projection is CollectionProjection collection)
            {
                return new FactReference[0];
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static ImmutableList<FactReference> GetFactReferences(Projection projection, ImmutableList<Product> products, Type type)
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
                var references = (
                    from product in products
                    from parameter in parameters
                    from reference in GetFactReferences(
                        compound.GetProjection(parameter.Name),
                        product)
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