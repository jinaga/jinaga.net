using Jinaga.Facts;
using Jinaga.Observers;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Jinaga.Serialization
{
    class Emitter
    {
        public FactGraph Graph { get; }

        public DeserializerCache DeserializerCache { get; private set; }

        private ImmutableDictionary<FactReference, object> objectByReference =
            ImmutableDictionary<FactReference, object>.Empty;

        public IWatchContext? WatchContext { get; }

        private readonly ConditionalWeakTable<object, FactGraph> graphByFact;

        /// <summary>
        /// Dates learned travel beside the graph rather than inside it, because the graph is
        /// what the serializer sends to the network and a date learned is replica-local.
        /// </summary>
        private readonly ImmutableDictionary<FactReference, DateTime> datesLearned;

        /// <summary>
        /// Local authors travel beside the graph for the same reason dates learned do: the
        /// graph is what the serializer sends, and an authorship record is replica-local.
        /// </summary>
        private readonly ImmutableDictionary<FactReference, string> localAuthors;

        public Emitter(FactGraph graph, DeserializerCache deserializerCache, ConditionalWeakTable<object, FactGraph> graphByFact, IWatchContext? watchContext = null, ImmutableDictionary<FactReference, DateTime>? datesLearned = null, ImmutableDictionary<FactReference, string>? localAuthors = null)
        {
            this.Graph = graph;
            DeserializerCache = deserializerCache;
            this.graphByFact = graphByFact;
            WatchContext = watchContext;
            this.datesLearned = datesLearned ?? ImmutableDictionary<FactReference, DateTime>.Empty;
            this.localAuthors = localAuthors ?? ImmutableDictionary<FactReference, string>.Empty;
        }

        public DateTime GetDateLearned(FactReference reference)
        {
            if (!datesLearned.TryGetValue(reference, out var dateLearned))
            {
                throw new InvalidOperationException($"The store did not report when it learned {reference.Type} {reference.Hash}.");
            }
            return dateLearned;
        }

        /// <summary>
        /// The public key of the user who authored the fact on this replica, or null if the
        /// store recorded none.
        ///
        /// Absence is an ordinary answer rather than an error, unlike a missing date learned:
        /// a fact that arrived from a feed, or one saved while nobody was logged in, has no
        /// local author and is attributed by its signatures alone.
        /// </summary>
        public string? GetLocalAuthor(FactReference reference)
        {
            return localAuthors.TryGetValue(reference, out var author)
                ? author
                : null;
        }

        public TFact Deserialize<TFact>(FactReference reference)
        {
            Type type = typeof(TFact);
            object runtimeFact = DeserializeToType(reference, type);

            return (TFact)runtimeFact;
        }

        public object DeserializeToType(FactReference reference, Type type)
        {
            if (!objectByReference.TryGetValue(reference, out var runtimeFact))
            {
                var (newCache, deserializer) = DeserializerCache.GetDeserializer(type);
                DeserializerCache = newCache;
                try
                {
                    runtimeFact = deserializer(Graph.GetFact(reference), this);
                    objectByReference = objectByReference.Add(reference, runtimeFact);
                }
                catch (TargetInvocationException tie)
                {
                    throw tie.InnerException;
                }
            }

            return runtimeFact;
        }

        public TFact[] DeserializeArray<TFact>(ImmutableList<FactReference> references)
        {
            return references
                .Select(reference => Deserialize<TFact>(reference))
                .ToArray();
        }

        public T SetGraph<T>(Fact fact, T runtimeFact)
        {
            if (runtimeFact != null)
            {
                graphByFact.Add(runtimeFact, Graph.GetSubgraph(fact.Reference));
            }
            return runtimeFact;
        }
    }
}
