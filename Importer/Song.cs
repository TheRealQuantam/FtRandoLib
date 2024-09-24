using FtRandoLib.Library;
using FtRandoLib.Utility;
using System;

namespace FtRandoLib.Importer;

/// <summary>
/// A song is a single logical track of music which may be assigned to play in a specific case in game.
/// </summary>
public interface ISong
{
    string Engine { get; }
    bool IsEngine(string eng);

    /// <summary>
    /// The index of the song in its containing module, or 0 for songs that are not contained in multi-song modules.
    /// </summary>
    int Number { get; }

    string Title { get; }
    string? Author { get; }

    bool Enabled { get; }
    IstringSet Uses { get; }

    int PrimarySquareChan { get; }
    bool StreamingSafe { get; }

    /// <summary>
    /// The containing module, or null only when the song is builtin.
    /// </summary>
    Module? Module { get; }
}

public abstract class SongBase : ISong
{
    public string Engine { get; protected set; }
    public bool IsEngine(string eng) => StringComparer.InvariantCultureIgnoreCase.Equals(Engine, eng);

    public int Number { get; protected set; }
    public string Title { get; protected set; }
    public string? Author { get; protected set; }

    public bool Enabled { get; protected set; }
    public IstringSet Uses { get; protected set; }

    public int PrimarySquareChan { get; protected set; }
    public bool StreamingSafe { get; protected set; }

    public Module? Module { get; protected set; }

    /// <summary>
    /// Create a song object with explicit values.
    /// </summary>
    protected SongBase(
        string Engine = "",
        int Number = -1,
        string Title = "",
        string? Author = null,
        bool Enabled = false,
        IstringSet? Uses = null,
        int PrimarySquareChan = 0,
        bool StreamingSafe = false,
        Module? Module = null)
    {
        this.Engine = Engine;
        this.Number = Number;
        this.Title = Title;
        this.Author = Author;
        this.Enabled = Enabled;
        this.Uses = Uses ?? new();
        this.PrimarySquareChan = PrimarySquareChan;
        this.StreamingSafe = StreamingSafe;
        this.Module = Module;
    }

    /// <summary>
    /// Create a song object based on a library hierarchy, handling inherited values appropriately. songInfo (if present) takes precedence over modInfo, which takes precedence over grpInfo (if present), which takes precedence over the default values supplied as parameters.
    /// </summary>
    protected SongBase(
        string Engine,
        MusicInfo? grpInfo,
        MusicFileInfo modInfo,
        MusicInfo? songInfo,
        IstringSet defaultUses,
        int Number = 0,
        Module? Module = null,
        bool enabledDefault = true,
        int defaultPrimarySquareChan = 0,
        bool defaultStreamingSafe = true)
    {
        this.Engine = Engine;
        this.Number = Number;
        this.Module = Module;

        MusicInfo grpFac = grpInfo ?? modInfo,
            songFac = songInfo ?? modInfo;

        Title = grpInfo is not null ? $"{grpInfo.Title} - " : "";
        Title = Title + modInfo.Title;
        Author = songFac.Author ?? modInfo.Author ?? grpFac.Author;

        Enabled = songFac.Enabled
            ?? modInfo.Enabled
            ?? grpFac.Enabled
            ?? enabledDefault;

        StreamingSafe = songFac.StreamingSafe
            ?? modInfo.StreamingSafe
            ?? grpFac.StreamingSafe
            ?? defaultStreamingSafe;
        PrimarySquareChan = songFac.PrimarySquareChan
            ?? modInfo.PrimarySquareChan
            ?? grpFac.PrimarySquareChan
            ?? defaultPrimarySquareChan;

        Uses = songFac.Uses.Count > 0 ? songFac.Uses
            : modInfo.Uses.Count > 0 ? modInfo.Uses
            : defaultUses;
    }

    public override string ToString() => $"[{GetType().Name} : \"{Title}\"]";
}

/// <summary>
/// A song that is built into the ROM and has no data to import.
/// </summary>
public class BuiltinSong : SongBase
{
    public BuiltinSong(
        int Number,
        string Title,
        string? Author = null,
        bool Enabled = true,
        IstringSet? Uses = null,
        int PrimarySquareChan = 0,
        bool StreamingSafe = true)
        : base(
              "native",
              Number,
              Title,
              Author,
              Enabled,
              Uses,
              PrimarySquareChan,
              StreamingSafe: StreamingSafe)
    {
    }
}

/// <summary>
/// A song in a FamiTracker module.
/// </summary>
public class FtSong : SongBase
{
    public readonly FtModuleInfo ModuleInfo;
    public readonly FtSongInfo? Info;

    public FtSong(
        FtModuleGroupInfo? grpInfo,
        FtModuleInfo modInfo,
        int songIdx,
        Module module,
        int defaultPrimarySquareChan,
        bool defaultStreamingSafe,
        IstringSet defaultUses)
        : base(
              "ft",
              grpInfo,
              modInfo,
              modInfo.Songs.Count > 0 ? modInfo.Songs[songIdx] : null,
              defaultUses,
              songIdx,
              module,
              true,
              defaultPrimarySquareChan,
              defaultStreamingSafe)
    {
        MusicInfo grpFac = grpInfo is not null ? grpInfo : modInfo,
            songFac = Info is not null ? Info : modInfo;

        ModuleInfo = modInfo;
        Info = modInfo.Songs.Count > 0 ? modInfo.Songs[songIdx] : null;

        if (Info is not null)
        {
            Number = Info.Number;
            Title = Title + $" - {Info.Title}";
        }
    }
}
