using Jinaga.Facts;
using Jinaga.Products;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Store.SQLite
{
    internal class SpecificationLabel
    {
        public SpecificationLabel(string name, int index, string type)
        {
            Name = name;
            Index = index;
            Type = type;
        }

        public string Name { get; }
        public int Index { get; }
        public string Type { get; }
    }

    internal class SpecificationSqlQuery
    {
        public static SpecificationSqlQuery Empty = new SpecificationSqlQuery(string.Empty, ImmutableList<object>.Empty, ImmutableList<SpecificationLabel>.Empty);

        public SpecificationSqlQuery(string sql, ImmutableList<object> parameters, ImmutableList<SpecificationLabel> labels)
        {
            Sql = sql;
            Parameters = parameters;
            Labels = labels;
        }

        public string Sql { get; }
        public ImmutableList<object> Parameters { get; }
        public ImmutableList<SpecificationLabel> Labels { get; }
    }

    internal class SqlQueryTree
    {
        private class IdentifiedResult
        {
            public ImmutableList<int> FactIds { get; set; }
            public Product Product { get; set; }
        }

        private class ChildResult
        {
            public ImmutableList<int> ParentFactIds { get; set; }
            public ImmutableList<Product> Products { get; set; }
        }

        public SpecificationSqlQuery SqlQuery { get; }
        public ImmutableDictionary<string, SqlQueryTree> ChildQueries { get; }

        public readonly int parentFactIdLength;

        public SqlQueryTree(SpecificationSqlQuery sqlQuery, int parentFactIdLength, ImmutableDictionary<string, SqlQueryTree> childQueries)
        {
            SqlQuery = sqlQuery;
            this.parentFactIdLength = parentFactIdLength;
            ChildQueries = childQueries;
        }

        public ImmutableList<Product> ResultsToProducts(ResultSetTree resultSetTree, Product givenProduct)
        {
            var childResults = MergeChildResults(resultSetTree, givenProduct);
            if (childResults.Count == 0)
            {
                return ImmutableList<Product>.Empty;
            }
            else
            {
                return childResults.Single().Products;
            }
        }

        private ImmutableList<ChildResult> MergeChildResults(ResultSetTree resultSetTree, Product givenProduct)
        {
            var rows = resultSetTree.ResultSet;
            if (rows.Count == 0)
            {
                return ImmutableList<ChildResult>.Empty;
            }

            var identifiedResults = rows.Select(row => new IdentifiedResult
            {
                FactIds = IdentifierOf(row),
                Product = ProductOf(row, givenProduct)
            }).ToImmutableList();

            foreach (var childQuery in ChildQueries)
            {
                var childResultSetTree = resultSetTree.ChildResultSets[childQuery.Key];
                var childResults = childQuery.Value.MergeChildResults(childResultSetTree, givenProduct);

                // Merge the child results into the parent products
                int childIndex = 0;
                foreach (var identifiedResult in identifiedResults)
                {
                    if (childIndex < childResults.Count &&
                        identifiedResult.FactIds.SequenceEqual(childResults[childIndex].ParentFactIds))
                    {
                        identifiedResult.Product = identifiedResult.Product.With(
                            childQuery.Key,
                            new CollectionElement(childResults[childIndex].Products));
                        childIndex++;
                    }
                    else
                    {
                        identifiedResult.Product = identifiedResult.Product.With(
                            childQuery.Key,
                            new CollectionElement(ImmutableList<Product>.Empty));
                    }
                }
            }

            // Group the results by their parent identifiers
            var groupedResults = ImmutableList<ChildResult>.Empty;
            var parentFactIds = identifiedResults[0].FactIds.Take(parentFactIdLength).ToImmutableList();
            var products = ImmutableList.Create(identifiedResults[0].Product);
            foreach (var identifiedResult in identifiedResults.Skip(1))
            {
                var nextParentFactIds = identifiedResult.FactIds.Take(parentFactIdLength).ToImmutableList();
                if (nextParentFactIds.SequenceEqual(parentFactIds))
                {
                    products = products.Add(identifiedResult.Product);
                }
                else
                {
                    groupedResults = groupedResults.Add(new ChildResult
                    {
                        ParentFactIds = parentFactIds,
                        Products = products
                    });
                    parentFactIds = nextParentFactIds;
                    products = ImmutableList.Create(identifiedResult.Product);
                }
            }
            groupedResults = groupedResults.Add(new ChildResult
            {
                ParentFactIds = parentFactIds,
                Products = products
            });
            return groupedResults;
        }

        private ImmutableList<int> IdentifierOf(ImmutableDictionary<int, ResultSetFact> row)
        {
            return SqlQuery.Labels
                .OrderBy(label => label.Index)
                .Select(label => row[label.Index].FactId)
                .ToImmutableList();
        }

        private Product ProductOf(ImmutableDictionary<int, ResultSetFact> row, Product givenProduct)
        {
            return SqlQuery.Labels.Aggregate(givenProduct, (tuple, label) =>
            {
                var fact = row[label.Index];
                var value = new FactReference(label.Type, fact.Hash);
                return tuple.With(label.Name, new SimpleElement(value));
            });
        }
    }
}