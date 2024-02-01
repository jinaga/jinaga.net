﻿using Jinaga.Facts;
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
        public static IEnumerable<FactReference> GetFactReferences(Projection projection, Product product, string name)
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
                    return GetFactReferences(collection.Projection, collectionElement.Products);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (projection is FieldProjection field)
            {
                return new[] { product.GetFactReference(field.Tag) };
            }
            else if (projection is HashProjection hash)
            {
                return new[] { product.GetFactReference(hash.Tag) };
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static ImmutableList<FactReference> GetFactReferences(Projection projection, ImmutableList<Product> products)
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
            else if (projection is FieldProjection field)
            {
                return products
                    .Select(product => product.GetFactReference(field.Tag))
                    .ToImmutableList();
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
                return (
                    from product in products
                    from name in compound.Names
                    from reference in GetFactReferences(
                        compound.GetProjection(name),
                        product,
                        name)
                    select reference
                ).Distinct().ToImmutableList();
            }
            else if (projection is FieldProjection field)
            {
                return products
                    .Select(product => product.GetFactReference(field.Tag))
                    .ToImmutableList();
            }
            else if (projection is HashProjection hash)
            {
                return products
                    .Select(product => product.GetFactReference(hash.Tag))
                    .ToImmutableList();
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}