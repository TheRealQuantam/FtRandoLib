using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FtRandoLib.Utility;

using Buffer = IList<byte>;

/// <summary>
/// Buffer for binary data that can read and write individual and series of 8-bit and 16-bit values with automatic endian conversion.
/// </summary>
public class BinaryBuffer
{
    /// <summary>
    /// Interface for encoding 16-bit values with different endians. Made an interface rather than a delegate or parameter in the hope of generating optimized inlined loops.
    /// </summary>
    private interface IEndianType16
    {
        UInt16 Read(Buffer buffer, ref int pos);
        void Write(Buffer buffer, ref int pos, UInt16 value);
    }

    private struct BigEndian16 : IEndianType16
    {
        public UInt16 Read(Buffer buffer, ref int pos)
        {
            byte msb = buffer[pos++];
            return (UInt16)(((UInt16)msb << 8) | buffer[pos++]);
        }

        public void Write(Buffer buffer, ref int pos, UInt16 value)
        {
            buffer[pos++] = (byte)(value >> 8);
            buffer[pos++] = (byte)value;
        }
    }

    private struct LittleEndian16 : IEndianType16
    {
        public UInt16 Read(Buffer buffer, ref int pos)
        {
            byte lsb = buffer[pos++];
            return (UInt16)(((UInt16)buffer[pos++] << 8) | lsb);
        }

        public void Write(Buffer buffer, ref int pos, UInt16 value)
        {
            buffer[pos++] = (byte)value;
            buffer[pos++] = (byte)(value >> 8);
        }
    }

    private Buffer buffer;
    private int position = 0;

    /// <summary>
    /// Verify that there is enough space for a read.
    /// </summary>
    /// <param name="bufferLength"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    private void CheckReadSize(int bufferLength, int index, int count)
    {
        Debug.Assert(bufferLength >= 0);

        if (index < 0 || count < 0)
            throw new ArgumentOutOfRangeException();
        else if (checked(index + count) > bufferLength)
            throw new EndOfStreamException();
    }

    /// <summary>
    /// Verify that there's enough space for a write from an enumerable.
    /// </summary>
    /// <param name="elementSize"></param>
    /// <param name="count"></param>
    private void CheckWriteSize(int elementSize, int? count)
    {
        Debug.Assert(elementSize > 0);

        if (count is null)
            return;

        if (count < 0)
            throw new ArgumentOutOfRangeException();
        else if (checked(position + count * elementSize) > buffer.Count)
            throw new OverflowException();
    }

    /// <summary>
    /// Verify that there's enough space for a write from a list-like buffer.
    /// </summary>
    /// <param name="elementSize"></param>
    /// <param name="bufferLength"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    private void CheckWriteSize(int elementSize, int bufferLength, int index, int count)
    {
        Debug.Assert(elementSize > 0);
        Debug.Assert(bufferLength >= 0);

        if (index < 0 || count < 0)
            throw new ArgumentOutOfRangeException();
        else if (checked(index + count) > bufferLength)
            throw new ArgumentException();
        else if (checked(position + count * elementSize) > buffer.Count)
            throw new OverflowException();
    }

    /// <summary>
    /// Read a single UInt16 from the current position.
    /// </summary>
    /// <typeparam name="Encoding"></typeparam>
    /// <param name="advance"></param>
    /// <returns></returns>
    private UInt16 InternalRead<Encoding>(bool advance = true)
        where Encoding : IEndianType16, new()
    {
        int pos = position;
        Encoding enc = new();
        ushort value = enc.Read(buffer, ref pos);
        if (advance)
            position = pos;

        return value;
    }

    /// <summary>
    /// Read multiple UInt16s from the current position.
    /// </summary>
    /// <typeparam name="Encoding"></typeparam>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of UInt16s read.</returns>
    private int InternalRead<Encoding>(IList<UInt16> values, int index = 0, int? count = null, bool advance = true)
        where Encoding : IEndianType16, new()
    {
        if (count is null)
            count = values.Count;

        CheckReadSize(values.Count, index, (int)count);

        int pos = position;
        Encoding enc = new();
        for (int i = index; i < index + count; i++)
            values[i] = enc.Read(buffer, ref pos);

        if (advance)
            position = pos;

        return (int)count;
    }

