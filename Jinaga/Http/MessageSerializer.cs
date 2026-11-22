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
            options.Converters.Add(new DistributionDecisionConverter());
            options.Converters.Add(new DistributionDenialCodeConverter());
            return options;
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
        /// both directions, so neither can drift from the other, and a name the table does
        /// not hold fails deserialization rather than becoming a default value.
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

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException($"Expected a string for {typeof(T).Name}.");
                }
                var name = reader.GetString() ?? "";
                if (!valuesByName.TryGetValue(name, out var value))
                {
                    throw new JsonException($"Unknown {typeof(T).Name} \"{name}\".");
                }
                return value;
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                if (!namesByValue.TryGetValue(value, out var name))
                {
                    throw new JsonException($"Unknown {typeof(T).Name} {value}.");
                }
                writer.WriteStringValue(name);
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
