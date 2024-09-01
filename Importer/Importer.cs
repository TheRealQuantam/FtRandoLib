using FtRandoLib.Library;
using FtRandoLib.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FtRandoLib.Importer;

public class RomFullException : Exception { }

public class LibraryParserOptions
{
    public bool EnabledOnly = true;
    public bool SafeOnly = false;
    public bool IgnoreExtraFields = false;
}

public record IntRange(int Start, int End);
public record BankRange(int Bank, int Start, int End);
public record BankData(BankLayout Layout, byte[] Data);

public class BankLayout
{
    public readonly int BankBaseAddr;
    public readonly int BankSize;
    public readonly IReadOnlyList<IntRange> FreeRanges;

    public readonly int? SourceBank;
    public readonly IReadOnlyList<IntRange>? CopyRanges;

    public BankLayout(
        int bankBaseAddr,
        int bankSize,
        IReadOnlyList<IntRange>? freeRanges = null,
        int? sourceBank = null)
    {
        BankBaseAddr = bankBaseAddr;
        BankSize = bankSize;

        CopyRanges = null;
        if (freeRanges is not null && freeRanges.Count > 0)
        {
            List<IntRange> freeRngs = new(freeRanges),
                copyRngs = new();
            freeRngs.Sort((a, b) => a.Start.CompareTo(b.Start));

            int prevEnd = 0;
            foreach (var rng in freeRngs)
            {
                Debug.Assert(prevEnd <= rng.Start);
                Debug.Assert(rng.Start < rng.End);
                Debug.Assert(rng.End <= bankSize);

                if (prevEnd < rng.Start)
                    copyRngs.Add(new(prevEnd, rng.Start));

                prevEnd = rng.End;
            }

            if (prevEnd < bankSize)
                copyRngs.Add(new(prevEnd, bankSize));

            FreeRanges = freeRngs;
            if (sourceBank is not null)
                CopyRanges = copyRngs;
        }
        else
            FreeRanges = new IntRange[1] { new(0, bankSize) };

        SourceBank = sourceBank;
    }
}

/// <summary>
/// In addition to the track table, which maps the game's internal track numbers to imported songs and contains the complete set of songs in the game, a game may have 0 or more song maps, which map different game-specific scenarios (e.g. specific boss fights) to song numbers in the main track table (or to no music). Song maps are usually value-added features of the FT conversion not present in the original game, allowing different areas in the game that normally use the same song to use different songs.
/// </summary>
public record SongMapInfo(string Name, int Offset, int Length, byte EmptyIndex = 0xff);

/*public class SongMapInfo
{
    public readonly string Name;

    public readonly int Offset;
    public readonly int Length;

    public readonly byte EmptyIndex;

    public SongMapInfo(
        string name, 
        int offset, 
        int length, 
        byte emptyIndex = 0xff)
    {
        Name = name;
        Offset = offset;
        Length = length;
        EmptyIndex = emptyIndex;
    }
}*/

public abstract class Importer
{
    /// <summary>
    /// The size of each ROM bank
    /// </summary>
    protected abstract int BankSize { get; }

    protected abstract List<int> FreeBanks { get; }
    protected virtual int EmptyBankIdx { get; } = 0;
    protected virtual int EmptySongIdx { get; } = 0xff;
    protected virtual int EmptyModAddr { get; } = 0;

    protected abstract int PrimarySquareChan { get; }
    protected abstract IstringSet Uses { get; }
    protected abstract IstringSet DefaultUses { get; }
    protected abstract bool DefaultStreamingSafe { get; }

    /// <summary>
    /// Offset of the song map table in the ROM
    /// </summary>
    protected abstract int SongMapOffs { get; }

    /// <summary>
    /// Offset of the song module address table in the ROM
    /// </summary>
    protected abstract int SongModAddrTblOffs { get; }

    protected abstract HashSet<int> BuiltinSongIdcs { get; }
    protected abstract List<int> FreeSongIdcs { get; }
    protected abstract int NumSongs { get; }

    protected abstract IReadOnlyDictionary<string, SongMapInfo> SongMapInfos { get; }

    protected abstract int NumFtChannels { get; }
    protected abstract int DefaultFtStartAddr { get; }
    protected abstract int DefaultFtPrimarySquareChan { get; }

