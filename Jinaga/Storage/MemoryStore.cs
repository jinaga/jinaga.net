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
        private volatile ImmutableDictionary<FactReference, DateTime> datesLearnedByReference = ImmutableDictionary<FactReference, DateTime>.Empty;
        private ImmutableList<Edge> edges = ImmutableList<Edge>.Empty;
        private volatile ImmutableDictionary<FactReference, ImmutableList<FactReference>> ancestors = ImmutableDictionary<FactReference, ImmutableList<FactReference>>.Empty;
        private volatile ImmutableDictionary<string, string> bookmarks = ImmutableDictionary<string, string>.Empty;
        private volatile ImmutableDictionary<string, DateTime> mruDates = ImmutableDictionary<string, DateTime>.Empty;
        private volatile ImmutableList<QueueEntry> queueEntries = ImmutableList<QueueEntry>.Empty;
        private volatile int queuedCount = 0;
        private volatile ImmutableDictionary<FactReference, ImmutableHashSet<string>> saversByReference = ImmutableDictionary<FactReference, ImmutableHashSet<string>>.Empty;

        /// <summary>
        /// One fact waiting in the outbound queue, with the position at which it
        /// was queued. A bookmark acknowledges a position, so the position has to
        /// outlive the entries queued before it: a fact held back leaves a gap in
        /// the queue when the facts after it are acknowledged, and the positions of
        /// those facts do not shift.
        /// </summary>
        private class QueueEntry
        {
            public FactReference Reference { get; }
            public int Position { get; }

            public QueueEntry(FactReference reference, int position)
            {
                Reference = reference;
                Position = position;
            }
        }

        public bool IsPersistent => false;

        public Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, CancellationToken cancellationToken)
        {
            return Save(graph, queue, null, cancellationToken);
        }

        public Task<ImmutableList<Fact>> Save(FactGraph graph, bool queue, string? savedBy, CancellationToken cancellationToken)
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
                // Only facts that were not already here are new, so saving a fact again --
                // whether on its own or carrying an added signature -- leaves its date alone.
                var dateLearned = DateTime.UtcNow;
                datesLearnedByReference = datesLearnedByReference.AddRange(newFacts
                    .Select(fact => new KeyValuePair<FactReference, DateTime>(fact.Reference, dateLearned))
                );
                if (queue)
                {
                    foreach (var fact in newFacts)
                    {
                        queueEntries = queueEntries.Add(new QueueEntry(fact.Reference, queuedCount));
                        queuedCount++;
                    }
                    if (savedBy != null && !graph.FactReferences.IsEmpty)
                    {
                        // The user saved the fact they asked for, whether or not
                        // the store already had it, and every fact this save added.
                        var saved = newFacts
                            .Select(fact => fact.Reference)
                            .Append(graph.Last)
                            .Distinct();
                        foreach (var reference in saved)
                        {
                            var savers = saversByReference.TryGetValue(reference, out var existing)
                                ? existing
                                : ImmutableHashSet<string>.Empty;
                            saversByReference = saversByReference.SetItem(reference, savers.Add(savedBy));
                        }
                    }
                }
                var newPredecessors = newFacts
                    .Select(fact => (
                        factReference: fact.Reference,
                        edges: fact.Predecessors
                            .SelectMany(predecessor => CreateEdges(fact.Reference, predecessor))
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
                    ExecuteMatches(WithoutRedeclaredLabels(resultReference, existentialCondition), existentialCondition.Matches).Any() ^
                    !existentialCondition.Exists)
                .ToImmutableList();
        }

        /// <summary>
        /// The tuple with the outer binding of every label the condition redeclares removed,
        /// so that each inner declaration is a new variable within the condition's scope.
        ///
        /// A user-written specification never has this shape, because the parser and
        /// <see cref="Jinaga.Projections.WellFormedSpecification"/> refuse a label already in
        /// scope. The inverses this library computes for itself do have it, so the evaluators
        /// have to read it. Only <c>Any()</c> of the inner tuples decides the condition, so the
        /// outer tuple the caller keeps is unaffected.
        ///
        /// The whole condition is one scope, which is what
        /// <c>ResultDescriptionBuilder.AddEdges</c> gives it in the SQLite store.
        /// </summary>
        private static FactReferenceTuple WithoutRedeclaredLabels(FactReferenceTuple references, ExistentialCondition existentialCondition)
        {
            return existentialCondition.Matches.Aggregate(
                references,
                (tuple, match) => tuple.Remove(match.Unknown.Name));
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
            // CreateProduct has already put every label of the tuple into the product, and
            // that is all a projection other than a collection reads back. Only a
            // collection adds anything.
            if (projection is CompoundProjection compoundProjection)
            {
                foreach (var name in compoundProjection.Names)
                {
                    if (compoundProjection.GetProjection(name) is CollectionProjection collectionProjection)
                    {
                        var products = ExecuteMatchesAndProjection(tuple, collectionProjection.Matches, collectionProjection.Projection);
                        product = product.With(name, new CollectionElement(products));
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

        public Task<ImmutableDictionary<FactReference, DateTime>> LoadDatesLearned(ImmutableList<FactReference> references, CancellationToken cancellationToken)
        {
            lock (this)
            {
                var datesLearned = references
                    .Distinct()
                    .Where(reference => datesLearnedByReference.ContainsKey(reference))
                    .ToImmutableDictionary(
                        reference => reference,
                        reference => datesLearnedByReference[reference]
                    );
                return Task.FromResult(datesLearned);
            }
        }

        /// <summary>
        /// Records the edges of a graph whose facts are identified by the references given,
        /// rather than by their content. Only the conformance vectors of jinaga-spec use this.
        ///
        /// An inverse vector's graph may name a predecessor that content addressing cannot
        /// reach: a fact saved later, or a cycle. No hash can express either, so such a graph
        /// cannot be built as a <see cref="FactGraph"/> and <see cref="Save"/> cannot take it.
        /// A read walks the edges, so recording them is enough to read a specification over
        /// the graph. Nothing else a store does is available for these facts.
        /// </summary>
        internal void PlaceGraphByReference(IEnumerable<(FactReference Reference, ImmutableList<Predecessor> Predecessors)> facts)
        {
            lock (this)
            {
                edges = edges.AddRange(facts
                    .SelectMany(fact => fact.Predecessors
                        .SelectMany(predecessor => CreateEdges(fact.Reference, predecessor))));
            }
        }

        private IEnumerable<Edge> CreateEdges(FactReference successor, Predecessor predecessor)
        {
            switch (predecessor)
            {
                case PredecessorSingle single:
                    return new Edge[]
                    {
                        new Edge(single.Reference, single.Role, successor)
                    };
                case PredecessorMultiple multiple:
                    return multiple.References.Select(reference => new Edge(
                        reference, multiple.Role, successor
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
                var queuedReferences = queueEntries
                    .Select(entry => entry.Reference)
                    .ToImmutableList();
                var facts = queuedReferences
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

                // The bookmark counts positions in the queue, so the bookmark
                // that acknowledges the fact at a given position is one past it.
                var queuedFacts = queueEntries
                    .Select(entry => new QueuedFact(
                        entry.Reference,
                        (entry.Position + 1).ToString()
                    ))
                    .ToImmutableList();

                return Task.FromResult(new QueuedFacts(graph, queuedFacts));
            }
        }

        public Task<ImmutableDictionary<FactReference, ImmutableHashSet<string>>> LoadSavers(ImmutableList<FactReference> references, CancellationToken cancellationToken)
        {
            lock (this)
            {
                var savers = references
                    .Distinct()
                    .Where(reference => saversByReference.ContainsKey(reference))
                    .ToImmutableDictionary(
                        reference => reference,
                        reference => saversByReference[reference]
                    );
                return Task.FromResult(savers);
            }
        }

        public Task SetQueueBookmark(string bookmark)
        {
            lock (this)
            {
                int acknowledgedPosition = int.Parse(bookmark);
                queueEntries = queueEntries
                    .Where(entry => entry.Position >= acknowledgedPosition)
                    .ToImmutableList();
                return Task.CompletedTask;
            }
        }

        public Task AcknowledgeQueue(ImmutableList<QueuedFact> facts)
        {
            lock (this)
            {
                var acknowledged = facts
                    .Select(fact => fact.Reference)
                    .ToImmutableHashSet();
                queueEntries = queueEntries
                    .Where(entry => !acknowledged.Contains(entry.Reference))
                    .ToImmutableList();
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

                // A purged fact that arrives again is learned again, so it gets a new date.
                datesLearnedByReference = datesLearnedByReference
                    .Where(pair => factsByReference.ContainsKey(pair.Key))
                    .ToImmutableDictionary(pair => pair.Key, pair => pair.Value);

                edges = edges
                    .Where(edge => factsByReference.ContainsKey(edge.Successor))
                    .ToImmutableList();

                ancestors = ancestors
                    .Where(pair => factsByReference.ContainsKey(pair.Key))
                    .ToImmutableDictionary(pair => pair.Key, pair => pair.Value);

                queueEntries = queueEntries
                    .Where(entry => factsByReference.ContainsKey(entry.Reference))
                    .ToImmutableList();

                // Who saved a fact is recorded against the fact, so it goes when
                // the fact goes. Left behind, it would attribute a fact that is
                // learned again to whoever saved the copy that was purged.
                saversByReference = saversByReference
                    .Where(pair => factsByReference.ContainsKey(pair.Key))
                    .ToImmutableDictionary(pair => pair.Key, pair => pair.Value);

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