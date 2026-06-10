using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;

namespace FtRandoLib.Library;

/// <summary>
/// A group of songs/files in the library.
/// </summary>
/// <typeparam name="TItem">The type of object in the group.</typeparam>
[JsonObject]
[YamlSerializable]
public class GroupInfo<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem> : MusicInfo
    where TItem : MusicFileInfo
{
    [JsonProperty("items")]
    public List<TItem> Items { get; set; } = new();
}

