using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;

namespace FtRandoLib.Library;

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
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FtSongInfo))]
    public FtModuleInfo()
    { }

    /// <summary>
    /// The list of songs in the module that may be accessed by FtRandoLib. Modules that contain 1 song typically do not have explicit song entries.
    /// </summary>
    [JsonProperty("songs")]
    public List<FtSongInfo> Songs { get; set; } = new();
}

[JsonObject]
[YamlSerializable]
public sealed class FtModuleGroupInfo : GroupInfo<FtModuleInfo> 
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FtModuleInfo))]
    public FtModuleGroupInfo()
    { }
}

[JsonObject]
[YamlSerializable]
public sealed class FtLibraryInfo : LibraryInfo<FtModuleInfo, FtModuleGroupInfo> 
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FtModuleGroupInfo))]
    public FtLibraryInfo()
    { }
}
