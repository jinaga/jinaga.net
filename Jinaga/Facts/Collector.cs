using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Jinaga.Facts
{
    class Collector
    {
        public FactGraph Graph { get; private set; } = new FactGraph();

        public int FactVisitsCount { get; private set; } = 0;
        public SerializerCache SerializerCache { get; private set; }

        public ImmutableHashSet<object> visiting =
            ImmutableHashSet<object>.Empty;
        public ImmutableDictionary<object, FactReference> referenceByObject =
            ImmutableDictionary<object, FactReference>.Empty;

        public Collector(SerializerCache serializerCache)
        {
            this.SerializerCache = serializerCache;
        }

        public FactReference Serialize(object runtimeFact)
        {
            if (!referenceByObject.TryGetValue(runtimeFact, out var reference))
            {
                if (visiting.Contains(runtimeFact))
                {
                    throw new ArgumentException("Jinaga cannot serialize a fact containing a cycle");
                }
                visiting = visiting.Add(runtimeFact);
                FactVisitsCount++;

                var runtimeType = runtimeFact.GetType();
                Func<object, Collector, Fact> serializer = GetSerializer(runtimeType);
                try
                {
                    var fact = serializer(runtimeFact, this);
                    reference = fact.Reference;

                    Graph = Graph.Add(fact);
                    referenceByObject = referenceByObject.Add(runtimeFact, reference);
                }
                catch (TargetInvocationException tie)
                {
                    throw tie.InnerException;
                }
            }
            return reference;
        }

        private Func<object, Collector, Fact> GetSerializer(Type runtimeType)
        {
            var (newCache, serializer) = SerializerCache.GetSerializer(runtimeType);
            SerializerCache = newCache;
            return serializer;
        }

        public static bool IsField(Type type)
        {
            return
                type == typeof(string) ||
                type == typeof(DateTime) ||
                type == typeof(int) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(bool);
        }

        public static bool IsPredecessor(Type type)
        {
            return
                IsFactType(type) ||
                IsArrayOfFactType(type);
        }

        private static bool IsFactType(Type type)
        {
            return type
                .GetCustomAttributes(inherit: false)
                .OfType<FactTypeAttribute>()
                .Any();
        }

        private static bool IsArrayOfFactType(Type type)
        {
            return
                type.IsArray &&
                IsFactType(type.GetElementType());
        }
    }
}
