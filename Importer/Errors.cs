using FtRandoLib.Library;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FtRandoLib.Importer;

public abstract class ImportError : Exception
{
    public ImportError(
        string? message = null, 
        Exception? innerException = null) 
        : base(message, innerException)
    {
    }
}

public class InvalidUsageError : ImportError
{
    public string Usage { get; }
    public ISong Song { get; }

    public InvalidUsageError(string usage, ISong song)
        : base($"invalid usage '{usage}' in song '{song.Title}'")
    {
        Usage = usage;
        Song = song;
    }
}

public class RomFullException : ImportError { }

