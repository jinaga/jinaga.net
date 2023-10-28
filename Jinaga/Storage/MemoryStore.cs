using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Storage
{
    public class MemoryStore : IStore
    {
        private volatile ImmutableDictionary<FactReference, Fact> factsByReference = ImmutableDictionary<FactReference, Fact>.Empty;
        private ImmutableList<Edge> edges = ImmutableList<Edge>.Empty;
        private volatile ImmutableDictionary<FactReference, ImmutableList<FactReference>> ancestors = ImmutableDictionary<FactReference, ImmutableList<FactReference>>.Empty;
        private volatile ImmutableDictionary<string, string> bookmarks = ImmutableDictionary<string, string>.Empty;
        private volatile ImmutableDictionary<string, DateTime> mruDates = ImmutableDictionary<string, DateTime>.Empty;
        private volatile ImmutableList<FactReference> feed = ImmutableList<FactReference>.Empty;
        private volatile int bookmark = 0;

        public Task<ImmutableList<Fact>> Save(FactGraph graph, CancellationToken cancellationToken)
        {
            lock (this)
            {
                var newFacts = graph.FactReferences
                    .Where(reference => !factsByReference.ContainsKey(reference))
                    .Select(reference => graph.GetFact(reference))
                    .ToImmutableList();
                factsByReference = factsByReference.AddRange(newFacts
                    .Select(fact => new KeyValuePair<FactReference, Fact>(fact.Reference, fact))
                );
                feed = feed.AddRange(newFacts
                    .Select(fact => fact.Reference)
                );
                var newPredecessors = newFacts
                    .Select(fact => (
                        factReference: fact.Reference,
                        edges: fact.Predecessors
                            .SelectMany(predecessor => CreateEdges(fact, predecessor))
                            .ToImmutableList()
                    ))
                    .ToImmutableList();
                edges = edges.AddRange(newPredecessors
                    .SelectMany(pair => pair.edges)
                );
                foreach (var (factReference, edges) in newPredecessors)
                {
                    ancestors = ancestors.Add(
                        factReference,
                        edges
                            .SelectMany(edge => ancestors[edge.Predecessor])
                            .Append(factReference)
                            .Distinct()
                            .ToImmutableList()
                    );
                }
                return Task.FromResult(newFacts);
            }
        }

        public Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            lock (this)
            {
                var products = ExecuteMatchesAndProjection(givenTuple, specification.Matches, specification.Projection);
                return Task.FromResult(products);
            }
        }

        private ImmutableList<Product> ExecuteMatchesAndProjection(FactReferenceTuple start, ImmutableList<Match> matches, Projection projection)
        {
            ImmutableList<FactReferenceTuple> tuples = ExecuteMatches(start, matches);
            var products = tuples.Select(tuple => CreateProduct(tuple, projection)).ToImmutableList();
            return products;
        }

        private ImmutableList<FactReferenceTuple> ExecuteMatches(FactReferenceTuple start, ImmutableList<Match> matches)
        {
            return matches.Aggregate(
                ImmutableList.Create(start),
                (set, match) => set
                    .SelectMany(references => ExecuteMatch(references, match))
                    .ToImmutableList());
        }

        private ImmutableList<FactReferenceTuple> ExecuteMatch(FactReferenceTuple references, Match match)
        {
            ImmutableList<FactReferenceTuple> resultReferences;
            var firstCondition = match.Conditions.First();
            if (firstCondition is PathCondition pathCondition)
            {
                var result = ExecutePathCondition(references, match.Unknown, pathCondition);
                resultReferences = result.Select(reference =>
                    references.Add(match.Unknown.Name, reference)).ToImmutableList();
            }
            else
            {
                throw new ArgumentException("The first condition must be a path condition.");
            }

            foreach (var condition in match.Conditions.Skip(1))
            {
                resultReferences = FilterByCondition(references, resultReferences, condition);
            }
            return resultReferences;
        }

        private ImmutableList<FactReference> ExecutePathCondition(FactReferenceTuple start, Label unknown, PathCondition pathCondition)
        {
            var startingFactReference = start.Get(pathCondition.LabelRight);
            var set = new FactReference[] { startingFactReference }.ToImmutableList();
            foreach (var role in pathCondition.RolesRight)
            {
                set = ExecutePredecessorStep(set, role.Name, role.TargetType);
            }
            foreach (var role in EnumerateRoles(pathCondition.RolesLeft, unknown.Type).Reverse())
            {
                set = ExecuteSuccessorStep(set, role.name, role.declaringType);
            }
            return set;
        }

        private ImmutableList<FactReferenceTuple> FilterByCondition(FactReferenceTuple references, ImmutableList<FactReferenceTuple> resultReferences, MatchCondition condition)
        {
            if (condition is PathCondition pathCondition)
            {
                throw new NotImplementedException();
            }
            else if (condition is ExistentialCondition existentialCondition)
            {
                var matchingResultReferences = resultReferences
                    .Where(resultReference =>
                        ExecuteMatches(resultReference, existentialCondition.Matches).Any() ^
                        !existentialCondition.Exists)
                    .ToImmutableList();
                return matchingResultReferences;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private IEnumerable<(string name, string declaringType)> EnumerateRoles(IEnumerable<Role> roles, string startingType)
        {
            var type = startingType;
            foreach (var role in roles)
            {
                yield return (role.Name, type);
                type = role.TargetType;
            }
        }

        private Product CreateProduct(FactReferenceTuple tuple, Projection projection)
        {
            var product = tuple.Names.Aggregate(
                Product.Empty,
                (product, name) => product.With(name, new SimpleElement(tuple.Get(name)))
            );
            product = ExecuteProjection(tuple, product, projection);
            return product;
        }

        private Product ExecuteProjection(FactReferenceTuple tuple, Product product, Projection projection)
        {
            if (projection is CompoundProjection compoundProjection)
            {
                foreach (var name in compoundProjection.Names)
                {
                    var childProjection = compoundProjection.GetProjection(name);
                    if (childProjection is SimpleProjection simpleProjection)
                    {
                        var element = new SimpleElement(tuple.Get(simpleProjection.Tag));
                        product = product.With(simpleProjection.Tag, element);
                    }
                    else if (childProjection is CollectionProjection collectionProjection)
                    {
                        var products = ExecuteMatchesAndProjection(tuple, collectionProjection.Matches, collectionProjection.Projection);
                        var element = new CollectionElement(products);
                        product = product.With(name, element);
                    }
                    else if (childProjection is FieldProjection fieldProjection)
                    {
                        var element = new SimpleElement(tuple.Get(fieldProjection.Tag));
                        product = product.With(fieldProjection.Tag, element);
                    }
                    else
                    {
                        throw new Exception($"Unsupported projection type {childProjection.GetType().Name}.");
                    }
                }
            }
            return product;
        }

        public Task<FactGraph> Load(ImmutableList<FactReference> references, CancellationToken cancellationToken)
        {
            var facts = references
                .SelectMany(reference => ancestors[reference])
                .Distinct()
                .Select(reference => factsByReference[reference]);
            var builder = new FactGraphBuilder();
            foreach (var fact in facts)
            {
                builder.Add(fact);
            }
            var graph = builder.Build();
            return Task.FromResult(graph);
        }

        private IEnumerable<Edge> CreateEdges(Fact successor, Predecessor predecessor)
        {
            switch (predecessor)
            {
                case PredecessorSingle single:
                    return new Edge[]
                    {
                        new Edge(single.Reference, single.Role, successor.Reference)
                    };
                case PredecessorMultiple multiple:
                    return multiple.References.Select(reference => new Edge(
                        reference, multiple.Role, successor.Reference
                    ));
                default:
                    throw new NotImplementedException();
            }
        }

        private ImmutableList<FactReference> ExecutePredecessorStep(ImmutableList<FactReference> set, string role, string targetType)
        {
            return set
                .SelectMany(factReference => edges
                    .Where(edge =>
                        edge.Successor == factReference &&
                        edge.Role == role &&
                        edge.Predecessor.Type == targetType
                    )
                    .Select(edge => edge.Predecessor)
                )
                .ToImmutableList();
        }

        private ImmutableList<FactReference> ExecuteSuccessorStep(ImmutableList<FactReference> set, string role, string targetType)
        {
            return set
                .SelectMany(factReference => edges
                    .Where(edge =>
                        edge.Predecessor == factReference &&
                        edge.Role == role &&
                        edge.Successor.Type == targetType
                    )
                    .Select(edge => edge.Successor)
                )
                .ToImmutableList();
        }

        public Task<string> LoadBookmark(string feed)
        {
            if (this.bookmarks.TryGetValue(feed, out var bookmark))
            {
                return Task.FromResult(bookmark);
            }
            else
            {
                return Task.FromResult("");
            }
        }

        public Task<ImmutableList<FactReference>> ListKnown(ImmutableList<FactReference> factReferences)
        {
            var known = factReferences
                .Where(r => factsByReference.ContainsKey(r))
                .ToImmutableList();
            return Task.FromResult(known);
        }

        public Task SaveBookmark(string feed, string bookmark)
        {
            lock (this)
            {
                bookmarks = bookmarks.SetItem(feed, bookmark);
                return Task.CompletedTask;
            }
        }

        public Task<DateTime?> GetMruDate(string specificationHash)
        {
            if (mruDates.TryGetValue(specificationHash, out var mruDate))
            {
                return Task.FromResult<DateTime?>(mruDate);
            }
            else
            {
                return Task.FromResult<DateTime?>(null);
            }
        }

        public Task SetMruDate(string specificationHash, DateTime mruDate)
        {
            lock (this)
            {
                mruDates = mruDates.SetItem(specificationHash, mruDate);
                return Task.CompletedTask;
            }
        }

        public Task<QueuedFacts> GetQueue()
        {
            lock (this)
            {
                var facts = feed.Skip(bookmark).Select(reference =>
                    factsByReference[reference]
                ).ToImmutableList();

                return Task.FromResult(new QueuedFacts(
                    facts, feed.Count.ToString()
                ));
            }
        }

        public Task SetQueueBookmark(string bookmark)
        {
            lock (this)
            {
                int nextBookmark = int.Parse(bookmark);
                if (nextBookmark > this.bookmark)
                    this.bookmark = nextBookmark;
                return Task.CompletedTask;
            }
        }
    }
}