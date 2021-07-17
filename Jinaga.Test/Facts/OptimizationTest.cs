using System;
using System.Collections.Immutable;
using System.Linq.Expressions;
using FluentAssertions;
using Jinaga.Facts;
using Jinaga.Test.Model;
using Xunit;

namespace Jinaga.Test.Facts
{
    public class OptimizationTest
    {
        [Fact]
        public void Optimization_LimitVisits()
        {
            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var firstName = new PassengerName(passenger, "George", new PassengerName[0]);
            var secondName = new PassengerName(passenger, "Jorge", new PassengerName[] { firstName });

            var collector = new Collector();
            var result = collector.Serialize(secondName);
            int factVisits = collector.FactVisitsCount;

            factVisits.Should().Be(5);
        }

        [Fact]
        public void Optimization_DetectsCycles()
        {
            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var firstName = new PassengerName(passenger, "George", new PassengerName[0]);
            var prior = new PassengerName[] { firstName };
            var secondName = new PassengerName(passenger, "Jorge", prior);

            // You have to really want to do this
            prior[0] = secondName;

            var collector = new Collector();
            Action serialize = () => collector.Serialize(secondName);
            serialize.Should().Throw<ArgumentException>()
                .WithMessage("Jinaga cannot serialize a fact containing a cycle");
        }

        [Fact]
        public void Optimization_CacheTypeSerializers()
        {
            Observe((airline, collector) => CreateFact(
                "Skylane.Airline",
                ImmutableList<Field>.Empty
                    .Add(new Field("identifier", new FieldValueString(airline.identifier))),
                ImmutableList<Predecessor>.Empty
            ));

            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var firstName = new PassengerName(passenger, "George", new PassengerName[0]);
            var secondName = new PassengerName(passenger, "Jorge", new PassengerName[] { firstName });

            var serializerCache = new SerializerCache();
            var collector = new Collector(serializerCache);
            var result = collector.Serialize(secondName);
            collector.SerializerCache.TypeCount.Should().Be(4);
        }

        private static Fact CreateFact(string factType, ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            var reference = new FactReference(factType, Collector.ComputeHash(fields, predecessors));
            var fact = new Fact(reference, fields, predecessors);
            return fact;
        }

        private static void Observe(Expression<Func<Airline, Collector, Fact>> serializer)
        {
            var output = serializer.ToString();
            var reduced = serializer.Reduce();
            var reducedOutput = reduced.ToString();

            /**

            **/
        }
    }
}
