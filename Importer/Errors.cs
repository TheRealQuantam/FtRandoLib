using FtRandoLib.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FtRandoLib.Importer;

public class InvalidUsage : Exception
{
    public string Usage { get; }
    public ISong Song { get; }
    public MusicInfo? Group { get; }

    public InvalidUsage(string usage, ISong song, MusicInfo? group = null)
        : base(CreateMessage(usage, song, group))
    {
        Usage = usage;
        Song = song;
        Group = group;
    }

    static string CreateMessage(
        string usage,
        ISong song,
        MusicInfo? group = null)
    {
        string grpPart = "";
        if (group is not null)
            grpPart = $" of group '{group.Title}'";

        return $"invalid usage '{usage}' in song '{song.Title}'{grpPart}";
    }
}

public class RomFullException : Exception { }

