using Jinaga.Facts;
using Jinaga.Products;
using Jinaga.Serialization;
using Jinaga.Test.Model;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Jinaga.Test.Facts
{
    public class OptimizationTest
    {
        private readonly ConditionalWeakTable<object, FactGraph> graphByFact = new();

        [Fact]
        public void Optimization_LimitVisits()
        {
            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var firstName = new PassengerName(passenger, "George", new PassengerName[0]);
            var secondName = new PassengerName(passenger, "Jorge", new PassengerName[] { firstName });

            var collector = new Collector(SerializerCache.Empty, new());
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

            var collector = new Collector(SerializerCache.Empty, new());
            Action serialize = () => collector.Serialize(secondName);
            serialize.Should().Throw<ArgumentException>()
                .WithMessage("Jinaga cannot serialize a fact containing a cycle");
        }

        [Fact]
        public void Optimization_CreateSerializer()
        {
            var serializerCache = SerializerCache.Empty;
            var (newCache, serializer) = serializerCache.GetSerializer(typeof(Airline));
            newCache.TypeCount.Should().Be(1);
        }

        [Fact]
        public void Optimization_CacheTypeSerializers()
        {
            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var firstName = new PassengerName(passenger, "George", new PassengerName[0]);
            var secondName = new PassengerName(passenger, "Jorge", new PassengerName[] { firstName });

            var serializerCache = SerializerCache.Empty;
            var collector = new Collector(serializerCache, new());
            var result = collector.Serialize(secondName);
            collector.SerializerCache.TypeCount.Should().Be(4);
        }

        [Fact]
        public void Optimization_ReuseObjects()
        {
            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var firstName = new PassengerName(passenger, "George", new PassengerName[0]);
            var secondName = new PassengerName(passenger, "Jorge", new PassengerName[] { firstName });

            var collector = new Collector(SerializerCache.Empty, new());
            var result = collector.Serialize(secondName);

            var emitter = new Emitter(collector.Graph, DeserializerCache.Empty, graphByFact);
            var deserialized = emitter.Deserialize<PassengerName>(result);

            deserialized.passenger.Should().BeSameAs(deserialized.prior[0].passenger);
        }

        [Fact]
        public void Optimize_PredecessorQueryCanRunOnGraph()
        {
            var userWithName = Given<PassengerName>.Match(passengerName =>
                passengerName.passenger.user);

            userWithName.CanRunOnGraph.Should().BeTrue();

            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var passengerName = new PassengerName(passenger, "George", new PassengerName[0]);

            var collector = new Collector(SerializerCache.Empty, new());
            var reference = collector.Serialize(passengerName);
            var graph = collector.Graph;
            var tuple = FactReferenceTuple.Empty
                .Add(userWithName.Givens.Single().Label.Name, reference);

            var references = userWithName
                .Execute(tuple, graph)
                .Select(p => p.GetElement("user"))
                .OfType<SimpleElement>()
                .Select(e => e.FactReference)
                .ToImmutableList();
            var userReference = references.Should().ContainSingle().Subject;

            var emitter = new Emitter(graph, DeserializerCache.Empty, graphByFact);
            var user = emitter.Deserialize<User>(userReference);
            user.publicKey.Should().Be("--- PUBLIC KEY ---");
        }

        [Fact]
        public async Task Optimization_RunPipelineOnGraph()
        {
            var userWithName = Given<PassengerName>.Match(passengerName =>
                passengerName.passenger.user);

            var passenger = new Passenger(new Airline("IA"), new User("--- PUBLIC KEY ---"));
            var passengerName = new PassengerName(passenger, "George", new PassengerName[0]);

            // This instance does not contain the facts.
            var j = JinagaTest.Create();
            var users = await j.Query(userWithName, passengerName);

            // But the graph does.
            users.Should().ContainSingle().Which
                .publicKey.Should().Be("--- PUBLIC KEY ---");
        }
    }
}
