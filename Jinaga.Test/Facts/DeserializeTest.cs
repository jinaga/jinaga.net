using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Serialization;
using Jinaga.Test.Model;
using System.Collections.Immutable;
using Xunit;

namespace Jinaga.Test.Facts
{
    public class DeserializeTest
    {
        [Fact]
        public void Deserialize_StringField()
        {
            var fact = Fact.Create(
                "Skylane.Airline",
                ImmutableList<Field>.Empty.Add(new Field("identifier", new FieldValueString("value"))),
                ImmutableList<Predecessor>.Empty
            );
            var graph = new FactGraph().Add(fact);
            var airline = Deserialize<Airline>(graph, fact.Reference);

            airline.identifier.Should().Be("value");
        }

        private static T Deserialize<T>(FactGraph graph, FactReference reference)
        {
            var emitter = new Emitter(graph, new DeserializerCache());
            var runtimeFact = emitter.Deserialize(reference, typeof(T));
            return (T)runtimeFact;
        }
    }
}
