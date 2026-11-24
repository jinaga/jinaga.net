using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jinaga.Http
{
    /// <summary>
    /// Reads a protocol member that the wire spells as a string. An unknown value is
    /// tolerated, which is why these members are carried as strings rather than as enums,
    /// but a shape that is not a string is refused, JSON null included. A null is a shape
    /// rather than a value this client has yet to learn: nothing a newer replicator decides
    /// makes it meaningful, and tolerating it would put a null in a member declared not to
    /// hold one, to fail somewhere else.
    /// Applied per property, so it governs the members of a protocol message and no other
    /// string in the serializer.
    /// </summary>
    internal class ProtocolStringConverter : JsonConverter<string>
    {
        public override bool HandleNull => true;

        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected a string for a protocol member, not {reader.TokenType}.");
            }
            return reader.GetString() ?? "";
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    /// <summary>
    /// Reads a protocol member that the wire spells as an array, refusing any other shape,
    /// JSON null included, for the reason <see cref="ProtocolStringConverter"/> gives. The
    /// elements are read the way the serializer reads them anywhere else, so this adds the
    /// shape check and nothing more.
    /// </summary>
    internal class ProtocolListConverter<T> : JsonConverter<List<T>>
    {
        public override bool HandleNull => true;

        public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Expected an array for a protocol member, not {reader.TokenType}.");
            }
            return JsonSerializer.Deserialize<List<T>>(ref reader, options)
                ?? throw new JsonException("Expected an array for a protocol member, not null.");
        }

        public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
