using Jinaga.Facts;
using Jinaga.Identity;
using Jinaga.Pipelines;
using Jinaga.Products;
using Jinaga.Projections;
using Jinaga.Services;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jinaga.Observers
{
    internal class ObservableSource
    {
        private class SpecificationWithListeners
        {
            public Specification Specification { get; }
            public ImmutableList<SpecificationListener> Listeners { get; }

            public SpecificationWithListeners(Specification specification, ImmutableList<SpecificationListener> listeners)
            {
                Specification = specification;
                Listeners = listeners;
            }

            public SpecificationWithListeners Add(SpecificationListener specificationListener)
            {
                return new SpecificationWithListeners(
                    Specification,
                    Listeners.Add(specificationListener)
                );
            }
        }

        /// <summary>
        /// Identifies the read that inverse listeners share. The inverse is identified by
        /// a hash of its description, and the givens by their own value, so two listeners
        /// on one inverse with different givens read separately.
        /// </summary>
        private readonly struct InverseKey : IEquatable<InverseKey>
        {
            private readonly string specificationKey;
            private readonly FactReferenceTuple givens;

            public InverseKey(string specificationKey, FactReferenceTuple givens)
            {
                this.specificationKey = specificationKey;
                this.givens = givens;
            }

            public bool Equals(InverseKey other)
            {
                return specificationKey == other.specificationKey &&
                    givens.Equals(other.givens);
            }

            public override bool Equals(object? obj)
            {
                return obj is InverseKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return specificationKey.GetHashCode() ^ givens.GetHashCode();
            }
        }

        private class InverseWithListeners
        {
            public Specification Inverse { get; }
            public FactReferenceTuple Givens { get; }
            public ImmutableList<SpecificationListener> Listeners { get; }

            public InverseWithListeners(Specification inverse, FactReferenceTuple givens, ImmutableList<SpecificationListener> listeners)
            {
                Inverse = inverse;
                Givens = givens;
                Listeners = listeners;
            }

            public InverseWithListeners With(ImmutableList<SpecificationListener> listeners)
            {
                return new InverseWithListeners(Inverse, Givens, listeners);
            }
        }

        private readonly IStore store;

        private ImmutableDictionary<string, ImmutableDictionary<string, SpecificationWithListeners>> listenersByTypeAndHash =
            ImmutableDictionary<string, ImmutableDictionary<string, SpecificationWithListeners>>.Empty;

        private ImmutableDictionary<string, ImmutableDictionary<InverseKey, InverseWithListeners>> inverseListenersByTypeAndKey =
            ImmutableDictionary<string, ImmutableDictionary<InverseKey, InverseWithListeners>>.Empty;

        private ImmutableDictionary<FactReference, ImmutableList<FactListener>> factListenersByReference =
            ImmutableDictionary<FactReference, ImmutableList<FactListener>>.Empty;

        public ObservableSource(IStore store)
        {
            this.store = store;
        }

        public SpecificationListener AddSpecificationListener(Specification specification, Func<ImmutableList<Product>, CancellationToken, Task> onResult)
        {
            if (specification.Givens.Count != 1)
            {
                throw new ArgumentException("The specification must have exactly one given.");
            }
            var givenType = specification.Givens[0].Label.Type;
            var specificationKey = IdentityUtilities.ComputeStringHash(specification.ToDescriptiveString());

            var specificationListener = new SpecificationListener(onResult);
            lock (this)
            {
                if (!listenersByTypeAndHash.TryGetValue(givenType, out var listenersByHash))
                {
                    listenersByHash = ImmutableDictionary<string, SpecificationWithListeners>.Empty;
                }
                if (!listenersByHash.TryGetValue(specificationKey, out var specificationWithListeners))
                {
                    specificationWithListeners = new SpecificationWithListeners(specification, ImmutableList<SpecificationListener>.Empty);
                }
                specificationWithListeners = specificationWithListeners.Add(specificationListener);
                listenersByHash = listenersByHash.SetItem(specificationKey, specificationWithListeners);
                listenersByTypeAndHash = listenersByTypeAndHash.SetItem(givenType, listenersByHash);
            }

            return specificationListener;
        }

        public void RemoveSpecificationListener(SpecificationListener listener)
        {
            lock (this)
            {
                foreach (var type in listenersByTypeAndHash.Keys)
                {
                    var listenersByHash = listenersByTypeAndHash[type];
                    foreach (var hash in listenersByHash.Keys)
                    {
                        var specificationWithListeners = listenersByHash[hash];
                        var specification = specificationWithListeners.Specification;
                        var listeners = specificationWithListeners.Listeners;
                        if (listeners.Contains(listener))
                        {
                            listeners = listeners.Remove(listener);

                            if (listeners.Count == 0)
                            {
                                listenersByHash = listenersByHash.Remove(hash);
                            }
                            else
                            {
                                listenersByHash = listenersByHash.SetItem(hash, new SpecificationWithListeners(specification, listeners));
                            }

                            if (listenersByHash.Count == 0)
                            {
                                listenersByTypeAndHash = listenersByTypeAndHash.Remove(type);
                            }
                            else
                            {
                                listenersByTypeAndHash = listenersByTypeAndHash.SetItem(type, listenersByHash);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Listens for the facts that an inverse is pinned on.
        /// </summary>
        /// <remarks>
        /// An inverse's givens are the watch's own givens followed by
        /// <see cref="InverseCandidates.PinLabel"/>. The type of that last given is the type
        /// of fact this listener waits for, and <paramref name="givens"/> supplies the rest,
        /// so the read is already restricted to one watch and the listener filters nothing.
        /// </remarks>
        /// <param name="inverse">An inverse whose last given is the pin label</param>
        /// <param name="givens">A reference for each of the inverse's other givens</param>
        /// <param name="onResult">Receives the products the inverse produces</param>
        public SpecificationListener AddInverseListener(Specification inverse, FactReferenceTuple givens, Func<ImmutableList<Product>, CancellationToken, Task> onResult)
        {
            if (inverse.Givens.Count == 0 ||
                inverse.Givens[inverse.Givens.Count - 1].Label.Name != InverseCandidates.PinLabel)
            {
                throw new ArgumentException($"The last given of an inverse must be named {InverseCandidates.PinLabel}.");
            }
            if (givens.Names.Contains(InverseCandidates.PinLabel))
            {
                throw new ArgumentException($"The tuple must leave {InverseCandidates.PinLabel} to the saved fact.");
            }
            var pinnedType = inverse.Givens[inverse.Givens.Count - 1].Label.Type;
            var missing = inverse.Givens
                .Take(inverse.Givens.Count - 1)
                .Select(given => given.Label.Name)
                .Where(name => !givens.Names.Contains(name))
                .ToImmutableList();
            if (missing.Any())
            {
                throw new ArgumentException($"The tuple does not contain a reference for the inverse's given {string.Join(", ", missing)}.");
            }

            var inverseKey = new InverseKey(
                IdentityUtilities.ComputeStringHash(inverse.ToDescriptiveString()),
                givens);

            var specificationListener = new SpecificationListener(onResult);
            lock (this)
            {
                if (!inverseListenersByTypeAndKey.TryGetValue(pinnedType, out var listenersByKey))
                {
                    listenersByKey = ImmutableDictionary<InverseKey, InverseWithListeners>.Empty;
                }
                if (!listenersByKey.TryGetValue(inverseKey, out var inverseWithListeners))
                {
                    inverseWithListeners = new InverseWithListeners(inverse, givens, ImmutableList<SpecificationListener>.Empty);
                }
                inverseWithListeners = inverseWithListeners.With(inverseWithListeners.Listeners.Add(specificationListener));
                listenersByKey = listenersByKey.SetItem(inverseKey, inverseWithListeners);
                inverseListenersByTypeAndKey = inverseListenersByTypeAndKey.SetItem(pinnedType, listenersByKey);
            }

            return specificationListener;
        }

        public void RemoveInverseListener(SpecificationListener listener)
        {
            lock (this)
            {
                foreach (var type in inverseListenersByTypeAndKey.Keys)
                {
                    var listenersByKey = inverseListenersByTypeAndKey[type];
                    foreach (var inverseKey in listenersByKey.Keys)
                    {
                        var inverseWithListeners = listenersByKey[inverseKey];
                        var listeners = inverseWithListeners.Listeners;
                        if (listeners.Contains(listener))
                        {
                            listeners = listeners.Remove(listener);
                            listenersByKey = listeners.Count == 0
                                ? listenersByKey.Remove(inverseKey)
                                : listenersByKey.SetItem(inverseKey, inverseWithListeners.With(listeners));
                        }
                    }

                    inverseListenersByTypeAndKey = listenersByKey.Count == 0
                        ? inverseListenersByTypeAndKey.Remove(type)
                        : inverseListenersByTypeAndKey.SetItem(type, listenersByKey);
                }
            }
        }

        /// <summary>
        /// Listens for the save of one fact, named by reference.
        /// </summary>
        /// <remarks>
        /// This is the cheapest listener there is: it reads nothing, and it fires only for
        /// the fact it names. An observer whose own given is saved has to read its whole
        /// specification, and a listener keyed by the given's type would pay for that read
        /// on every fact of that type and then discard all but the one row it wanted. The
        /// comparison that decides whether to read belongs here, where it is a reference
        /// equality against a dictionary key.
        /// </remarks>
        /// <param name="reference">The fact to wait for</param>
        /// <param name="onSaved">Called when that fact is saved</param>
        public FactListener AddFactListener(FactReference reference, Func<CancellationToken, Task> onSaved)
        {
            var factListener = new FactListener(onSaved);
            lock (this)
            {
                if (!factListenersByReference.TryGetValue(reference, out var listeners))
                {
                    listeners = ImmutableList<FactListener>.Empty;
                }
                factListenersByReference = factListenersByReference.SetItem(
                    reference, listeners.Add(factListener));
            }
            return factListener;
        }

        public void RemoveFactListener(FactListener listener)
        {
            lock (this)
            {
                foreach (var reference in factListenersByReference.Keys)
                {
                    var listeners = factListenersByReference[reference];
                    if (listeners.Contains(listener))
                    {
                        listeners = listeners.Remove(listener);
                        factListenersByReference = listeners.IsEmpty
                            ? factListenersByReference.Remove(reference)
                            : factListenersByReference.SetItem(reference, listeners);
                    }
                }
            }
        }

        public async Task Notify(FactGraph graph, ImmutableList<Fact> facts, CancellationToken cancellationToken)
        {
            foreach (var fact in facts)
            {
                await NotifyFactSaved(graph, fact, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task NotifyFactSaved(FactGraph graph, Fact fact, CancellationToken cancellationToken)
        {
            await NotifySpecificationListeners(graph, fact, cancellationToken).ConfigureAwait(false);
            await NotifyInverseListeners(fact, cancellationToken).ConfigureAwait(false);
            await NotifyFactListeners(fact, cancellationToken).ConfigureAwait(false);
        }

        private async Task NotifyFactListeners(Fact fact, CancellationToken cancellationToken)
        {
            if (factListenersByReference.TryGetValue(fact.Reference, out var listeners))
            {
                foreach (var listener in listeners)
                {
                    await listener.OnSaved(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task NotifySpecificationListeners(FactGraph graph, Fact fact, CancellationToken cancellationToken)
        {
            if (listenersByTypeAndHash.TryGetValue(fact.Reference.Type, out var listenersByHash))
            {
                foreach (var specificationWithListeners in listenersByHash.Values)
                {
                    var listeners = specificationWithListeners.Listeners;
                    if (listeners.Any())
                    {
                        var specification = specificationWithListeners.Specification;
                        var givenReference = fact.Reference;
                        string name = specification.Givens.Single().Label.Name;
                        var givenTuple = FactReferenceTuple.Empty.Add(name, givenReference);
                        var graphSpecification = GraphSpecification.From(specification, out _);
                        var products = graphSpecification != null
                            ? graphSpecification.Execute(givenTuple, graph)
                            : await store.Read(givenTuple, specification, cancellationToken).ConfigureAwait(false);
                        foreach (var listener in listeners)
                        {
                            await listener.OnResult(products, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads each inverse that is pinned on this fact's type, once per distinct givens.
        /// </summary>
        /// <remarks>
        /// The read goes to the store rather than to the saved batch's graph. An inverse
        /// binds the watch's givens, and neither they nor their predecessors are generally
        /// in that graph.
        /// </remarks>
        private async Task NotifyInverseListeners(Fact fact, CancellationToken cancellationToken)
        {
            if (inverseListenersByTypeAndKey.TryGetValue(fact.Reference.Type, out var listenersByKey))
            {
                foreach (var inverseWithListeners in listenersByKey.Values)
                {
                    var listeners = inverseWithListeners.Listeners;
                    if (listeners.Any())
                    {
                        var givenTuple = inverseWithListeners.Givens
                            .Add(InverseCandidates.PinLabel, fact.Reference);
                        var products = await store.Read(givenTuple, inverseWithListeners.Inverse, cancellationToken).ConfigureAwait(false);
                        foreach (var listener in listeners)
                        {
                            await listener.OnResult(products, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }
    }
}