    /// <summary>
    /// Write a single UInt16 to the current position.
    /// </summary>
    /// <typeparam name="Encoding"></typeparam>
    /// <param name="value"></param>
    /// <param name="advance"></param>
    private void InternalWrite<Encoding>(UInt16 value, bool advance = true)
         where Encoding : IEndianType16, new()
    {
        if (checked(position + 2) > buffer.Count)
            throw new OverflowException();

        int pos = position;
        Encoding enc = new();
        enc.Write(buffer, ref pos, value);

        if (advance)
            position = pos;
    }

    /// <summary>
    /// Write multiple UInt16s from an enumerator to the current position.
    /// </summary>
    /// <typeparam name="Encoding"></typeparam>
    /// <param name="iter"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of UInt16s written.</returns>
    private int InternalWrite<Encoding>(IEnumerator<UInt16> iter, int? count = null, bool advance = true)
         where Encoding : IEndianType16, new()
    {
        CheckWriteSize(1, count);

        int pos = position;
        Encoding enc = new();
        if (count is not null)
        {
            int endPos = position + (int)count * 2;
            while (pos < endPos && iter.MoveNext())
                enc.Write(buffer, ref pos, iter.Current);
        }
        else
        {
            while (iter.MoveNext())
                enc.Write(buffer, ref pos, iter.Current);
        }

        int numWritten = (pos - position) / 2;
        if (advance)
            position = pos;

        return numWritten;
    }

    /// <summary>
    /// Write multiple UInt16s from a list-like buffer to the current position.
    /// </summary>
    /// <typeparam name="Encoding"></typeparam>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of UInt16s written.</returns>
    private int InternalWrite<Encoding>(IReadOnlyList<UInt16> values, int index = 0, int? count = null, bool advance = true)
         where Encoding : IEndianType16, new()
    {
        if (count is null)
            count = values.Count - index;

        CheckWriteSize(2, values.Count, index, (int)count);

        int pos = position;
        Encoding enc = new();
        for (int inPos = index; inPos < index + count; inPos++)
            enc.Write(this.buffer, ref pos, values[inPos]);

        if (advance)
            position = pos;

        return (int)count;
    }

    public BinaryBuffer(Buffer buffer)
    {
        this.buffer = buffer;
    }

    /// <summary>
    /// The current position that subsequent reads or writes will occur at.
    /// </summary>
    public int Position
    {
        get { return position; }
        set { Seek(value); }
    }

    /// <summary>
    /// Access an arbitary byte in the buffer without using/updating Position.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public byte this[int index]
    {
        get { return buffer[index]; }
        set { buffer[index] = value; }
    }

    /// <summary>
    /// Set the buffer position for subsequent reads and writes.
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="origin"></param>
    /// <returns>The new absolute position.</returns>
    public int Seek(int offset, SeekOrigin origin = SeekOrigin.Begin)
    {
        int newPos = position;
        checked
        {
            if (origin == SeekOrigin.Begin)
                newPos = offset;
            else if (origin == SeekOrigin.Current)
                newPos += offset;
            else if (origin == SeekOrigin.End)
                newPos = buffer.Count + offset;
            else
                throw new ArgumentException();
        }

        if (newPos < 0)
            throw new OverflowException();

        position = newPos;

        return position;
    }

    /// <summary>
    /// Try to read a byte from the current position.
    /// </summary>
    /// <param name="advance"></param>
    /// <returns>The byte as an int, or -1 if past the end of the buffer.</returns>
    public int Read(bool advance = true)
    {
        int value = -1;
        if (position < buffer.Count)
        {
            value = buffer[position];

            if (advance)
                position++;
        }

        return value;
    }

