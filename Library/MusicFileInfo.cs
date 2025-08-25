using FtRandoLib.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using YamlDotNet.Serialization;

namespace FtRandoLib.Library;

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

