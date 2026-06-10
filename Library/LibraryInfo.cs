using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace FtRandoLib.Library;

/// <summary>
/// An entire music library.
/// </summary>
/// <typeparam name="TItem">The file type of the library.</typeparam>
/// <typeparam name="TGroup">The file group type of the library.</typeparam>
[JsonObject]
[YamlSerializable]
public class LibraryInfo<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TGroup
>
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
    public static LibraryInfo<TItem, TGroup> Parse(
        string data,
        Type type,
        bool ignoreExtraFields = false)
    {
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
    public static TLibrary Parse<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TLibrary>(
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
    public static LibraryInfo<TItem, TGroup> ParseJson(
        string jsonData,
        Type type,
        bool ignoreExtraFields = false)
    {
        return ParseJson<LibraryInfo<TItem, TGroup>>(jsonData,
            (j, s) => JsonConvert.DeserializeObject(j, type, s) as LibraryInfo<TItem, TGroup>,
            ignoreExtraFields);
    }

    /// <summary>
    /// Parse a JSON file into a LibraryInfo. Should be used when loading libraries to ensure that errors are properly translated into ParsingErrors.
    /// </summary>
    /// <typeparam name="TLibrary">The type to construct. Must be the class through which Parse is called or a subclass of it.</typeparam>
    /// <param name="jsonData">The JSON data to parse.</param>
    /// <param name="ignoreExtraFields">Whether to ignore fields that are not defined in the class. Defaults to false: throw an error if extra fields are present.</param>
    /// <returns></returns>
    public static TLibrary ParseJson<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TLibrary>(
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
    public static LibraryInfo<TItem, TGroup> ParseYaml(
        string yamlData,
        Type type,
        bool ignoreExtraFields = false)
    {
        var builder = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new YamlHexStringConverter())
            .WithNodeDeserializer(
                i => new ValidatingYamlNodeDeserializer(i),
                s => s.InsteadOf<ObjectNodeDeserializer>());

        if (ignoreExtraFields)
            builder = builder.IgnoreUnmatchedProperties();

        var deserializer = builder.Build();
        return DeserializeYaml<LibraryInfo<TItem, TGroup>>(yamlData,
            d => deserializer.Deserialize(d, type) as LibraryInfo<TItem, TGroup>);
    }

    /// <summary>
    /// Parse a YAML file into a LibraryInfo. Should be used when loading libraries to ensure that errors are properly translated into ParsingErrors. This version of ParseYaml is not trimming safe.
    /// </summary>
    /// <typeparam name="TLibrary">The type to construct. Must be the class through which Parse is called or a subclass of it.</typeparam>
    /// <param name="yamlData">The YAML data to parse.</param>
    /// <param name="ignoreExtraFields">Whether to ignore fields that are not defined in the class. Defaults to false: throw an error if extra fields are present.</param>
    /// <returns></returns>
    public static TLibrary ParseYaml<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TLibrary>(
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
    public static TLibrary ParseYaml<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TLibrary, 
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TContext
    >(
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

    static TLibrary ParseJson<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TLibrary>(
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

    static TLibrary DeserializeYaml<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TLibrary>(
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


