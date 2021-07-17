using System;
using System.Linq;

namespace Jinaga.Facts
{
    public partial class FactSerializer
    {
        public static FactGraph Serialize(object runtimeFact)
        {
            var collector = new Collector();
            var reference = collector.Serialize(runtimeFact);
            return collector.Graph;
        }

        public static TFact Deserialize<TFact>(FactGraph graph, FactReference reference)
        {
            var runtimeType = typeof(TFact);
            object runtimeFact = DeserializeFact(graph, reference, runtimeType);
            return (TFact)runtimeFact;
        }

        private static object DeserializeFact(FactGraph graph, FactReference reference, Type runtimeType)
        {
            var fact = graph.GetFact(reference);
            var constructor = runtimeType.GetConstructors().Single();
            var parameters = constructor.GetParameters();
            var parameterValues = parameters
                .Select(parameter =>
                    Collector.IsField(parameter.ParameterType)
                        ? GetFieldValue(parameter.ParameterType, fact.Fields.Single(f => f.Name == parameter.Name).Value) :
                    Collector.IsPredecessor(parameter.ParameterType)
                        ? GetPredecessorValue(parameter.ParameterType, fact.Predecessors.Single(p => p.Role == parameter.Name), graph) :
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
                            ? (object)DateTime.Parse(str.StringValue).ToUniversalTime() :
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

        private static object GetPredecessorValue(Type parameterType, Predecessor predecessor, FactGraph graph)
        {
            switch (predecessor)
            {
                case PredecessorSingle single:
                    return DeserializeFact(graph, single.Reference, parameterType);
                case PredecessorMultiple multiple:
                    var elementType = parameterType.GetElementType();
                    var facts = multiple.References.Select(r =>
                        DeserializeFact(graph, r, elementType)
                    ).ToArray();
                    var value = (Array)Activator.CreateInstance(parameterType, facts.Length);
                    Array.Copy(facts, value, facts.Length);
                    return value;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
