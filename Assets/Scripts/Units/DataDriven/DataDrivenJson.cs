// DataDrivenJson.cs
// Shared Newtonsoft settings for the data-driven unit/ability JSON files.
// Enums serialize as strings ("Fire", "AOE", ...) so the emitted files stay
// diffable and stable across enum reordering. Vector2 gets a compact {x, y}
// converter — Unity's default serialization would otherwise emit derived
// properties (magnitude/normalized, recursively) and bloat every file.
using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

public static class DataDrivenJson
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = { new StringEnumConverter(), new Vector2Converter() }
    };
}

public class Vector2Converter : JsonConverter<Vector2>
{
    public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(value.x);
        writer.WritePropertyName("y");
        writer.WriteValue(value.y);
        writer.WriteEndObject();
    }

    public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue,
                                     bool hasExistingValue, JsonSerializer serializer)
    {
        float x = 0f, y = 0f;
        if (reader.TokenType == JsonToken.StartObject)
        {
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName) continue;
                string prop = (string)reader.Value;
                if (prop == "x" && reader.Read()) x = Convert.ToSingle(reader.Value, CultureInfo.InvariantCulture);
                else if (prop == "y" && reader.Read()) y = Convert.ToSingle(reader.Value, CultureInfo.InvariantCulture);
                else reader.Skip();
            }
        }
        return new Vector2(x, y);
    }
}
