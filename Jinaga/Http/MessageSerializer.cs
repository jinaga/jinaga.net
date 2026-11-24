using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jinaga.Records;

namespace Jinaga.Http
{
    public static class MessageSerializer
    {
        private static readonly DistributionDecisionConverter decisionConverter = new DistributionDecisionConverter();
        private static readonly DistributionDenialCodeConverter denialCodeConverter = new DistributionDenialCodeConverter();
        private static JsonSerializerOptions options = GetOptions();

        private static JsonSerializerOptions GetOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            options.Converters.Add(new PredecessorSetConverter());
            options.Converters.Add(new FieldValueConverter());
            options.Converters.Add(decisionConverter);
            options.Converters.Add(denialCodeConverter);
            return options;
        }

        /// <summary>
        /// The <see cref="DistributionDenialCode"/> that the protocol spells with this name,
        /// or null when the name is null or names a code this client does not know. It reads
        /// the same table the converter reads, so a name stored as the replicator sent it and
        /// a code derived from that name cannot drift apart.
        /// </summary>
        internal static DistributionDenialCode? DenialCodeOfName(string? name)
        {
            return name == null ? null : denialCodeConverter.ValueOfName(name);
        }

        /// <summary>
        /// How the protocol spells this denial code, from the same table.
        /// </summary>
        internal static string NameOfDenialCode(DistributionDenialCode code)
        {
            return denialCodeConverter.NameOfValue(code);
        }

        /// <summary>
        /// The <see cref="DistributionDecision"/> that the protocol spells with this name,
        /// or null when the name is null or names a decision this client does not know. It
        /// reads the same table the converter reads, so a name stored as the replicator sent
        /// it and a decision derived from that name cannot drift apart.
        /// </summary>
        internal static DistributionDecision? DecisionOfName(string? name)
        {
            return name == null ? null : decisionConverter.ValueOfName(name);
        }

        /// <summary>
        /// How the protocol spells this decision, from the same table.
        /// </summary>
        internal static string NameOfDecision(DistributionDecision decision)
        {
            return decisionConverter.NameOfValue(decision);
        }

        public static string Serialize<TRequest>(TRequest request)
        {
            return JsonSerializer.Serialize(request, options);
        }

        public static TResponse Deserialize<TResponse>(string json)
        {
            return CheckedDeserialize<TResponse>(json);
        }

