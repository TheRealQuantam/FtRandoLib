using System;
using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

/// <summary>
/// IYamlTypeConverter that allows a number to be specified either as decimal or a hex value prefixed with either $ or 0x.
/// </summary>
public class YamlHexStringConverter : IYamlTypeConverter
{
    static readonly string[] HexPrefixes = ["$", "0x"];

    public YamlHexStringConverter()
    { }

    public bool Accepts(Type type)
        => false;

    public object? ReadYaml(
        IParser parser,
        Type type,
        ObjectDeserializer deserializer)
    {
        Scalar scalar = parser.Consume<Scalar>();
        string valStr = scalar.Value;
        bool isHex = false;

        foreach (string pre in HexPrefixes)
        {
            if (valStr.StartsWith(pre))
            {
                valStr = valStr.Substring(pre.Length);
                isHex = true;

                break;
            }
        }

        return int.Parse(valStr,
            isHex ? NumberStyles.HexNumber : NumberStyles.Integer);
    }

    public void WriteYaml(
        IEmitter emitter,
        object? value,
        Type type,
        ObjectSerializer serializer)
        => throw new NotImplementedException();
}

