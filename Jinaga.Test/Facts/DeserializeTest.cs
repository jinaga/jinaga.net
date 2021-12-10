using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Serialization;
using Jinaga.Test.Model;
using System;
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
            var graph = FactGraph.Empty.Add(fact);
            var airline = Deserialize<Airline>(graph, fact.Reference);

            airline.identifier.Should().Be("value");
        }

        [Fact]
        public void Deserialize_DateField()
        {
            DateTime now = DateTime.Parse("2021-07-04T01:39:43.241Z");
            var fact = Fact.Create(
                "Skylane.Airline",
                ImmutableList<Field>.Empty.Add(new Field("identifier", new FieldValueString("value"))),
                ImmutableList<Predecessor>.Empty
            );
            var successor = Fact.Create(
                "Skylane.Airline.Day",
                ImmutableList<Field>.Empty.Add(new Field("date", new FieldValueString(now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")))),
                ImmutableList<Predecessor>.Empty.Add(new PredecessorSingle("airline", fact.Reference))
            );
            var graph = FactGraph.Empty.Add(fact).Add(successor);
            var airlineDay = Deserialize<AirlineDay>(graph, successor.Reference);

            airlineDay.date.Kind.Should().Be(DateTimeKind.Utc);
            airlineDay.date.Hour.Should().Be(1);
        }

        private static T Deserialize<T>(FactGraph graph, FactReference reference)
        {
            var emitter = new Emitter(graph, DeserializerCache.Empty);
            var runtimeFact = emitter.Deserialize<T>(reference);
            return runtimeFact;
        }
    }
}
