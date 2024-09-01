using FtRandoLib.Utility;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FtRandoLib.Importer;

/// <summary>
/// Most of the data used internally by Importer while importing songs, as well as the necessary functions for handling different simple (single contiguous data block) music formats. More complex formats may be supported by a derived class.
/// </summary>
public abstract class ImportedModuleInfo
{
    /// <summary>
    /// The module to import.
    /// </summary>
    public readonly Module Module;

    /// <summary>
    /// The songs contained in the module that are of relevance to the Importer.
    /// </summary>
    public readonly RefSet<ISong> Songs;

    /// <summary>
    /// The bank the module has been assigned to, or -1 prior to placement.
    /// </summary>
    public int Bank = -1;

    /// <summary>
    /// The address the module will be imported to, or -1 prior to placement.
    /// </summary>
    public int Address = -1;

    /// <summary>
    /// Mapping of primary song map indices to module-specific song indices.
    /// </summary>
    public Dictionary<int, int> SongIndices = new();

    public ImportedModuleInfo(Module module)
    {
        Module = module;
        Songs = new();
    }

    /// <summary>
    /// Gets the primary song map entry for the specified song in the module.
    /// </summary>
    /// <param name="songMapIdx">The index in the primary song map that will be assigned to.</param>
    /// <param name="bankIdx">The bank entry for the song.</param>
    /// <param name="songIdx">The song index.</param>
    public virtual void GetSongMapEntry(
        int songMapIdx,
        out int bankIdx,
        out int songIdx)
    {
        Debug.Assert(SongIndices.ContainsKey(songMapIdx));

        bankIdx = Bank;
        songIdx = songMapIdx;
    }

    /// <summary>
    /// Gets the final form of the module, ready to be placed in the ROM.
    /// </summary>
    /// <param name="address">The target address where the module will be placed.</param>
    /// <param name="primarySquareChan">The primary square channel for the ROM. If necessary, the square channels of module songs will be swapped to conform to this.</param>
    /// <returns></returns>
    public abstract byte[] GetData(int address, int primarySquareChan);
}

/// <summary>
/// Utility class used by Importer for binary searching the list of modules to import. This list is ordered by module size, descending.
/// </summary>
public class ImportedModuleInfoSizeComparer : IComparer<ImportedModuleInfo>
{
    public int Size { get; set; }

    public ImportedModuleInfoSizeComparer(int size)
    {
        Size = size;
    }

    public int Compare(ImportedModuleInfo? a, ImportedModuleInfo? b)
    {
        if (a is not null)
        {
            Debug.Assert(b is null);
            return -a.Module.Data.Length.CompareTo(Size);
        }
        else
        {
            Debug.Assert(b is not null);
            return -Size.CompareTo(b.Module.Data.Length);
        }
    }
}

/// <summary>
/// The ImportedModuleInfo implementation for Dn-FamiTracker BIN format modules.
/// </summary>
public class ImportedFtModuleInfo : ImportedModuleInfo
{
    readonly int NumChannels;

    /// <summary>
    /// Create an instance.
    /// </summary>
    /// <param name="module">The module</param>
    /// <param name="numChannels">The number of channels in the module, as this is not explicitly exposed by the binary data.</param>
    public ImportedFtModuleInfo(
        Module module,
        int numChannels)
        : base(module)
    {
        Debug.Assert(module.IsEngine("ft"));

        NumChannels = numChannels;
    }

    public override void GetSongMapEntry(
        int songMapIdx, 
        out int bankIdx, 
        out int songIdx)
    {
        bankIdx = Bank ^ 0xff;
        songIdx = SongIndices[songMapIdx];
    }

    public override byte[] GetData(int address, int primarySquareChan)
    {
        byte[] buffer = Module.Data.ToArray();
        FtmBinary ftmBin = new(buffer, Module.Address, NumChannels);
        foreach (var song in Songs)
        {
            if (song.PrimarySquareChan != primarySquareChan)
                ftmBin.SwapSquareChans(song.Number);
        }

        if (address != Module.Address)
            ftmBin.Rebase(address);

        return buffer;
    }
}
