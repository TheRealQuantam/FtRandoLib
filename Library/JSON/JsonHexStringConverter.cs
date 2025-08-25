using Newtonsoft.Json;
using System;
using System.Diagnostics;

namespace FtRandoLib.Library;

/// <summary>
/// JsonConverter that allows a number to be specified either as a decimal literal or a string containing a hex number.
/// </summary>
public class JsonHexStringConverter : JsonConverter
{
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        => throw new NotImplementedException();

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        Debug.Assert(reader.Value is not null);

        if (reader.TokenType == JsonToken.Integer)
            // It's actually a boxed long
            return checked((int)(long)reader.Value);
        else if (reader.TokenType == JsonToken.String)
            return Convert.ToInt32((string)reader.Value, 16);

        throw new JsonReaderException("invalid hex value", reader.Path, -1, -1, null);
    }

    public override bool CanConvert(Type objectType)
        => throw new NotImplementedException();
}