    public readonly LibraryParserOptions DefaultParserOptions = new();

    protected IReadOnlyDictionary<string, BankLayout> EngineBankLayouts { get; }

    protected byte[]? Rom;
    protected readonly IRomAccess RomWriter;
    //protected readonly IShuffler Shuffler;

    public Importer(
        IReadOnlyDictionary<string, BankLayout> EngineBankLayouts,
        IRomAccess RomWriter/*,
        IShuffler Shuffler*/)
    {
        Debug.Assert(EngineBankLayouts.ContainsKey("ft"));

        this.EngineBankLayouts = new IstringDictionary<BankLayout>(EngineBankLayouts);

        try
        {
            this.Rom = RomWriter.Rom;
        }
        catch (NotImplementedException)
        {
            this.Rom = null;
        }

        this.RomWriter = RomWriter;
        //this.Shuffler = Shuffler;
    }

    public LibraryInfo<TItem, TGroup> ParseJsonLibrary<TItem, TGroup>(
        string jsonData,
        LibraryParserOptions? opts = null)
        where TItem : MusicFileInfo
        where TGroup : GroupInfo<TItem>
    {
        opts = opts ?? DefaultParserOptions;
        JsonSerializerSettings? settings = null;
        if (!opts.IgnoreExtraFields)
        {
            settings = new();
            settings.MissingMemberHandling = MissingMemberHandling.Error;
        }

        var libObj = JsonConvert.DeserializeObject<LibraryInfo<TItem, TGroup>>(jsonData, settings);
        Debug.Assert(libObj is not null); ////

        return libObj;
    }

    protected IEnumerable<TSong> LoadJsonLibrarySongs<TSong, TItem, TGroup>(
        LibraryInfo<TItem, TGroup> libObj,
        Func<TGroup?, IEnumerable<TItem>, LibraryParserOptions?, IEnumerable<TSong>> LoadSongs,
        LibraryParserOptions? opts = null)
        where TSong : ISong
        where TItem : MusicFileInfo
        where TGroup : GroupInfo<TItem>
    {
        List<TSong> songs = new(LoadSongs(null, libObj.Single, opts));
        foreach (var grpInfo in libObj.Groups)
            songs.AddRange(LoadSongs(grpInfo, grpInfo.Items, opts));

        return songs;
    }

    protected IEnumerable<TSong> LoadJsonLibrarySongs<TSong, TItem, TGroup>(
        string jsonData,
        Func<TGroup?, IEnumerable<TItem>, LibraryParserOptions?, IEnumerable<TSong>> LoadSongs,
        LibraryParserOptions? opts = null)
        where TSong : ISong
        where TItem : MusicFileInfo
        where TGroup : GroupInfo<TItem>
    {
        var libObj = ParseJsonLibrary<TItem, TGroup>(jsonData, opts);
        return LoadJsonLibrarySongs(libObj, LoadSongs, opts);
    }

    public IEnumerable<FtSong> LoadFtJsonLibrarySongs(
        string jsonData,
        LibraryParserOptions? opts = null)
    {
        return LoadJsonLibrarySongs<FtSong, FtModuleInfo, FtModuleGroupInfo>(jsonData, LoadFtSongs, opts);
    }

    protected IEnumerable<FtSong> LoadFtSongs(
        FtModuleGroupInfo? grpInfo,
        IEnumerable<FtModuleInfo> modInfos,
        LibraryParserOptions? opts = null)
    {
        opts = opts ?? DefaultParserOptions;
        foreach (FtModuleInfo modInfo in modInfos)
        {
            Debug.Assert(modInfo.Size <= BankSize, $"FTM {modInfo.Title} is larger than a bank"); ////

            Module mod = new(
                "ft",
                modInfo.Title,
                modInfo.StartAddr ?? DefaultFtStartAddr,
                modInfo.UncompressedData);
            for (int i = 0; i < Math.Max(modInfo.Songs.Count, 1); i++)
            {
                FtSong song = new(
                    grpInfo,
                    modInfo,
                    i,
                    mod,
                    DefaultFtPrimarySquareChan,
                    DefaultStreamingSafe,
                    DefaultUses);
                if ((song.Enabled || !opts.EnabledOnly)
                    && (song.StreamingSafe || !opts.SafeOnly))
                    yield return song;
            }
        }
    }

