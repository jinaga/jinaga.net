using Jinaga.Identity;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Facts
{
    public class Fact
    {
        public static Fact Create(string type, ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            predecessors = predecessors
                .Where(predecessor => predecessor != null)
                .ToImmutableList();
            var reference = new FactReference(type, ComputeHash(fields, predecessors));
            return new Fact(reference, fields, predecessors);
        }

        /// <summary>
        /// A fact that carries the reference it is given rather than one computed from its
        /// contents.
        /// </summary>
        /// <remarks>
        /// Content addressing is how a fact gets its reference, and nothing a caller saves
        /// escapes it. A conformance vector does: its graph names a predecessor that
        /// addressing cannot reach, because the fact arrives later or the graph holds a
        /// cycle, and the vector states the references itself. A test that places such a
        /// graph needs facts carrying those references, which is the only reason this
        /// exists.
        /// </remarks>
        internal static Fact CreateByReference(FactReference reference, ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            return new Fact(reference, fields, predecessors);
        }

        private Fact(FactReference reference, ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            Reference = reference;
            Fields = fields;
            Predecessors = predecessors;
        }

        public FactReference Reference { get; }
        public ImmutableList<Field> Fields { get; }
        public ImmutableList<Predecessor> Predecessors { get; }

        public FieldValue GetFieldValue(string name)
        {
            var values = Fields
                .Where(f => f.Name == name)
                .Select(f => f.Value)
                .ToImmutableList();
            if (values.Count == 0)
            {
                return FieldValue.Undefined;
            }
            else if (values.Count > 1)
            {
                throw new ArgumentException($"The fact {Reference.Type} contained {values.Count} fields named {name}; there should only be 1");
            }
            else
            {
                return values.Single();
            }
        }

        public FactReference? GetPredecessorSingle(string role)
        {
            var references = Predecessors
                .Where(p => p.Role == role)
                .OfType<PredecessorSingle>()
                .Select(p => p.Reference)
                .ToImmutableList();
            if (references.Count == 0)
            {
                return null;
            }
            else if (references.Count > 1)
            {
                return null;
            }
            else
            {
                return references.Single();
            }
        }

        public ImmutableList<FactReference> GetPredecessorMultiple(string role)
        {
            var references = Predecessors
                .Where(p => p.Role == role)
                .OfType<PredecessorMultiple>()
                .SelectMany(p => p.References)
                .ToImmutableList();
            return references;
        }

        public ImmutableList<FactReference> GetPredecessors(string role)
        {
            var references = Predecessors
                .Where(p => p.Role == role)
                .ToImmutableList();
            if (references.Count == 0)
            {
                throw new ArgumentException($"The fact {Reference.Type} did not contain any predecessors in role {role}");
            }
            else if (references.Count > 1)
            {
                throw new ArgumentException($"The fact {Reference.Type} contained {references.Count} predecessors in role {role}; there should only be 1");
            }
            return references.Single() switch
            {
                PredecessorSingle single => ImmutableList<FactReference>.Empty.Add(single.Reference),
                PredecessorMultiple multiple => multiple.References,
                _ => throw new InvalidOperationException("Unknown predecessor type")
            };
        }

        public IEnumerable<FactReference> GetAllPredecessorReferences()
        {
            return Predecessors
                .Where(p => p != null)
                .SelectMany(p => p switch
                {
                    PredecessorSingle single => ImmutableList<FactReference>.Empty.Add(single.Reference),
                    PredecessorMultiple multiple => multiple.References,
                    _ => throw new InvalidOperationException("Unknown predecessor type")
                });
        }

        private static string ComputeHash(ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            string json = Canonicalize(fields, predecessors);
            return IdentityUtilities.ComputeStringHash(json);
        }

        public static string Canonicalize(ImmutableList<Field> fields, ImmutableList<Predecessor> predecessors)
        {
            string fieldsString = CanonicalizeFields(fields);
            string predecessorsString = CanonicalizePredecessors(predecessors);
            return $"{{\"fields\":{{{fieldsString}}},\"predecessors\":{{{predecessorsString}}}}}";
        }

        private static string CanonicalizeFields(ImmutableList<Field> fields)
        {
            var serializedFields = fields
                .OrderBy(field => field.Name, StringComparer.Ordinal)
                .Select(field => $"{CanonicalJson.SerializeString(field.Name)}:{SerializeFieldValue(field.Value)}")
                .ToArray();
            var result = String.Join(",", serializedFields);
            return result;
        }

        private static string SerializeFieldValue(FieldValue value)
        {
            switch (value)
            {
                case FieldValueString str:
                    // A nullable string property reaches here as a FieldValueString
                    // holding null, because the serializer cache cannot see the
                    // annotation. It canonicalizes as null, which is what it has
                    // always done and what JSON.stringify writes for it.
                    return str.StringValue == null
                        ? "null"
                        : CanonicalJson.SerializeString(str.StringValue);
                case FieldValueNumber number:
                    return CanonicalJson.SerializeNumber(number.DoubleValue);
                case FieldValueBoolean boolean:
                    return boolean.BoolValue ? "true" : "false";
                case FieldValueNull _:
                    return "null";
                default:
                    throw new ArgumentException($"Unknown field value type {value.GetType().Name}");
            }
        }

        private static string CanonicalizePredecessors(ImmutableList<Predecessor> predecessors)
        {
            var serializedPredecessors = predecessors
                .Where(predecessor => predecessor != null)
                .OrderBy(predecessor => predecessor.Role, StringComparer.Ordinal)
                .Select(predecessor => $"{CanonicalJson.SerializeString(predecessor.Role)}:{SerializePredecessor(predecessor)}")
                .ToArray();
            var result = String.Join(",", serializedPredecessors);
            return result;
        }

        private static string SerializePredecessor(Predecessor predecessor)
        {
            switch (predecessor)
            {
                case PredecessorSingle single:
                    return SerializeFactReference(single.Reference);
                case PredecessorMultiple multiple:
                    var referenceStrings = multiple
                        .References
                        .OrderBy(reference => reference.Hash, StringComparer.Ordinal)
                        .ThenBy(references => references.Type, StringComparer.Ordinal)
                        .Select(reference => SerializeFactReference(reference))
                        .ToArray();
                    return $"[{String.Join(",", referenceStrings)}]";
                default:
                    throw new NotImplementedException();
            }
        }

        private static string SerializeFactReference(FactReference reference)
        {
            string serializedType = CanonicalJson.SerializeString(reference.Type);
            return $"{{\"hash\":{CanonicalJson.SerializeString(reference.Hash)},\"type\":{serializedType}}}";
        }
    }
}