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
        private volatile ImmutableDictionary<FactReference, ImmutableList<FactSignature>> signaturesByReference = ImmutableDictionary<FactReference, ImmutableList<FactSignature>>.Empty;
        private ImmutableList<Edge> edges = ImmutableList<Edge>.Empty;
        private volatile ImmutableDictionary<FactReference, ImmutableList<FactReference>> ancestors = ImmutableDictionary<FactReference, ImmutableList<FactReference>>.Empty;
        private volatile ImmutableDictionary<string, string> bookmarks = ImmutableDictionary<string, string>.Empty;
        private volatile ImmutableDictionary<string, DateTime> mruDates = ImmutableDictionary<string, DateTime>.Empty;
        private volatile ImmutableList<FactReference> feed = ImmutableList<FactReference>.Empty;
        private volatile int bookmark = 0;

        public bool IsPersistent => false;

        public Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, CancellationToken cancellationToken)
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
                if (queue)
                {
                    feed = feed.AddRange(newFacts
                        .Select(fact => fact.Reference)
                    );
                }
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
                foreach (var reference in graph.FactReferences)
                {
                    var graphSignatures = graph.GetSignatures(reference);
                    if (graphSignatures.Count == 0)
                        continue;

                    if (signaturesByReference.TryGetValue(reference, out var existingSignatures))
                    {
                        var merged = existingSignatures
                            .Concat(graphSignatures)
                            .Distinct()
                            .ToImmutableList();
                        signaturesByReference = signaturesByReference.SetItem(
                            reference,
                            merged
                        );
                    }
                    else
                    {
                        signaturesByReference = signaturesByReference.Add(
                            reference,
                            graphSignatures
                        );
                    }
                }
                return Task.FromResult(newFacts);
            }
        }

        public Task<ImmutableList<Product>> Read(FactReferenceTuple givenTuple, Specification specification, CancellationToken cancellationToken)
        {
            lock (this)
            {
                var products = ExecuteSpecification(givenTuple, specification);
                return Task.FromResult(products);
            }
        }

        private ImmutableList<Product> ExecuteSpecification(FactReferenceTuple givenTuple, Specification specification)
        {
            // If any given does not match its existential conditions,
            // then return nothing.
            var factReferences = ImmutableList.Create(givenTuple);
            foreach (var given in specification.Givens)
            {
                foreach (var existentialCondition in given.ExistentialConditions)
                {
                    var result = FilterByExistentialCondition(factReferences, existentialCondition);
                    if (result.IsEmpty)
                    {
                        return ImmutableList<Product>.Empty;
                    }
                }
            }
            return ExecuteMatchesAndProjection(givenTuple, specification.Matches, specification.Projection);
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
            var pathCondition = match.PathConditions.First();
            var result = ExecutePathCondition(references, match.Unknown, pathCondition);
            resultReferences = result.Select(reference =>
                references.Add(match.Unknown.Name, reference)).ToImmutableList();

            foreach (var additionalPathCondition in match.PathConditions.Skip(1))
            {
                resultReferences = FilterByPathCondition(resultReferences, references, match.Unknown, additionalPathCondition);
            }
            foreach (var condition in match.ExistentialConditions)
            {
                resultReferences = FilterByExistentialCondition(resultReferences, condition);
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

        private ImmutableList<FactReferenceTuple> FilterByPathCondition(ImmutableList<FactReferenceTuple> resultReferences, FactReferenceTuple references, Label unknown, PathCondition pathCondition)
        {
            var otherResultReferences = ExecutePathCondition(references, unknown, pathCondition);
            return resultReferences
                .Where(resultReference =>
                    otherResultReferences.Contains(resultReference.Get(unknown.Name)))
                .ToImmutableList();
        }

        private ImmutableList<FactReferenceTuple> FilterByExistentialCondition(ImmutableList<FactReferenceTuple> resultReferences, ExistentialCondition existentialCondition)
        {
            return resultReferences
                .Where(resultReference =>
                    ExecuteMatches(resultReference, existentialCondition.Matches).Any() ^
                    !existentialCondition.Exists)
                .ToImmutableList();
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
                    else if (childProjection is HashProjection hashProjection)
                    {
                        var element = new SimpleElement(tuple.Get(hashProjection.Tag));
                        product = product.With(hashProjection.Tag, element);
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
                if (signaturesByReference.TryGetValue(fact.Reference, out var signatures))
                {
                    builder.Add(new FactEnvelope(fact, signatures));
                }
                else
                {
                    builder.Add(new FactEnvelope(fact, ImmutableList<FactSignature>.Empty));
                }
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
                var facts = feed.Skip(bookmark)
                    .SelectMany(reference => ancestors[reference])
                    .Distinct()
                    .Select(reference => factsByReference[reference]);

                // Write the facts and their ancestors to a fact graph.
                var builder = new FactGraphBuilder();
                foreach (var fact in facts)
                {
                    if (signaturesByReference.TryGetValue(fact.Reference, out var signatures))
                    {
                        builder.Add(new FactEnvelope(fact, signatures));
                    }
                    else
                    {
                        builder.Add(new FactEnvelope(fact, ImmutableList<FactSignature>.Empty));
                    }
                }
                var graph = builder.Build();

                return Task.FromResult(new QueuedFacts(
                    graph, feed.Count.ToString()
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

        public Task<IEnumerable<Fact>> GetAllFacts()
        {
            // Ensure that facts are returned in topological order.
            // Predecessors must be returned before their successors.
            var orderedFacts = new List<Fact>();
            var visited = new HashSet<FactReference>();
            void Visit(FactReference reference)
            {
                if (visited.Contains(reference))
                    return;

                visited.Add(reference);

                if (ancestors.TryGetValue(reference, out var predecessors))
                {
                    foreach (var predecessor in predecessors)
                    {
                        Visit(predecessor);
                    }
                }

                orderedFacts.Add(factsByReference[reference]);
            }

            foreach (var reference in factsByReference.Keys)
            {
                Visit(reference);
            }

            return Task.FromResult(orderedFacts.AsEnumerable());
        }

        public Task Purge(ImmutableList<Specification> purgeConditions)
        {
            // Not implemented.
            return Task.CompletedTask;
        }

        public Task PurgeDescendants(FactReference purgeRoot, ImmutableList<FactReference> triggers)
        {
            lock (this)
            {
                var triggersAndTheirAncestors = new HashSet<FactReference>(triggers);
                foreach (var trigger in triggers)
                {
                    if (factsByReference.TryGetValue(trigger, out var triggerFact))
                    {
                        AddAllAncestors(triggerFact, triggersAndTheirAncestors);
                    }
                }

                factsByReference = factsByReference
                    .Where(pair => 
                        !ancestors[pair.Key].Contains(purgeRoot) || 
                        triggersAndTheirAncestors.Contains(pair.Key))
                    .ToImmutableDictionary(pair => pair.Key, pair => pair.Value);

                signaturesByReference = signaturesByReference
                    .Where(pair => factsByReference.ContainsKey(pair.Key))
                    .ToImmutableDictionary(pair => pair.Key, pair => pair.Value);

                edges = edges
                    .Where(edge => factsByReference.ContainsKey(edge.Successor))
                    .ToImmutableList();

                ancestors = ancestors
                    .Where(pair => factsByReference.ContainsKey(pair.Key))
                    .ToImmutableDictionary(pair => pair.Key, pair => pair.Value);

                feed = feed
                    .Where(reference => factsByReference.ContainsKey(reference))
                    .ToImmutableList();

                return Task.CompletedTask;
            }
        }

        private void AddAllAncestors(Fact fact, HashSet<FactReference> ancestors)
        {
            foreach (var predecessor in fact.Predecessors)
            {
                var references = predecessor switch
                {
                    PredecessorSingle single => ImmutableList.Create(single.Reference),
                    PredecessorMultiple multiple => multiple.References,
                    _ => throw new NotImplementedException()
                };

                foreach (var reference in references)
                {
                    if (!ancestors.Contains(reference))
                    {
                        ancestors.Add(reference);
                        if (factsByReference.TryGetValue(reference, out var predecessorFact))
                        {
                            AddAllAncestors(predecessorFact, ancestors);
                        }
                    }
                }
            }
        }
    }
}