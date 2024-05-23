using System;
using System.IO;
using Jinaga.Facts;
using System.Text.Json;
using System.Text;
using System.Collections.Immutable;
using System.Text.Encodings.Web;

namespace Jinaga.Http
{
    public class GraphSerializer : IDisposable
    {
        private readonly StreamWriter writer;
        private ImmutableDictionary<FactReference, int> factIndex = ImmutableDictionary<FactReference, int>.Empty;
        private ImmutableDictionary<string, int> publicKeyIndex = ImmutableDictionary<string, int>.Empty;

        public GraphSerializer(Stream stream)
        {
            writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);
            writer.NewLine = "\n";
        }

        public void Dispose()
        {
            writer.Dispose();
        }

        public void Serialize(FactGraph graph)
        {
            foreach (var factReference in graph.FactReferences)
            {
                var signatures = graph.GetSignatures(factReference);
                SerializePublicKeys(signatures);
                var fact = graph.GetFact(factReference);
                SerializeFact(fact, signatures);
            }
            writer.Flush();
        }

        private void SerializePublicKeys(ImmutableList<FactSignature> signatures)
        {
            var encoder = JavaScriptEncoder.Default;

            foreach (var signature in signatures)
            {
                if (!publicKeyIndex.ContainsKey(signature.PublicKey))
                {
                    writer.WriteLine($"PK{publicKeyIndex.Count}");
                    publicKeyIndex = publicKeyIndex.Add(signature.PublicKey, publicKeyIndex.Count);
                    writer.WriteLine($"\"{encoder.Encode(signature.PublicKey)}\"");
                    writer.WriteLine();
                }
            }
        }

        private void SerializeFact(Fact fact, ImmutableList<FactSignature> signatures)
        {
            var encoder = JavaScriptEncoder.Default;

            writer.WriteLine(JsonSerializer.Serialize(fact.Reference.Type));
            writer.WriteLine(SerializePredecessors(fact.Predecessors));
            writer.WriteLine(SerializeFields(fact.Fields));
            foreach (var signature in signatures)
            {
                writer.WriteLine($"PK{publicKeyIndex[signature.PublicKey]}");
                writer.WriteLine($"\"{encoder.Encode(signature.Signature)}\"");
            }
            writer.WriteLine();

            factIndex = factIndex.Add(fact.Reference, factIndex.Count);
        }

        private string SerializePredecessors(ImmutableList<Predecessor> predecessors)
        {
            var sb = new StringBuilder();
            var encoder = JavaScriptEncoder.Default;

            sb.Append("{");

            bool isFirstPredecessor = true;
            foreach (var predecessor in predecessors)
            {
                if (!isFirstPredecessor)
                {
                    sb.Append(",");
                }

                sb.Append($"\"{encoder.Encode(predecessor.Role)}\":");

                switch (predecessor)
                {
                    case PredecessorSingle predecessorSingle:
                        sb.Append(factIndex[predecessorSingle.Reference].ToString());
                        break;
                    case PredecessorMultiple predecessorMultiple:
                        sb.Append("[");
                        bool isFirstIndex = true;
                        foreach (var reference in predecessorMultiple.References)
                        {
                            if (!isFirstIndex)
                            {
                                sb.Append(",");
                            }
                            sb.Append(factIndex[reference].ToString());
                            isFirstIndex = false;
                        }
                        sb.Append("]");
                        break;
                }

                isFirstPredecessor = false;
            }

            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeFields(ImmutableList<Field> fields)
        {
            var sb = new StringBuilder();
            var encoder = JavaScriptEncoder.Default;

            sb.Append("{");

            bool isFirstField = true;
            foreach (var field in fields)
            {
                if (!isFirstField)
                {
                    sb.Append(",");
                }

                sb.Append($"\"{encoder.Encode(field.Name)}\":");

                switch (field.Value)
                {
                    case FieldValueBoolean fieldValueBoolean:
                        sb.Append(fieldValueBoolean.BoolValue.ToString().ToLowerInvariant());
                        break;
                    case FieldValueNumber fieldValueNumber:
                        sb.Append(fieldValueNumber.DoubleValue.ToString());
                        break;
                    case FieldValueString fieldValueString:
                        sb.Append($"\"{encoder.Encode(fieldValueString.StringValue)}\"");
                        break;
                    case FieldValueNull _:
                        sb.Append("null");
                        break;
                }

                isFirstField = false;
            }

            sb.Append("}");
            return sb.ToString();
        }
    }
}