    /// <summary>
    /// Read a byte from the current position.
    /// </summary>
    /// <param name="advance"></param>
    /// <returns></returns>
    public byte ReadByte(bool advance = true)
    {
        byte value = buffer[position];

        if (advance)
            position++;

        return value;
    }

    /// <summary>
    /// Read an sbyte from the current position.
    /// </summary>
    /// <param name="advance"></param>
    /// <returns></returns>
    public sbyte ReadSByte(bool advance = true)
    {
        return (sbyte)ReadByte(advance);
    }

    /// <summary>
    /// Read multiple bytes from the current position into a buffer.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of bytes read.</returns>
    public int Read(IList<byte> values, int index = 0, int? count = null, bool advance = true)
    {
        if (count is null)
            count = values.Count;

        CheckReadSize(values.Count, index, (int)count);

        int pos = position;
        for (int i = index; i < index + count; i++)
            values[i] = buffer[pos++];

        if (advance)
            position = pos;

        return (int)count;
    }

    /// <summary>
    /// Read multiple sbytes from the current position into a buffer.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of sbytes read.</returns>
    public int Read(IList<sbyte> values, int index = 0, int? count = null, bool advance = true)
    {
        return Read((IList<byte>)values, index, count, advance);
    }

    /// <summary>
    /// Get an enumerator to read bytes beginning from the current position. The enumerator has its own position, and does not advance the position of the BinaryBuffer object.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<byte> GetByteEnumerator()
    {
        for (int pos = position; pos < buffer.Count; pos++)
            yield return buffer[pos++];
    }

    /// <summary>
    /// Get an enumerator to read sbytes beginning from the current position. The enumerator has its own position, and does not advance the position of the BinaryBuffer object.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<sbyte> GetSByteEnumerator()
    {
        for (int pos = position; pos < buffer.Count; pos++)
            yield return (sbyte)buffer[pos++];
    }

    /// <summary>
    /// Read a single big-endian UInt16 from the current position.
    /// </summary>
    /// <param name="advance"></param>
    /// <returns></returns>
    public UInt16 ReadUInt16BE(bool advance = true)
    {
        return InternalRead<BigEndian16>(advance);
    }

    /// <summary>
    /// Read a single little-endian UInt16 from the current position.
    /// </summary>
    /// <param name="advance"></param>
    /// <returns></returns>
    public UInt16 ReadUInt16LE(bool advance = true)
    {
        return InternalRead<LittleEndian16>(advance);
    }

    /// <summary>
    /// Read a single big-endian Int16 from the current position.
    /// </summary>
    /// <param name="advance"></param>
    /// <returns></returns>
    public Int16 ReadInt16BE(bool advance = true)
    {
        return (short)ReadUInt16BE(advance);
    }

    /// <summary>
    /// Read a single little-endian Int16 from the current position.
    /// </summary>
    /// <param name="advance"></param>
    /// <returns></returns>
    public Int16 ReadInt16LE(bool advance = true)
    {
        return (short)ReadUInt16LE(advance);
    }

    /// <summary>
    /// Read multiple big-endian UInt16s from the currently position into a buffer.
    /// </summary>
    /// <param name="values">The buffer to read into.</param>
    /// <param name="index">The initial index to read into.</param>
    /// <param name="count">The number of UInt16s to read, or null to fill the whole buffer starting at index.</param>
    /// <param name="advance">Whether to advance the position after read.</param>
    /// <returns>The number of UInt16s read.</returns>
    public int ReadBE(IList<UInt16> values, int index = 0, int? count = null, bool advance = true)
    {
        return InternalRead<BigEndian16>(values, index, count, advance);
    }

    /// <summary>
    /// Read multiple little-endian UInt16s from the currently position into a buffer.
    /// </summary>
    /// <param name="values">The buffer to read into.</param>
    /// <param name="index">The initial index to read into.</param>
    /// <param name="count">The number of UInt16s to read, or null to fill the whole buffer starting at index.</param>
    /// <param name="advance">Whether to advance the position after read.</param>
    /// <returns>The number of UInt16s read.</returns>
    public int ReadLE(IList<UInt16> values, int index = 0, int? count = null, bool advance = true)
    {
        return InternalRead<LittleEndian16>(values, index, count, advance);
    }