    public Dictionary<string, List<ISong>> SplitSongsByUsage(
        IEnumerable<ISong> songs)
    {
        RefSet<ISong> haveSongs = new();
        IstringDictionary<List<ISong>> usageSongs = new();
        foreach (var usage in Uses)
            usageSongs[usage] = new();

        foreach (var song in songs)
        {
            Debug.Assert(!haveSongs.Contains(song));

            foreach (var usage in song.Uses)
                usageSongs[usage].Add(song);

            haveSongs.Add(song);
        }

        return usageSongs;
    }

    public void Import(
        IReadOnlyDictionary<int, ISong?> songs,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, ISong?>>? songMaps,
        out HashSet<int> freeBanks)
    {
        RefDictionary<ISong, int> songIdcs;
        Dictionary<int, ISong?> songMap;
        CreateSongIndexMap(songs, songMaps, out songIdcs, out songMap);
        var impModInfos = CreateImportedModuleInfos(songMap);

        Queue<int> freeBanksQueue = new(FreeBanks);
        RefDictionary<BankLayout, Queue<BankRange>> freeRngQueues = new();
        foreach (var layout in EngineBankLayouts.Values)
            freeRngQueues.TryAdd(layout, new());

        Dictionary<int, BankData> banks = new();
        ImportModulesData(
            impModInfos, 
            banks,
            freeBanksQueue,
            freeRngQueues);

        WritePrimarySongMap(songMap, impModInfos);
        if (songMaps is not null)
        {
            foreach (var (mapName, mapSongs) in songMaps)
                WriteSongMap(songIdcs, mapName, mapSongs, impModInfos);
        }

        // Get the ROM again because the song maps may need to be duplicated
        byte[]? rom;
        try
        {
            rom = RomWriter.Rom;
        }
        catch (NotImplementedException)
        {
            rom = null;
        }

        foreach (var (bankIdx, bankData) in banks)
        {
            var layout = bankData.Layout;
            if (layout.SourceBank is not null)
            {
                Debug.Assert(rom is not null);
                Debug.Assert(layout.CopyRanges is not null);

                foreach (var rng in layout.CopyRanges)
                {
                    Array.Copy(
                        rom,
                        bankIdx * layout.BankSize + rng.Start + 0x10,
                        bankData.Data,
                        rng.Start,
                        rng.End - rng.Start);
                }
            }

            RomWriter.Write(
                bankIdx * BankSize + 0x10,
                bankData.Data,
                $"Bank {bankIdx:x} Song Data");
        }

        freeBanks = new(freeBanksQueue);

        return;
    }

    public void TestRebase(IEnumerable<ISong?> songs)
    {
        RefSet<Module> doneMods = new();
        foreach (var song in songs)
        {
            if (song is null)
                continue;

            var mod = song.Module;
            if (mod is null || doneMods.Contains(mod))
                continue;

            var modInfo = CreateImportedModuleInfo(mod);
            var data = modInfo.GetData(
                0x8001, song.PrimarySquareChan > 0 ? 0 : 1);

            Debug.Assert(data.Length == mod.Data.Length);

            doneMods.Add(mod);
        }

        return;
    }

    protected static IEnumerable<int> InRange(int first, int last)
    {
        return Enumerable.Range(first, checked(last + 1 - first));
    }

    protected static IEnumerable<int> ExRange(int start, int stop)
    {
        return Enumerable.Range(start, stop - start);
    }

    protected void CreateSongIndexMap(
        IReadOnlyDictionary<int, ISong?> songs,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, ISong?>>? songLists,
        out RefDictionary<ISong, int> songIdcs,
        out Dictionary<int, ISong?> songMap)
    {
        songMap = new(songs);
        songIdcs = new();

        HashSet<int> freeIdcs = new(FreeSongIdcs);
        foreach (var (songIdx, song) in songs)
        {
            if (song is null)
                continue;

            songIdcs.TryAdd(song, songIdx);
            freeIdcs.Remove(songIdx);
        }

        if (songLists is not null)
        {
            Queue<int> freeIdxQueue = new(freeIdcs.OrderBy((x) => -x));
            foreach (var (listName, songList) in songLists)
            {
                foreach (var (listIdx, song) in songList)
                {
                    if (song is not null && !songIdcs.ContainsKey(song))
                    {
                        if (song.Module is not null)
                        {
                            int songIdx = freeIdxQueue.Dequeue();
                            songIdcs[song] = songIdx;
                            songMap[songIdx] = song;
                        }
                        else
                            songIdcs[song] = song.Number;
                    }
                }
            }
        }

        return;
    }

