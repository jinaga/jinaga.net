using System;
using System.Linq;
using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Test.Model;
using Xunit;

namespace Jinaga.Test.Facts
{
    public class SerializeTest
    {
        [Fact]
        public void SerializeType()
        {
            var facts = FactSerializer.Serialize(new Airline("value"));

            facts.Should().ContainSingle().Which
                .Type.Should().Be("Skylane.Airline");
        }
        
        [Fact]
        public void SerializeStringField()
        {
            var facts = FactSerializer.Serialize(new Airline("value"));

            var field = facts.Should().ContainSingle().Which
                .Fields.Should().ContainSingle().Subject;
            field.Name.Should().Be("identifier");
            field.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().Be("value");
        }

        [Fact]
        public void SerializeDateTimeField()
        {
            DateTime now = DateTime.Parse("7/4/2021 1:39:43.241Z");
            var facts = FactSerializer.Serialize(new AirlineDay(new Airline("value"), now));

            var airlineDay = facts.ElementAt(1);
            var field = airlineDay.Fields.Should().ContainSingle().Subject;
            field.Name.Should().Be("date");
            field.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().Be("2021-07-04T01:39:43.241Z");
        }

        [Fact]
        public void SerializeConvertDateTimeToUTC()
        {
            // This test will fail if the local timezone offset is 0
            // Sorry, London
            DateTime now = DateTime.Parse("7/4/2021 1:39:43.241");
            var facts = FactSerializer.Serialize(new AirlineDay(new Airline("value"), now));

            var airlineDay = facts.ElementAt(1);
            var field = airlineDay.Fields.Should().ContainSingle().Subject;
            field.Name.Should().Be("date");
            field.Value.Should().BeOfType<FieldValueString>().Which
                .StringValue.Should().NotBe("2021-07-04T01:39:43.241Z");
        }

        [Fact]
        public void SerializeInteger()
        {
            var facts = FactSerializer.Serialize(new Flight(new AirlineDay(new Airline("IA"), DateTime.Today), 4272));

            var flight = facts.Last();
            var field = flight.Fields.Should().ContainSingle().Subject;
            field.Name.Should().Be("flightNumber");
            field.Value.Should().BeOfType<FieldValueNumber>().Which
                .DoubleValue.Should().Be(4272.0);
        }

        [Fact]
        public void SerializePredecessor()
        {
            var now = DateTime.UtcNow;
            var facts = FactSerializer.Serialize(new AirlineDay(new Airline("value"), now));

            var airlineDay = facts.ElementAt(1);
            airlineDay.Type.Should().Be("Skylane.Airline.Day");
            var predecessor = airlineDay.Predecessors.Should().ContainSingle().Subject;
            predecessor.Role.Should().Be("airline");
            var airlineReference = predecessor.Should().BeOfType<PredecessorSingle>()
                .Subject.Reference;
            airlineReference.Type.Should().Be("Skylane.Airline");

            var airline = facts.ElementAt(0);
            airline.Type.Should().Be("Skylane.Airline");
            airline.Predecessors.Should().BeEmpty();
            airlineReference.Hash.Should().Be(airline.Hash);
        }
    }
}