    /// <summary>
    /// Read multiple big-endian Int16s from the current position into a buffer.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of Int16s read.</returns>
    public int ReadBE(IList<Int16> values, int index = 0, int? count = null, bool advance = true)
    {
        return ReadBE((IList<UInt16>)values, index, count, advance);
    }

    /// <summary>
    /// Read multiple little-endian Int16s from the current position into a buffer.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of Int16s read.</returns>
    public int ReadLE(IList<Int16> values, int index = 0, int? count = null, bool advance = true)
    {
        return ReadLE((IList<UInt16>)values, index, count, advance);
    }

    private IEnumerable<UInt16> GetUInt16Enumerable<Endian>() where Endian : IEndianType16, new()
    {
        int pos = position;
        Endian enc = new();
        while (pos + 1 < buffer.Count)
            yield return enc.Read(buffer, ref pos);
    }

    private IEnumerable<Int16> GetInt16Enumerable<Endian>() where Endian : IEndianType16, new()
    {
        int pos = position;
        Endian enc = new();
        while (pos + 1 < buffer.Count)
            yield return (Int16)enc.Read(buffer, ref pos);
    }

    /// <summary>
    /// Get an enumerable to read big-endian UInt16s beginning from the current position. The enumerable has its own position, and does not advance the position of the BinaryBuffer object.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<UInt16> GetUInt16BEEnumerable()
    {
        return GetUInt16Enumerable<BigEndian16>();
    }

    /// <summary>
    /// Get an enumerable to read little-endian UInt16s beginning from the current position. The enumerable has its own position, and does not advance the position of the BinaryBuffer object.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<UInt16> GetUInt16LEEnumerable()
    {
        return GetUInt16Enumerable<LittleEndian16>();
    }

    /// <summary>
    /// Get an enumerable to read big-endian Int16s beginning from the current position. The enumerable has its own position, and does not advance the position of the BinaryBuffer object.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Int16> GetInt16BEEnumerable()
    {
        return GetInt16Enumerable<BigEndian16>();
    }

    /// <summary>
    /// Get an enumerable to read little-endian Int16s beginning from the current position. The enumerable has its own position, and does not advance the position of the BinaryBuffer object.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Int16> GetInt16LEEnumerable()
    {
        return GetInt16Enumerable<LittleEndian16>();
    }

    /*/// <summary>
    /// Get an enumerator to read big-endian UInt16s beginning from the current position. The enumerator has its own position, and does not advance the position of the BinaryBuffer object.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<UInt16> GetUInt16BEEnumerator()
    {
        int pos = position;
        BigEndian16 enc = new();
        while (pos + 1 < buffer.Count)
            yield return enc.Read(buffer, ref pos);
    }

    /// <summary>
    /// Get an enumerator to read little-endian UInt16s beginning from the current position. The enumerator has its own position, and does not advance the position of the BinaryBuffer object.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<UInt16> GetUInt16LEEnumerator()
    {
        int pos = position;
        LittleEndian16 enc = new();
        while (pos + 1 < buffer.Count)
            yield return enc.Read(buffer, ref pos);
    }

    /// <summary>
    /// Get an enumerator to read big-endian Int16s beginning from the current position. The enumerator has its own position, and does not advance the position of the BinaryBuffer object.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<Int16> GetInt16BEEnumerator()
    {
        return from x in GetUInt16BEEnumerable() select (Int16)x;
    }

    /// <summary>
    /// Get an enumerator to read little-endian Int16s beginning from the current position. The enumerator has its own position, and does not advance the position of the BinaryBuffer object.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<Int16> GetInt16LEEnumerator()
    {
        return from x in GetUInt16LEEnumerable() select (Int16)x;
    }*/

