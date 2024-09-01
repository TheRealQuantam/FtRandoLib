using FtRandoLib.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.IO.Compression;

namespace FtRandoLib.Library;

/// <summary>
/// JsonConverter that allows a number to be specified either as a decimal literal or a string containing a hex number.
/// </summary>
public class JsonHexStringConverter : JsonConverter
{
    public override bool CanWrite { get { return false; } }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

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
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// An object in the library hierarchy that contains information that will directly or indirectly be applied to songs. This may be a song, a file (block of data that contains songs), or a group.
/// </summary>
public abstract class MusicInfo
{
    /*[JsonIgnore]
    public const bool EnabledDefault = true;
    [JsonIgnore]
    public const bool StreamingSafeDefault = true;*/

    [JsonProperty("enabled")]
    public bool? Enabled { get; set; } = null;

    [JsonProperty("title", Required = Required.Always)]
    public string Title { get; set; } = "";

    [JsonProperty("author")]
    public string? Author { get; set; } = null;

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
    public IstringSet Uses = new();
}

/// <summary>
/// A file object that represents data that contains one or more songs, e.g. a FamiTracker module.
/// </summary>
public abstract class MusicFileInfo : MusicInfo
{
    /// <summary>
    /// The logical address of the start of the file data. This is used in rebasing the data to be placed in a different location in memory.
    /// </summary>
    [JsonProperty("start_addr")]
    [JsonConverter(typeof(JsonHexStringConverter))]
    public int? StartAddr { get; set; } = null;

    /// <summary>
    /// The data in the form provided by the library file. The base implementation encodes this data in base64 (.NET dialect), optionally compressed via deflate (.NET dialect) if the data begins with "deflate:".
    /// </summary>
    [JsonProperty("data", Required = Required.Always)]
    public string Data
    {
        get { return data; }
        set
        {
            data = value;
            UncompressData();
        }
    }

    /// <summary>
    /// The decoded and decompressed data.
    /// </summary>
    [JsonIgnore]
    public byte[] UncompressedData { get; private set; } = new byte[0];

    [JsonIgnore]
    public int Size { get { return UncompressedData.Length; } }

    private string data = "";

    /// <summary>
    /// Decodes and (if necessary) decompresses the data.
    /// </summary>
    protected void UncompressData()
    {
        const string deflateHdr = "deflate:";
        if (Data.StartsWith(deflateHdr))
        {
            var data = Convert.FromBase64String(Data.Substring(deflateHdr.Length));
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
        else
            UncompressedData = Convert.FromBase64String(Data);
    }
}

/// <summary>
/// A FamiTracker song in the library.
/// </summary>
[JsonObject]
public class FtSongInfo : MusicInfo
{
    /// <summary>
    /// The 0-based index of the song in the containing module.
    /// </summary>
    [JsonProperty("number", Required = Required.Always)]
    public int Number { get; set; } = 0;
}

/// <summary>
/// A FamiTracker module in the library.
/// </summary>
[JsonObject]
public class FtModuleInfo : MusicFileInfo
{
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
public class GroupInfo<TItem> : MusicInfo 
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
public class LibraryInfo<TItem, TGroup> 
    where TItem : MusicFileInfo 
    where TGroup : GroupInfo<TItem>
{
    [JsonProperty("single")]
    public List<TItem> Single { get; set; } = new();

    [JsonProperty("groups")]
    public List<TGroup> Groups { get; set; } = new();
}

public sealed class FtModuleGroupInfo : GroupInfo<FtModuleInfo> { }
public sealed class FtLibraryInfo : LibraryInfo<FtModuleInfo, FtModuleGroupInfo> { }
