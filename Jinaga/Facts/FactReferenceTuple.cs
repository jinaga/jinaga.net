using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Facts
{
    public class FactReferenceTuple
    {
        public static FactReferenceTuple Empty { get; } = new FactReferenceTuple(ImmutableDictionary<string, FactReference>.Empty);

        private readonly ImmutableDictionary<string, FactReference> referenceByName;

        internal FactReferenceTuple(ImmutableDictionary<string, FactReference> referenceByName)
        {
            this.referenceByName = referenceByName;
        }

        public FactReferenceTuple Add(string name, FactReference reference)
        {
            return new FactReferenceTuple(referenceByName.Add(name, reference));
        }

        /// <summary>
        /// The tuple without the named binding, or this tuple if it has none.
        ///
        /// A match inside an existential condition may declare a label that the enclosing
        /// specification has already bound. The inner declaration is a new variable, so the
        /// outer binding leaves scope before the condition runs. Removing it is what lets
        /// <see cref="Add"/> keep throwing on a label bound twice in one scope.
        /// </summary>
        internal FactReferenceTuple Remove(string name)
        {
            return new FactReferenceTuple(referenceByName.Remove(name));
        }

        public IEnumerable<string> Names => referenceByName.Keys;

        public FactReference Get(string name)
        {
            if (referenceByName.TryGetValue(name, out var factReference))
            {
                return factReference;
            }
            else
            {
                throw new ArgumentException($"The tuple does not contain a reference named {name}.");
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (FactReferenceTuple)obj;
            return referenceByName.Count == other.referenceByName.Count &&
                referenceByName.All(pair =>
                    other.referenceByName.TryGetValue(pair.Key, out var otherReference) &&
                    pair.Value.Equals(otherReference));
        }

        public override int GetHashCode()
        {
            return referenceByName.Aggregate(0, (hash, pair) =>
                hash ^ pair.Key.GetHashCode() ^ pair.Value.GetHashCode());
        }
    }
}
