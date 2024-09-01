using System;
using System.Collections.Generic;

namespace FtRandoLib.Importer;

/// <summary>
/// Interface to read and write the ROM being modified.
/// </summary>
public interface IRomAccess
{
    /// <summary>
    /// Get a copy of the current ROM with all changes made thus far. It is valid for the implementation to not support reading from the ROM, in which case Rom must return NotImplementedException; this will disable some features such as builtin tracks.
    /// </summary>
    byte[] Rom { get; }

    /// <summary>
    /// Write a single byte to the ROM, with an optional comment for debugging.
    /// </summary>
    void Write(int offset, byte data, string comment = "");

    /// <summary>
    /// Write a block to the ROM, with an optional comment for debugging.
    /// </summary>
    void Write(int offset, IReadOnlyList<byte> data, string comment = "");
}

public interface IShuffler
{
    IList<T> Shuffle<T>(IReadOnlyList<T> items);
}