    protected Dictionary<Module, ImportedModuleInfo> CreateImportedModuleInfos(Dictionary<int, ISong?> songMap)
    {
        RefDictionary<Module, ImportedModuleInfo> impModInfos = new();
        foreach (var (songIdx, song) in songMap)
        {
            if (song is null) 
                continue;

            Module? mod = song.Module;
            if (mod is null)
                continue;

            ImportedModuleInfo? modInfo = null;
            if (!impModInfos.TryGetValue(mod, out modInfo))
            {
                modInfo = CreateImportedModuleInfo(mod);
                impModInfos[mod] = modInfo;
            }

            modInfo.Songs.Add(song);
            modInfo.SongIndices[songIdx] = song.Number;
        }

        return impModInfos;
    }

    protected virtual ImportedModuleInfo CreateImportedModuleInfo(Module mod)
    {
        Debug.Assert(mod.IsEngine("ft")); ////

        return new ImportedFtModuleInfo(mod, NumFtChannels);
    }

    protected void ImportModulesData(
        Dictionary<Module, ImportedModuleInfo> impModInfos,
        Dictionary<int, BankData> banks,
        Queue<int> freeBanks,
        Dictionary<BankLayout, Queue<BankRange>> freeRngQueues)
    {
        IstringDictionary<List<ImportedModuleInfo>> engInfos = new();
        foreach (var modInfo in impModInfos.Values)
        {
            string eng = modInfo.Module.Engine;
            if (!engInfos.ContainsKey(eng))
                engInfos[eng] = new();

            engInfos[eng].Add(modInfo);
        }

        foreach (var (eng, modInfos) in engInfos)
            ImportEngineModules(
                eng, 
                modInfos,
                banks, 
                freeBanks,
                freeRngQueues);

        return;
    }

    protected void ImportEngineModules(
        string eng,
        List<ImportedModuleInfo> modInfos,
        Dictionary<int, BankData> banks,
        Queue<int> freeBanks,
        Dictionary<BankLayout, Queue<BankRange>> freeRngQueues,
        int rngSizeThresh = 64)
    {
        foreach (var info in modInfos)
            Debug.Assert(info.Module.IsEngine(eng));

        var layout = EngineBankLayouts[eng];
        var freeRngs = freeRngQueues[layout];
        RefSet<ImportedModuleInfo> modsLeft = new(modInfos);
        List<ImportedModuleInfo> infos = new(modsLeft.OrderBy((info) => -info.Module.Data.Length));
        List<BankRange> newFreeRngs = new();
        ImportedModuleInfoSizeComparer comparer = new(0);
        while (modsLeft.Count > 0 
            && (freeRngs.Count > 0 || freeBanks.Count > 0))
        {
            BankRange? rng = null;
            if (!freeRngs.TryDequeue(out rng))
            {
                int bankIdx = freeBanks.Dequeue();
                foreach (var rngInfo in layout.FreeRanges)
                    freeRngs.Enqueue(new(bankIdx, rngInfo.Start, rngInfo.End));

                rng = freeRngs.Dequeue();
            }

            Debug.Assert(rng is not null);

            int baseAddr = layout.BankBaseAddr + rng.Start,
                bytesLeft = rng.End - rng.Start;

            BankData? bankData;
            if (!banks.TryGetValue(rng.Bank, out bankData))
            {
                bankData = new(layout, new byte[BankSize]);
                banks[rng.Bank] = bankData;
            }

            int curIdx = 0;
            while (modsLeft.Count > 0 && curIdx < infos.Count)
            {
                comparer.Size = bytesLeft;
#pragma warning disable CS8625
                curIdx = infos.BinarySearch(curIdx, infos.Count - curIdx, null, comparer);
#pragma warning restore CS8625
                if (curIdx >= 0)
                {
                    while (curIdx > 0 && infos[curIdx - 1].Module.Data.Length == bytesLeft)
                        curIdx--;
                }
                else
                {
                    curIdx = ~curIdx;
                    if (curIdx >= infos.Count)
                        break;
                }

                var info = infos[curIdx];
                Debug.Assert(info.Module.Data.Length <= bytesLeft);

                //// TODO: Speed this up with binary search
                /*for (; curIdx < infos.Count; curIdx++)
                {
                    if (infos[curIdx].Module.Data.Length <= bytesLeft)
                        break;
                }

                if (curIdx >= infos.Count)
                    break;

                ImportedModuleInfo info = infos[curIdx];*/
                int size = info.Module.Data.Length,
                    rngOffs = bytesLeft - size,
                    address = rngOffs + baseAddr;

                var data = info.GetData(address, PrimarySquareChan);
                data.CopyTo(bankData.Data, rng.Start + rngOffs);

                info.Bank = rng.Bank;
                info.Address = address;

                modsLeft.Remove(info);

                bytesLeft = rngOffs;
                infos.RemoveAt(curIdx);
            }

            Debug.Assert(bytesLeft != BankSize); ////

            if (bytesLeft >= rngSizeThresh)
                newFreeRngs.Add(new(rng.Bank, rng.Start, rng.Start + bytesLeft));
        }

        if (modsLeft.Count > 0)
            throw new RomFullException();

        foreach (var rng in newFreeRngs)
            freeRngs.Enqueue(rng);
    }

