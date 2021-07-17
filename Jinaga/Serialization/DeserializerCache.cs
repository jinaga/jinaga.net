using Jinaga.Facts;
using System;
using System.Linq;

namespace Jinaga.Serialization
{
    class DeserializerCache
    {
        public (DeserializerCache, Func<Fact, Emitter, object>) GetDeserializer(Type type)
        {
            var runtimeType = type;
            Func<Fact, Emitter, object> deserializer = (fact, emitter) =>
            {
                var constructor = runtimeType.GetConstructors().Single();
                var parameters = constructor.GetParameters();
                var parameterValues = parameters
                    .Select(parameter =>
                        Interrogate.IsField(parameter.ParameterType)
                            ? GetFieldValue(parameter.ParameterType, fact.Fields.Single(f => f.Name == parameter.Name).Value) :
                        Interrogate.IsPredecessor(parameter.ParameterType)
                            ? GetPredecessorValue(parameter.ParameterType, fact.Predecessors.Single(p => p.Role == parameter.Name), emitter) :
                        throw new NotImplementedException()
                    )
                    .ToArray();
                var runtimeFact = constructor.Invoke(parameterValues);
                return runtimeFact;
            };
            return (this, deserializer);
        }

        private static object GetFieldValue(Type parameterType, FieldValue value)
        {
            switch (value)
            {
                case FieldValueString str:
                    return
                        parameterType == typeof(string)
                            ? str.StringValue :
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

        private static object GetPredecessorValue(Type parameterType, Predecessor predecessor, Emitter emitter)
        {
            switch (predecessor)
            {
                case PredecessorSingle single:
                    return emitter.Deserialize(single.Reference, parameterType);
                case PredecessorMultiple multiple:
                    var elementType = parameterType.GetElementType();
                    var facts = multiple.References.Select(r =>
                        emitter.Deserialize(r, elementType)
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