        /// <summary>
        /// Reads and writes an enum as the protocol spells it. One table of names serves
        /// both directions, so neither can drift from the other. A name the table does not
        /// hold fails deserialization rather than becoming a default value, as does a token
        /// that is not a string. Both hold for every converter.
        /// A protocol value a newer replicator may spell in a way this client has no name
        /// for therefore does not reach a converter at all: <see cref="FeedDecision.DecisionName"/>
        /// and <see cref="FeedDecision.CodeName"/> are read as the strings they are, and
        /// <see cref="FeedDecision.Decision"/> and <see cref="FeedDecision.Code"/> are
        /// derived from them through this same table, by <see cref="DecisionOfName"/> and
        /// <see cref="DenialCodeOfName"/>.
        /// </summary>
        private abstract class ProtocolEnumConverter<T> : JsonConverter<T>
            where T : struct, Enum
        {
            private readonly Dictionary<string, T> valuesByName;
            private readonly Dictionary<T, string> namesByValue;

            protected ProtocolEnumConverter(params KeyValuePair<T, string>[] names)
            {
                namesByValue = new Dictionary<T, string>();
                valuesByName = new Dictionary<string, T>();
                foreach (var pair in names)
                {
                    namesByValue.Add(pair.Key, pair.Value);
                    valuesByName.Add(pair.Value, pair.Key);
                }
            }

            protected static KeyValuePair<T, string> Name(T value, string name)
            {
                return new KeyValuePair<T, string>(value, name);
            }

            /// <summary>
            /// The value the protocol spells with this name, or null when the table holds
            /// no such name. Callers outside deserialization read the table through this.
            /// </summary>
            public T? ValueOfName(string name)
            {
                return valuesByName.TryGetValue(name, out var value) ? value : (T?)null;
            }

            /// <summary>
            /// How the protocol spells this value.
            /// </summary>
            public string NameOfValue(T value)
            {
                if (!namesByValue.TryGetValue(value, out var name))
                {
                    throw new JsonException($"Unknown {typeof(T).Name} {value}.");
                }
                return name;
            }

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException($"Expected a string for {typeof(T).Name}.");
                }
                var name = reader.GetString() ?? "";
                var value = ValueOfName(name);
                if (value == null)
                {
                    throw new JsonException($"Unknown {typeof(T).Name} \"{name}\".");
                }
                return value.Value;
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(NameOfValue(value));
            }
        }

        private class DistributionDecisionConverter : ProtocolEnumConverter<DistributionDecision>
        {
            public DistributionDecisionConverter() : base(
                Name(DistributionDecision.Authorized, "authorized"),
                Name(DistributionDecision.Reactive, "reactive"),
                Name(DistributionDecision.Denied, "denied"))
            {
            }
        }

        private class DistributionDenialCodeConverter : ProtocolEnumConverter<DistributionDenialCode>
        {
            public DistributionDenialCodeConverter() : base(
                Name(DistributionDenialCode.NoMatchingRule, "no-matching-rule"),
                Name(DistributionDenialCode.SpecMoreRestrictiveThanRule, "spec-more-restrictive-than-rule"),
                Name(DistributionDenialCode.PrincipalExcluded, "principal-excluded"),
                Name(DistributionDenialCode.NotAuthenticated, "not-authenticated"))
            {
            }
        }

        private class PredecessorSetConverter : JsonConverter<PredecessorSet>
        {
            public override PredecessorSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        var reference = CheckedDeserialize<FactReference>(ref reader, options);
                        return new PredecessorSetSingle
                        {
                            Reference = reference
                        };
                    case JsonTokenType.StartArray:
                        var references = CheckedDeserialize<List<FactReference>>(ref reader, options);
                        return new PredecessorSetMultiple
                        {
                            References = references
                        };
                    default:
                        throw new JsonException();
                }
            }

            public override void Write(Utf8JsonWriter writer, PredecessorSet value, JsonSerializerOptions options)
            {
                switch (value)
                {
                    case PredecessorSetSingle single:
                        JsonSerializer.Serialize(writer, single.Reference, options);
                        break;
                    case PredecessorSetMultiple multiple:
                        JsonSerializer.Serialize(writer, multiple.References, options);
                        break;
                    default:
                        throw new JsonException();
                }
            }
        }

        private class FieldValueConverter : JsonConverter<FieldValue>
        {
            public override FieldValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        return FieldValue.From(reader.GetString() ?? "");
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                        return FieldValue.From(reader.GetBoolean());
                    case JsonTokenType.Number:
                        return FieldValue.From(reader.GetDouble());
                    default:
                        throw new JsonException();
                }
            }

            public override void Write(Utf8JsonWriter writer, FieldValue value, JsonSerializerOptions options)
            {
                switch (value)
                {
                    case FieldValueString stringValue:
                        JsonSerializer.Serialize(writer, stringValue.Value, options);
                        break;
                    case FieldValueBoolean booleanValue:
                        JsonSerializer.Serialize(writer, booleanValue.Value, options);
                        break;
                    case FieldValueNumber numberValue:
                        JsonSerializer.Serialize(writer, numberValue.Value, options);
                        break;
                    case FieldValueNull _:
                        writer.WriteNullValue();
                        break;
                    default:
                        throw new JsonException();
                }
            }
        }

        private static T CheckedDeserialize<T>(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var response = JsonSerializer.Deserialize<T>(ref reader, options);
            if (response == null)
            {
                throw new ArgumentException($"Object is not a {nameof(T)}");
            }
            return response;
        }

        private static T CheckedDeserialize<T>(string json)
        {
            var response = JsonSerializer.Deserialize<T>(json, options);
            if (response == null)
            {
                throw new ArgumentException($"Object is not a {nameof(T)}: {json}");
            }
            return response;
        }
    }
}