    /// <summary>
    /// Write a byte to the current position.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="advance"></param>
    public void Write(byte value, bool advance = true)
    {
        buffer[position] = value;

        if (advance)
            position++;
    }

    /// <summary>
    /// Write multiple bytes from an enumerator to the current position. Begins by calling MoveNext, so the initial position should be prior to the first element to be written.
    /// </summary>
    /// <param name="iter"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of bytes written.</returns>
    public int Write(IEnumerator<byte> iter, int? count = null, bool advance = true)
    {
        CheckWriteSize(1, count);

        int pos = position;
        if (count is not null)
        {
            int endPos = position + (int)count;
            while (pos < endPos && iter.MoveNext())
                buffer[pos++] = iter.Current;
        }
        else
        {
            while (iter.MoveNext())
                buffer[pos++] = iter.Current;
        }

        int numWritten = pos - position;
        if (advance)
            position = pos;

        return numWritten;
    }

    /// <summary>
    /// Write multiple bytes from an enumerable to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of bytes written.</returns>
    public int Write(IEnumerable<byte> values, int? count = null, bool advance = true)
    {
        return Write(values.GetEnumerator(), count, advance);
    }

    /// <summary>
    /// Write multiple bytes from a list-like object to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of bytes written.</returns>
    public int Write(IReadOnlyList<byte> values, int index = 0, int? count = null, bool advance = true)
    {
        if (count is null)
            count = values.Count - index;

        CheckWriteSize(1, values.Count, index, (int)count);

        int pos = position;
        for (int inPos = index; inPos < index + count; inPos++)
            this.buffer[pos++] = values[inPos];

        if (advance)
            position = pos;

        return (int)count;
    }

    /// <summary>
    /// Write an sbyte to the current position.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="advance"></param>
    public void Write(sbyte value, bool advance = true)
    {
        Write((byte)value, advance);
    }

    /// <summary>
    /// Write multiple sbytes from an enumerator to the current position. Begins by calling MoveNext, so the initial position should be prior to the first element to be written.
    /// </summary>
    /// <param name="iter"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of sbytes written.</returns>
    public int Write(IEnumerator<sbyte> iter, int? count = null, bool advance = true)
    {
        return Write((IEnumerator<byte>)iter, count, advance);
    }

    /// <summary>
    /// Write multiple sbytes from an enumerable to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of sbytes written.</returns>
    public int Write(IEnumerable<sbyte> values, int? count = null, bool advance = true)
    {
        return Write((IEnumerable<byte>)values, count, advance);
    }

    /// <summary>
    /// Write multiple bytes from a list-like object to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of sbytes written.</returns>
    public int Write(IReadOnlyList<sbyte> values, int index = 0, int? count = null, bool advance = true)
    {
        return Write((IReadOnlyList<byte>)values, index, count, advance);
    }

    /// <summary>
    /// Write a big-endian UInt16 to the current position.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="advance"></param>
    public void WriteBE(UInt16 value, bool advance = true)
    {
        InternalWrite<BigEndian16>(value, advance);
    }

    /// <summary>
    /// Write a little-endian UInt16 to the current position.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="advance"></param>
    public void WriteLE(UInt16 value, bool advance = true)
    {
        InternalWrite<LittleEndian16>(value, advance);
    }

    /// <summary>
    /// Write multiple big-endian UInt16s from an enumerator to the current position. Begins by calling MoveNext, so the initial position should be prior to the first element to be written.
    /// </summary>
    /// <param name="iter"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of UInt16s written.</returns>
    public int WriteBE(IEnumerator<UInt16> iter, int? count = null, bool advance = true)
    {
        return InternalWrite<BigEndian16>(iter, count, advance);
    }

    /// <summary>
    /// Write multiple little-endian UInt16s from an enumerator to the current position. Begins by calling MoveNext, so the initial position should be prior to the first element to be written.
    /// </summary>
    /// <param name="iter"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of UInt16s written.</returns>
    public int WriteLE(IEnumerator<UInt16> iter, int? count = null, bool advance = true)
    {
        return InternalWrite<LittleEndian16>(iter, count, advance);
    }

