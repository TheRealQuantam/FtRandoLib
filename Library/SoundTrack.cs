using FtRandoLib.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

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

/// <summary>
/// An object in the library hierarchy that contains information that will directly or indirectly be applied to songs. This may be a song, a file (block of data that contains songs), or a group.
/// </summary>
public abstract class MusicInfo
{
    [JsonProperty("enabled")]
    public bool? Enabled { get; set; } = null;

    [JsonProperty("title", Required = Required.Always)]
    [Required]
    public string Title { get; set; } = "";

    [JsonProperty("author")]
    public string? Author { get; set; } = null;

    /// <summary>
    /// Miscellaneous string tags of mostly application-specific uses. Tags with no or a '+' prefix are added to the tags of their parents, while tags with a '-' prefix are removed.
    /// </summary>
    [JsonProperty("tags")]
    public IstringSet Tags { get; set; } = new();

    /// <summary>
    /// Whether or not the item is likely to be caught by stream scanners and have negative implications for the stream.
    /// </summary>
    [JsonProperty("streaming_safe")]
    public bool? StreamingSafe { get; set; } = null;

    /// <summary>
    /// The index of the most important square channel that sound effects should least interfere with. E.g. square 0 for Capcom games or square 1 for Nintendo games.
    /// </summary>
    [JsonProperty("primary_square_chan")]
    public int? PrimarySquareChan { get; set; } = null;

    /// <summary>
    /// The uses in the randomizer this song may be selected for.
    /// </summary>
    [JsonProperty("uses")]
    public IstringSet Uses { get; set; } = new();

    public override string ToString() => $"{GetType().Name} : \"{Title}\"";
}

/// <summary>
/// A file object that represents data that contains one or more songs, e.g. a FamiTracker module.
/// </summary>
public abstract class MusicFileInfo : MusicInfo
{
    public static IReadOnlySet<string> JsonExtensions() 
        => new IstringSet([".json", ".jsonc", ".cjson", ".json5"]);
    public static IReadOnlySet<string> YamlExtensions() 
        => new IstringSet([".yaml", ".yml"]);

    /// <summary>
    /// The logical address of the start of the file data. This is used in rebasing the data to be placed in a different location in memory.
    /// </summary>
    [JsonProperty("start_addr")]
    [JsonConverter(typeof(JsonHexStringConverter))]
    [YamlConverter(typeof(YamlHexStringConverter))]
    public int? StartAddr { get; set; } = null;

    /// <summary>
    /// The data in the form provided by the library file. The base implementation encodes this data in base64 (.NET dialect), optionally compressed via deflate (.NET dialect) if the data begins with "deflate:".
    /// </summary>
    [JsonProperty("data", Required = Required.Always)]
    [Required]
    public string Data
    {
        get { return data; }
        set
        {
            UncompressData(value);
            data = value;
        }
    }

    /// <summary>
    /// The decoded and decompressed data.
    /// </summary>
    [JsonIgnore]
    [YamlIgnore]
    [MinLength(1, ErrorMessage = "The Data field is required")]
    public byte[] UncompressedData { get; private set; } = Array.Empty<byte>();

    [JsonIgnore]
    [YamlIgnore]
    public int Size { get { return UncompressedData.Length; } }

    private string data = "";

    [DynamicDependency(DynamicallyAccessedMemberTypes.All | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(JsonHexStringConverter))]
    public MusicFileInfo()
    { }

    /// <summary>
    /// Decodes and (if necessary) decompresses the data.
    /// </summary>
    protected void UncompressData(string rawData)
    {
        const string deflateHdr = "deflate:",
            hexHdr = "hex:";
        if (rawData.StartsWith(deflateHdr))
        {
            var data = Convert.FromBase64String(rawData.Substring(deflateHdr.Length));
            using (var outStream = new MemoryStream())
            {
                using (var memStream = new MemoryStream(data))
                {
                    using (var cmpStream = new DeflateStream(memStream, CompressionMode.Decompress))
                        cmpStream.CopyTo(outStream);
                }

                UncompressedData = outStream.ToArray();
            }
        }
        else if (rawData.StartsWith(hexHdr))
            UncompressedData = Convert.FromHexString(rawData.Substring(hexHdr.Length));
        else
            UncompressedData = Convert.FromBase64String(rawData);
    }
}

/// <summary>
/// A FamiTracker song in the library.
/// </summary>
[JsonObject]
[YamlSerializable]
public class FtSongInfo : MusicInfo
{
    /// <summary>
    /// The 0-based index of the song in the containing module.
    /// </summary>
    [JsonProperty("number", Required = Required.Always)]
    [Range(0, int.MaxValue, ErrorMessage = "The " + nameof(Number) + " field is required")]
    public int Number { get; set; } = -1;
}

/// <summary>
/// A FamiTracker module in the library.
/// </summary>
[JsonObject]
[YamlSerializable]
public class FtModuleInfo : MusicFileInfo
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(FtSongInfo))]
    public FtModuleInfo()
    { }

    /// <summary>
    /// The list of songs in the module that may be accessed by FtRandoLib. Modules that contain 1 song typically do not have explicit song entries.
    /// </summary>
    [JsonProperty("songs")]
    public List<FtSongInfo> Songs { get; set; } = new();
}

