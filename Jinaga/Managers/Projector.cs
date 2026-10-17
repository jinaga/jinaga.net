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
            else if (projection is SignerProjection signer)
            {
                return new[] { product.GetFactReference(signer.Tag) };
            }
            else if (projection is DateLearnedProjection dateLearned)
            {
                return new[] { product.GetFactReference(dateLearned.Tag) };
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
            else if (projection is SignerProjection signer)
            {
                return products
                    .Select(product => product.GetFactReference(signer.Tag))
                    .ToImmutableList();
            }
            else if (projection is DateLearnedProjection dateLearned)
            {
                return products
                    .Select(product => product.GetFactReference(dateLearned.Tag))
                    .ToImmutableList();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Whether the projection reads the date on which this replica learned a fact, so
        /// that the store has to be asked for it. Every other specification pays nothing.
        /// </summary>
        public static bool ProjectsDateLearned(Projection projection)
        {
            switch (projection)
            {
                case DateLearnedProjection _:
                    return true;
                case CompoundProjection compound:
                    return compound.Names.Any(name => ProjectsDateLearned(compound.GetProjection(name)));
                case CollectionProjection collection:
                    return ProjectsDateLearned(collection.Projection);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the projection reads the users who asserted a fact, so that the store has
        /// to be asked who saved the facts on this replica. Every other specification pays
        /// nothing for it.
        /// </summary>
        public static bool ProjectsSigners(Projection projection)
        {
            switch (projection)
            {
                case SignerProjection _:
                    return true;
                case CompoundProjection compound:
                    return compound.Names.Any(name => ProjectsSigners(compound.GetProjection(name)));
                case CollectionProjection collection:
                    return ProjectsSigners(collection.Projection);
                default:
                    return false;
            }
        }
    }
}