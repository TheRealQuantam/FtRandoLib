using BinaryBufferLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FtRandoLib.Utility;

internal class Instrument
{
    public int Flags = 0;
    public int?[] SeqAddrs = new int?[5];

    public Instrument(ref BufferReader buffer)
    {
        Flags = buffer.ReadByte();

        byte chanMask = buffer.ReadByte();
        for (int i = 0; i < SeqAddrs.Length; i++)
        {
            SeqAddrs[i] = (chanMask & 1) != 0 ? buffer.ReadUInt16LE() : null;
            chanMask >>= 1;
        }
    }
}

internal class Song
{
    public int BaseAddr;

    public int FrameListAddr;

    public int PatternLength;
    public int Speed;
    public int Tempo;
    public int GroovePos;
    public int InitialBank;

    public List<int> FrameAddrs;
    public List<int[]> FramePatAddrs;

    public Song(ref BufferReader buffer, int baseAddr, int numChans)
    {
        BaseAddr = baseAddr;

        FrameListAddr = buffer.ReadUInt16LE();

        int numFrames = buffer.ReadByte();
        PatternLength = buffer.ReadByte();
        Speed = buffer.ReadByte();
        Tempo = buffer.ReadByte();
        GroovePos = buffer.ReadByte();
        InitialBank = buffer.ReadByte();

        buffer.Seek(FrameListAddr - baseAddr);
        FrameAddrs = (from x in buffer.ReadUInt16LEs().Take(numFrames) select (int)x).ToList();

        FramePatAddrs = new List<int[]>();
        foreach (int frameAddr in FrameAddrs)
        {
            buffer.Seek(frameAddr - baseAddr);
            FramePatAddrs.Add((from x in buffer.ReadUInt16LEs().Take(numChans) select (int)x).ToArray());
        }
    }
}

/// <summary>
/// FamiTracker module binary class for parsing and modifying FTMs. Mainly used for rebasing and swapping square channels.
/// </summary>
public class FtmBinary
{
    public int BaseAddr { get; private set; }
    private byte[] data;

    public int SongListAddr { get; private set; }
    public int InstrListAddr { get; private set; }
    public int SampleMapAddr { get; private set; }
    public int SamplesAddr { get; private set; }
    public int GroovesAddr { get; private set; }

    public byte Flags { get; private set; }

    public int NtscSpeed { get; private set; }
    public int PalSpeed { get; private set; }

    private List<int> songAddrs;
    private List<int> instrAddrs;

    private List<Song> songs;
    private List<Instrument> instrs;

    public FtmBinary(byte[] data, int baseAddr = 0, int numChans = 5)
    {
        this.data = data;
        BaseAddr = baseAddr;

        BufferReader rdr = new(data);
        SongListAddr = rdr.ReadUInt16LE();
        InstrListAddr = rdr.ReadUInt16LE();
        SampleMapAddr = rdr.ReadUInt16LE();
        SamplesAddr = rdr.ReadUInt16LE();
        GroovesAddr = rdr.ReadUInt16LE();

        Flags = rdr.ReadByte();

        NtscSpeed = rdr.ReadUInt16LE();
        PalSpeed = rdr.ReadUInt16LE();

        // Parse instruments
        instrAddrs = new List<int>();
        instrs = new List<Instrument>();
        if (InstrListAddr != SampleMapAddr)
        {
            rdr.Seek(InstrListAddr - baseAddr);

            int firstInstrAddr = rdr.PeekUInt16LE();
            int numInstrs = (firstInstrAddr - InstrListAddr) / 2;
            instrAddrs = (from x in rdr.ReadUInt16LEs().Take(numInstrs) select (int)x).ToList();

            foreach (int instrAddr in instrAddrs)
            {
                rdr.Seek(instrAddr - baseAddr);
                instrs.Add(new Instrument(ref rdr));
            }
        }

        // Parse songs
        songAddrs = new List<int>();
        songs = new List<Song>();
        if (SongListAddr != InstrListAddr)
        {
            rdr.Seek(SongListAddr - baseAddr);

            int firstSongAddr = rdr.PeekUInt16LE();
            int numSongs = (firstSongAddr - SongListAddr) / 2;
            songAddrs = (from x in rdr.ReadUInt16LEs().Take(numSongs) select (int)x).ToList();

            songs = new List<Song>();
            foreach (int songAddr in songAddrs)
            {
                rdr.Seek(songAddr - baseAddr);
                songs.Add(new Song(ref rdr, baseAddr, numChans));
            }
        }
    }