/// <summary>
/// A group of songs/files in the library.
/// </summary>
/// <typeparam name="TItem">The type of object in the group.</typeparam>
[JsonObject]
[YamlSerializable]
public class GroupInfo<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TItem> : MusicInfo 
    where TItem : MusicFileInfo
{
    [JsonProperty("items")]
    public List<TItem> Items { get; set; } = new();
}

/// <summary>
/// An entire music library.
/// </summary>
/// <typeparam name="TItem">The file type of the library.</typeparam>
/// <typeparam name="TGroup">The file group type of the library.</typeparam>
[JsonObject]
[YamlSerializable]
public class LibraryInfo<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TItem, 
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TGroup> 
    where TItem : MusicFileInfo 
    where TGroup : GroupInfo<TItem>
{
    [JsonProperty("single")]
    public List<TItem> Single { get; set; } = new();

    [JsonProperty("groups")]
    public List<TGroup> Groups { get; set; } = new();

    /// <summary>
    /// Parse a JSON or YAML file into a LibraryInfo. It is preferable to use ParseJson or ParseYaml as Parse is not 100% guaranteed to correctly detect the file format.
    /// </summary>
    /// <param name="data">The JSON or YAML data to parse.</param>
    /// <param name="type">The type to construct. Must be the class through which Parse is called or a subclass of it.</param>
    /// <param name="ignoreExtraFields">Whether to ignore fields that are not defined in the class. Defaults to false: throw an error if extra fields are present.</param>
    public static object Parse(
        string data,
        Type type,
        bool ignoreExtraFields = false)
    {
        Debug.Assert(type.IsAssignableTo(typeof(LibraryInfo<TItem, TGroup>)));

        if (IsFirstCharJson(data.First(ch => char.IsWhiteSpace(ch))))
            return ParseJson(data, type, ignoreExtraFields);
        else
            return ParseYaml(data, type, ignoreExtraFields);
    }

    /// <summary>
    /// Parse a JSON or YAML file into a LibraryInfo. It is preferable to use ParseJson or ParseYaml as Parse is not 100% guaranteed to correctly detect the file format.
    /// </summary>
    /// <typeparam name="TLibrary">The type to construct. Must be the class through which Parse is called or a subclass of it.</typeparam>
    /// <param name="data">The JSON or YAML data to parse.</param>
    /// <param name="ignoreExtraFields">Whether to ignore fields that are not defined in the class. Defaults to false: throw an error if extra fields are present.</param>
    /// <returns></returns>
    public static TLibrary Parse<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TLibrary>(
        string data,
        bool ignoreExtraFields = false)
        where TLibrary : LibraryInfo<TItem, TGroup>
    {
        if (IsFirstCharJson(data.First(ch => char.IsWhiteSpace(ch))))
            return ParseJson<TLibrary>(data, ignoreExtraFields);
        else
            return ParseYaml<TLibrary>(data, ignoreExtraFields);
    }

    /// <summary>
    /// Parse a JSON file into a LibraryInfo. Should be used when loading libraries to ensure that errors are properly translated into ParsingErrors.
    /// </summary>
    /// <param name="jsonData">The JSON data to parse.</param>
    /// <param name="type">The type to construct. Must be the class through which Parse is called or a subclass of it.</param>
    /// <param name="ignoreExtraFields">Whether to ignore fields that are not defined in the class. Defaults to false: throw an error if extra fields are present.</param>
    public static object ParseJson(
        string jsonData,
        Type type,
        bool ignoreExtraFields = false)
    {
        Debug.Assert(type.IsAssignableTo(typeof(LibraryInfo<TItem, TGroup>)));

        return ParseJson<object>(jsonData,
            (j, s) => JsonConvert.DeserializeObject(j, type, s),
            ignoreExtraFields);
    }

    /// <summary>
    /// Parse a JSON file into a LibraryInfo. Should be used when loading libraries to ensure that errors are properly translated into ParsingErrors.
    /// </summary>
    /// <typeparam name="TLibrary">The type to construct. Must be the class through which Parse is called or a subclass of it.</typeparam>
    /// <param name="jsonData">The JSON data to parse.</param>
    /// <param name="ignoreExtraFields">Whether to ignore fields that are not defined in the class. Defaults to false: throw an error if extra fields are present.</param>
    /// <returns></returns>
    public static TLibrary ParseJson<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TLibrary>(
        string jsonData,
        bool ignoreExtraFields = false)
        where TLibrary : LibraryInfo<TItem, TGroup>
        => ParseJson<TLibrary>(jsonData,
            (j, s) => JsonConvert.DeserializeObject<TLibrary>(j, s),
            ignoreExtraFields);

    /// <summary>
    /// Parse a YAML file into a LibraryInfo. Should be used when loading libraries to ensure that errors are properly translated into ParsingErrors.
    /// </summary>
    /// <param name="yamlData">The YAML data to parse.</param>
    /// <param name="type">The type to construct. Must be the class through which Parse is called or a subclass of it.</param>
    /// <param name="ignoreExtraFields">Whether to ignore fields that are not defined in the class. Defaults to false: throw an error if extra fields are present.</param>
    public static object ParseYaml(
        string yamlData,
        Type type,
        bool ignoreExtraFields = false)
    {
        Debug.Assert(type.IsAssignableTo(typeof(LibraryInfo<TItem, TGroup>)));

        var builder = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new YamlHexStringConverter())
            .WithNodeDeserializer(
                i => new ValidatingYamlNodeDeserializer(i), 
                s => s.InsteadOf<ObjectNodeDeserializer>());

        if (ignoreExtraFields)
            builder = builder.IgnoreUnmatchedProperties();

        var deserializer = builder.Build();
        return DeserializeYaml<object>(yamlData,
            d => deserializer.Deserialize(d, type));
    }

    /// <summary>
    /// Parse a YAML file into a LibraryInfo. Should be used when loading libraries to ensure that errors are properly translated into ParsingErrors. This version of ParseYaml is not trimming safe.
    /// </summary>
    /// <typeparam name="TLibrary">The type to construct. Must be the class through which Parse is called or a subclass of it.</typeparam>
    /// <param name="yamlData">The YAML data to parse.</param>
    /// <param name="ignoreExtraFields">Whether to ignore fields that are not defined in the class. Defaults to false: throw an error if extra fields are present.</param>
    /// <returns></returns>
    public static TLibrary ParseYaml<TLibrary>(
        string yamlData,
        bool ignoreExtraFields = false)
        where TLibrary : LibraryInfo<TItem, TGroup>
        => (TLibrary)ParseYaml(yamlData, typeof(TLibrary), ignoreExtraFields);

    /// <summary>
    /// Parse a YAML file into a LibraryInfo. Should be used when loading libraries to ensure that errors are properly translated into ParsingErrors.
    /// </summary>
    /// <typeparam name="TLibrary">The type to construct. Must be the class through which Parse is called or a subclass of it.</typeparam>
    /// <typeparam name="TContext">A YamlDotNet StaticContext capable of parsing a TLibrary.</typeparam>
    /// <param name="yamlData">The YAML data to parse.</param>
    /// <param name="ignoreExtraFields">Whether to ignore fields that are not defined in the class. Defaults to false: throw an error if extra fields are present.</param>
    /// <returns></returns>
    public static TLibrary ParseYaml<TLibrary, TContext>(
        string yamlData,
        bool ignoreExtraFields = false)
        where TLibrary : LibraryInfo<TItem, TGroup>
        where TContext : StaticContext, new()
    {
        var builder = new StaticDeserializerBuilder(new TContext())
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new YamlHexStringConverter())
            .WithNodeDeserializer(
                i => new ValidatingYamlNodeDeserializer(i),
                s => s.InsteadOf<ObjectNodeDeserializer>());

        if (ignoreExtraFields)
            builder = builder.IgnoreUnmatchedProperties();

        var deserializer = builder.Build();
        return DeserializeYaml<TLibrary>(yamlData,
            d => deserializer.Deserialize<TLibrary>(d));
    }

    static bool IsFirstCharJson(char ch)
        => ch == '[' || ch == '{';

    static TLibrary ParseJson<TLibrary>(
        string jsonData,
        Func<string, JsonSerializerSettings, TLibrary?> ParsePrimitive,
        bool ignoreExtraFields = false)
    {
        ParsingError? parsingError = null;
        JsonSerializerSettings settings = new()
        {
            Error = (sender, args) =>
            {
                parsingError = new ParsingError(
                    typeof(TItem), typeof(TGroup), sender, args);
            }
        };

        if (!ignoreExtraFields)
            settings.MissingMemberHandling = MissingMemberHandling.Error;

        try
        {
            var libObj = ParsePrimitive(jsonData, settings);
            Debug.Assert(libObj is not null); ////

            return libObj;
        }
        catch
        {
            if (parsingError is not null)
                throw parsingError;
            else
                throw;
        }
    }

    static TLibrary DeserializeYaml<TLibrary>(
        string yamlData,
        Func<string, TLibrary?> ParsePrimitive)
    {
        try
        {
            var value = ParsePrimitive(yamlData);
            Debug.Assert(value is not null); ////

            return value;
        }
        catch (YamlException e)
        {
            if (e.InnerException is BaseParsingError parseErr)
                throw new ParsingError(
                    parseErr, typeof(TItem), typeof(TGroup));
            else
                ParsingError.Throw<TItem, TGroup>(e);

            // Shut up compiler
            throw new UnreachableException();
        }
    }
}

[JsonObject]
[YamlSerializable]
public sealed class FtModuleGroupInfo : GroupInfo<FtModuleInfo> 
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(FtModuleInfo))]
    public FtModuleGroupInfo()
    { }
}

[JsonObject]
[YamlSerializable]
public sealed class FtLibraryInfo : LibraryInfo<FtModuleInfo, FtModuleGroupInfo> 
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(FtModuleGroupInfo))]
    public FtLibraryInfo()
    { }
}
