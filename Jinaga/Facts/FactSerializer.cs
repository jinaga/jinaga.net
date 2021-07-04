using System;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Facts
{
    public partial class FactSerializer
    {
        public static ImmutableList<Fact> Serialize(object runtimeFact)
        {
            var collector = new Collector();
            var reference = collector.Serialize(runtimeFact);
            return collector.Facts;
        }

        public static TFact Deserialize<TFact>(ImmutableList<Fact> facts, FactReference reference)
        {
            var runtimeType = typeof(TFact);
            object runtimeFact = DeserializeFact(facts, reference, runtimeType);
            return (TFact)runtimeFact;
        }

        private static object DeserializeFact(ImmutableList<Fact> facts, FactReference reference, Type runtimeType)
        {
            var fact = facts.Single(f => f.Reference == reference);
            var constructor = runtimeType.GetConstructors().Single();
            var parameters = constructor.GetParameters();
            var parameterValues = parameters
                .Select(parameter =>
                    Collector.IsField(parameter.ParameterType)
                        ? GetFieldValue(parameter.ParameterType, fact.Fields.Single(f => f.Name == parameter.Name).Value) :
                    !Collector.IsCondition(parameter.ParameterType)
                        ? GetPredecessorValue(parameter.ParameterType, fact.Predecessors.Single(p => p.Role == parameter.Name), facts) :
                    throw new NotImplementedException()
                )
                .ToArray();
            var runtimeFact = constructor.Invoke(parameterValues);
            return runtimeFact;
        }

        private static object GetFieldValue(Type parameterType, FieldValue value)
        {
            switch (value)
            {
                case FieldValueString str:
                    return
                        parameterType == typeof(string)
                            ? (object)str.StringValue :
                        parameterType == typeof(DateTime)
                            ? (object)DateTime.Parse(str.StringValue) :
                        throw new NotImplementedException();
                case FieldValueNumber number:
                    return
                        parameterType == typeof(int)
                            ? (object)(int)number.DoubleValue :
                        parameterType == typeof(float)
                            ? (object)(float)number.DoubleValue :
                        parameterType == typeof(double)
                            ? (object)(double)number.DoubleValue :
                        throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }

        private static object GetPredecessorValue(Type parameterType, Predecessor predecessor, ImmutableList<Fact> facts)
        {
            switch (predecessor)
            {
                case PredecessorSingle single:
                    return DeserializeFact(facts, single.Reference, parameterType);
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