    /// <summary>
    /// Returns the modified FTM data.
    /// </summary>
    /// <returns></returns>
    public byte[] GetData()
        => data;

    /// <summary>
    /// Relocate the FTM to a new base address, fixing up the internal addresses.
    /// </summary>
    /// <param name="newAddr"></param>
    public void Rebase(Int32 newAddr)
    {
        int addrDelta = newAddr - BaseAddr;
        FixedBufferWriter buff = new(data);

        // Update the instruments
        for (int instrIdx = 0; instrIdx < instrAddrs.Count; instrIdx++)
        {
            buff.Seek(instrAddrs[instrIdx] + 2 - BaseAddr);

            IList<int?> seqAddrs = instrs[instrIdx].SeqAddrs;
            for (int seqIdx = 0; seqIdx < seqAddrs.Count; seqIdx++)
            {
                int? seqAddr = seqAddrs[seqIdx];
                if (seqAddr is not null)
                {
                    seqAddrs[seqIdx] = (seqAddr += addrDelta);
                    buff.WriteLE((UInt16)seqAddr);
                }
            }

            instrAddrs[instrIdx] += addrDelta;
        }

        buff.Seek(InstrListAddr - BaseAddr);
        buff.WriteLE(from addr in instrAddrs select (UInt16)addr);

        // Update the songs
        for (int songIdx = 0; songIdx < songAddrs.Count; songIdx++)
        {
            int songAddr = songAddrs[songIdx];
            Song song = songs[songIdx];

            for (int frameIdx = 0; frameIdx < song.FrameAddrs.Count; frameIdx++)
            {
                int frameAddr = song.FrameAddrs[frameIdx];
                int[] patAddrs = song.FramePatAddrs[frameIdx];

                for (int chanIdx = 0; chanIdx < patAddrs.Length; chanIdx++)
                    patAddrs[chanIdx] += addrDelta;

                buff.Seek(frameAddr - BaseAddr);
                buff.WriteLE(from addr in patAddrs select (UInt16)addr);

                song.FrameAddrs[frameIdx] = frameAddr + addrDelta;
            }

            buff.Seek(song.FrameListAddr - BaseAddr);
            buff.WriteLE(from addr in song.FrameAddrs select (UInt16)addr);

            song.FrameListAddr += addrDelta;
            buff.Seek(songAddr - BaseAddr);
            buff.WriteLE((UInt16)song.FrameListAddr);

            song.BaseAddr += addrDelta;

            songAddrs[songIdx] += addrDelta;
        }

        buff.Seek(SongListAddr - BaseAddr);
        buff.WriteLE(from addr in songAddrs select (UInt16)addr);

        // Update the header
        SongListAddr += addrDelta;
        InstrListAddr += addrDelta;
        SampleMapAddr += addrDelta;
        SamplesAddr += addrDelta;
        GroovesAddr += addrDelta;

        buff.Seek(0);

        buff.WriteLE((UInt16)SongListAddr);
        buff.WriteLE((UInt16)InstrListAddr);
        buff.WriteLE((UInt16)SampleMapAddr);
        buff.WriteLE((UInt16)SamplesAddr);
        buff.WriteLE((UInt16)GroovesAddr);

        BaseAddr = newAddr;
    }

    /// <summary>
    /// Swap the channel data for square 1 and square 2.
    /// </summary>
    /// <param name="songIdx"></param>
    public void SwapSquareChans(int songIdx)
    {
        Song song = songs[songIdx];
        FixedBufferWriter buff = new(data);
        HashSet<int> doneFrameAddrs = new();

        for (int frameIdx = 0; frameIdx < song.FrameAddrs.Count; frameIdx++)
        {
            int frameAddr = song.FrameAddrs[frameIdx];
            if (!doneFrameAddrs.Add(frameAddr))
                continue;

            int[] frame = song.FramePatAddrs[frameIdx];
            int square1Addr = frame[0];
            frame[0] = frame[1];
            frame[1] = square1Addr;

            buff.Seek(frameAddr - BaseAddr);
            buff.WriteLE(from addr in frame select (UInt16)addr);
        }
    }
}