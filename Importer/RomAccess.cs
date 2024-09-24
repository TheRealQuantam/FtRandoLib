using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

/// <summary>
/// A basic implementation of IRomAccess that is backed by a supplied IList of bytes and keeps track of the comments.
/// </summary>
public class SimpleRomAccess : IRomAccess
{
    IList<byte> _rom;
    string?[] _comments;

    public SimpleRomAccess(IList<byte> romData)
    {
        _rom = romData;
        _comments = new string?[romData.Count];
    }

    public byte[] Rom => _rom.ToArray();
    public IReadOnlyList<string?> Comments => _comments;

    public void Write(int offset, byte data, string comment = "")
    {
        _rom[offset] = data;
        _comments[offset] = comment;
    }

    public void Write(int offset, IReadOnlyList<byte> data, string comment = "")
    {
        Debug.Assert(checked(offset + data.Count) <= int.MaxValue);

        for (int i = 0; i < data.Count; i++)
            _rom[offset + i] = data[i];

        Array.Fill(_comments, comment, offset, data.Count);
    }
}