    /// <summary>
    /// Write multiple big-endian UInt16s from an enumerable to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of UInt16s written.</returns>
    public int WriteBE(IEnumerable<UInt16> values, int? count = null, bool advance = true)
    {
        return WriteBE(values.GetEnumerator(), count, advance);
    }

    /// <summary>
    /// Write multiple little-endian UInt16s from an enumerable to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of UInt16s written.</returns>
    public int WriteLE(IEnumerable<UInt16> values, int? count = null, bool advance = true)
    {
        return WriteLE(values.GetEnumerator(), count, advance);
    }

    /// <summary>
    /// Write multiple big-endian UInt16s from a list-like object to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of UInt16s written.</returns>
    public int WriteBE(IReadOnlyList<UInt16> values, int index = 0, int? count = null, bool advance = true)
    {
        return InternalWrite<BigEndian16>(values, index, count, advance);
    }

    /// <summary>
    /// Write multiple little-endian UInt16s from a list-like object to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of UInt16s written.</returns>
    public int WriteLE(IReadOnlyList<UInt16> values, int index = 0, int? count = null, bool advance = true)
    {
        return InternalWrite<LittleEndian16>(values, index, count, advance);
    }

    /// <summary>
    /// Write a big-endian Int16 to the current position.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="advance"></param>
    public void WriteBE(Int16 value, bool advance = true)
    {
        WriteBE((UInt16)value, advance);
    }

    /// <summary>
    /// Write a little-endian Int16 to the current position.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="advance"></param>
    public void WriteLE(Int16 value, bool advance = true)
    {
        WriteLE((UInt16)value, advance);
    }

    /// <summary>
    /// Write multiple big-endian Int16s from an enumerator to the current position. Begins by calling MoveNext, so the initial position should be prior to the first element to be written.
    /// </summary>
    /// <param name="iter"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of Int16s written.</returns>
    public int WriteBE(IEnumerator<Int16> iter, int? count = null, bool advance = true)
    {
        return WriteBE((IEnumerator<UInt16>)iter, count, advance);
    }

    /// <summary>
    /// Write multiple little-endian Int16s from an enumerator to the current position. Begins by calling MoveNext, so the initial position should be prior to the first element to be written.
    /// </summary>
    /// <param name="iter"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of Int16s written.</returns>
    public int WriteLE(IEnumerator<Int16> iter, int? count = null, bool advance = true)
    {
        return WriteLE((IEnumerator<UInt16>)iter, count, advance);
    }

    /// <summary>
    /// Write multiple big-endian Int16s from an enumerable to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of Int16s written.</returns>
    public int WriteBE(IEnumerable<Int16> values, int? count = null, bool advance = true)
    {
        return WriteBE((IEnumerable<UInt16>)values, count, advance);
    }

    /// <summary>
    /// Write multiple little-endian Int16s from an enumerable to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of Int16s written.</returns>
    public int WriteLE(IEnumerable<Int16> values, int? count = null, bool advance = true)
    {
        return WriteLE((IEnumerable<UInt16>)values, count, advance);
    }

    /// <summary>
    /// Write multiple big-endian Int16s from a list-like object to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of Int16s written.</returns>
    public int WriteBE(IReadOnlyList<Int16> values, int index = 0, int? count = null, bool advance = true)
    {
        return WriteBE((IReadOnlyList<UInt16>)values, index, count, advance);
    }

    /// <summary>
    /// Write multiple little-endian Int16s from a list-like object to the current position.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="index"></param>
    /// <param name="count"></param>
    /// <param name="advance"></param>
    /// <returns>The number of Int16s written.</returns>
    public int WriteLE(IReadOnlyList<Int16> values, int index = 0, int? count = null, bool advance = true)
    {
        return WriteLE((IReadOnlyList<UInt16>)values, index, count, advance);
    }
}
