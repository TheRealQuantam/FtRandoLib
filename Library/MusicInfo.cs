using FtRandoLib.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace FtRandoLib.Library;

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