    protected virtual void WritePrimarySongMap(
        IReadOnlyDictionary<int, ISong?> songs,
        Dictionary<Module, ImportedModuleInfo> modInfos)
    {
        IReadOnlyList<byte>? origMap = null;
        if (Rom is not null)
            origMap = new ArraySegment<byte>(
                Rom,
                SongMapOffs,
                NumSongs * 2);
        int[] modAddrs = new int[NumSongs];

        byte[] buffData = new byte[2];
        foreach (var (songIdx, song) in songs)
        {
            int bankIdx, modAddr, modSongIdx;
            if (song is null)
            {
                // No music
                bankIdx = modAddr = 0;
                modSongIdx = 0xff;
            }
            else if (song.Module is null)
            {
                // Builtin track
                Debug.Assert(origMap is not null);

                bankIdx = origMap[songIdx * 2];
                modSongIdx = origMap[songIdx * 2 + 1];
                modAddr = EmptyModAddr;
            }
            else
            {
                // Normal track
                var modInfo = modInfos[song.Module];
                modInfo.GetSongMapEntry(songIdx, out bankIdx, out modSongIdx);
                modAddr = modInfo.Address;
            }

            int romOffs = SongMapOffs + songIdx * 2;
            buffData[0] = checked((byte)bankIdx);
            buffData[1] = checked((byte)modSongIdx);
            RomWriter.Write(
                romOffs,
                buffData, 
                $"Song Map Entry {songIdx:x}");

            modAddrs[songIdx] = modAddr;
        }

        byte[] modAddrsData = new byte[NumSongs * 2];
        BinaryBuffer modAddrsBuff = new(modAddrsData);
        modAddrsBuff.WriteLE(from int addr in modAddrs select (UInt16)addr);

        RomWriter.Write(
            SongModAddrTblOffs,
            modAddrsData, 
            "Song Module Address Table");
    }

    protected virtual void WriteSongMap(
        IReadOnlyDictionary<ISong, int> songIdcs,
        string mapName,
        IReadOnlyDictionary<int, ISong?> mapSongs,
        Dictionary<Module, ImportedModuleInfo> modInfos)
    {
        var mapInfo = SongMapInfos[mapName];
        foreach (var (mapSongIdx, song) in mapSongs)
        {
            int songIdx = song is not null
                ? songIdx = songIdcs[song]
                : mapInfo.EmptyIndex;

            RomWriter.Write(
                mapInfo.Offset + mapSongIdx,
                checked((byte)songIdx),
                $"Song Map {mapName} Entry {mapSongIdx:x}");
        }

        return;
    }
}
