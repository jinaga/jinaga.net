using System;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Serialization;
using Jinaga.Test.Model;
using Xunit;

namespace Jinaga.Test.Facts
{
    public class SerializeTest
    {
        [Fact]
        public void SerializeType()
        {
            var graph = Serialize(new Airline("value"));

            graph.Last.Type.Should().Be("Skylane.Airline");
        }
        
        [Fact]
        public void SerializeStringField()
        {
            var graph = Serialize(new Airline("value"));

            var fact = graph.GetFact(graph.Last);
            var field = fact.Fields.Should().ContainSingle().Subject;
            field.Name.Should().Be("identifier");
            field.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().Be("value");
        }

        [Fact]
        public void SerializeDateTimeField()
        {            
            DateTime now = DateTime.Parse("2021-07-04T01:39:43.241Z");
            var graph = Serialize(new AirlineDay(new Airline("value"), now));

            var airlineDay = graph.GetFact(graph.Last);
            var field = airlineDay.Fields.Should().ContainSingle().Subject;
            field.Name.Should().Be("date");
            field.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().Be("2021-07-04T01:39:43.241Z");
        }

        [Fact]
        public void SerializeInteger()
        {
            var graph = Serialize(new Flight(new AirlineDay(new Airline("IA"), DateTime.Today), 4272));

            var flight = graph.GetFact(graph.Last);
            var field = flight.Fields.Should().ContainSingle().Subject;
            field.Name.Should().Be("flightNumber");
            field.Value.Should().BeOfType<FieldValueNumber>().Which
                .DoubleValue.Should().Be(4272.0);
        }

        [Fact]
        public void SerializePredecessor()
        {
            var now = DateTime.UtcNow;
            var graph = Serialize(new AirlineDay(new Airline("value"), now));

            var airlineDay = graph.GetFact(graph.Last);
            airlineDay.Reference.Type.Should().Be("Skylane.Airline.Day");
            var predecessor = airlineDay.Predecessors.Should().ContainSingle().Subject;
            predecessor.Role.Should().Be("airline");
            var airlineReference = predecessor.Should().BeOfType<PredecessorSingle>()
                .Subject.Reference;
            airlineReference.Type.Should().Be("Skylane.Airline");

            var airline = graph.GetFact(airlineReference);
            airline.Reference.Type.Should().Be("Skylane.Airline");
            airline.Predecessors.Should().BeEmpty();
            airlineReference.Hash.Should().Be(airline.Reference.Hash);
        }

        [Fact]
        public void SerializeMultiplePredecessors()
        {
            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var firstName = new PassengerName(passenger, "George", new PassengerName[0]);
            var secondName = new PassengerName(passenger, "Jorge", new PassengerName[] { firstName });

            var graph = Serialize(secondName);
            var fact = graph.GetFact(graph.Last);

            fact.Predecessors.Where(p => p.Role == "prior").Should().ContainSingle().Which
                .Should().BeOfType<PredecessorMultiple>();
        }

        [Fact]
        public void SerializeRoundTripMultiplePredecessors()
        {
            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var firstName = new PassengerName(passenger, "George", new PassengerName[0]);
            var secondName = new PassengerName(passenger, "Jorge", new PassengerName[] { firstName });

            var graph = Serialize(secondName);
            var roundTrip = Deserialize<PassengerName>(graph, graph.Last);

            roundTrip.prior.Should().ContainSingle().Which
                .value.Should().Be("George");
        }

        [Fact]
        public void SerializeGuid()
        {
            var school = new School(Guid.NewGuid());

            var graph = Serialize(school);
            var fact = graph.GetFact(graph.Last);

            var field = fact.Fields.Should().ContainSingle().Subject;
            field.Name.Should().Be("identifier");
            field.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().Be(school.identifier.ToString());
        }

        [Fact]
        public void SerializeNullableGuid()
        {
            var school = new OldSchool("IHS", null);

            var graph = Serialize(school);
            var fact = graph.GetFact(graph.Last);

            fact.Fields.Should().HaveCount(2);
            var name = fact.Fields.Single(f => f.Name == "name");
            name.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().Be("IHS");
            var identifier = fact.Fields.Single(f => f.Name == "identifier");
            identifier.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().Be(string.Empty);
        }

        [Fact]
        public void SerializeNullableGuidWithValue()
        {
            var school = new OldSchool("IHS", Guid.NewGuid());

            var graph = Serialize(school);
            var fact = graph.GetFact(graph.Last);

            fact.Fields.Should().HaveCount(2);
            var name = fact.Fields.Single(f => f.Name == "name");
            name.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().Be("IHS");
            var identifier = fact.Fields.Single(f => f.Name == "identifier");
            identifier.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().Be(school.identifier.Value.ToString());
        }

        [Fact]
        public void SerializeGuidRoundTrip()
        {
            Guid identifier = Guid.NewGuid();
            var school = new School(identifier);

            var graph = Serialize(school);
            var roundTrip = Deserialize<School>(graph, graph.Last);

            roundTrip.identifier.Should().Be(identifier);
        }

        [Fact]
        public void SerializeNullableGuidWithValueRoundTrip()
        {
            Guid identifier = Guid.NewGuid();
            var school = new OldSchool("IHS", identifier);

            var graph = Serialize(school);
            var roundTrip = Deserialize<OldSchool>(graph, graph.Last);

            roundTrip.name.Should().Be("IHS");
            roundTrip.identifier.Should().Be(identifier);
        }

        [Fact]
        public void SerializeNullableGuidWithNullRoundTrip()
        {
            var school = new OldSchool("IHS", null);

            var graph = Serialize(school);
            var roundTrip = Deserialize<OldSchool>(graph, graph.Last);

            roundTrip.name.Should().Be("IHS");
            roundTrip.identifier.Should().BeNull();
        }

        private readonly ConditionalWeakTable<object, FactGraph> graphByFact = new();

        private FactGraph Serialize(object runtimeFact)
        {
            var collector = new Collector(SerializerCache.Empty, graphByFact);
            var reference = collector.Serialize(runtimeFact);
            return collector.Graph;
        }

        private T Deserialize<T>(FactGraph graph, FactReference reference)
        {
            var emitter = new Emitter(graph, DeserializerCache.Empty, graphByFact);
            var runtimeFact = emitter.Deserialize<T>(reference);
            return runtimeFact;
        }
    }
}
