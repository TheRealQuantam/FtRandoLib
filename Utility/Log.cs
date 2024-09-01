using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

// A general purpose mechanism for capturing logging information somewhere up the call tree without having to pass a log object through the entire network of objects being called. The type and purpose of logged information is up to the user. The primary motivations for creating a new mechanism rather than using Trace are 1. to not require TRACE, 2. so that only the innermost sink will capture the log entries, rather than all registered loggers (this is more useful for contextual log information that isn't globally relevant).

namespace FtRandoLib.Utility;

/// <summary>
/// A log sink, inspired by ITraceListener.
/// </summary>
public abstract class Logger : IDisposable
{
    int indentLevel = 0;

    /// <summary>
    /// The indentation level for relevant writes. Only applied when NeedIndent is true.
    /// </summary>
    public int IndentLevel
    {
        get => indentLevel;
        set
        {
            Debug.Assert(value >= 0);

            indentLevel = value;
        }
    }

    /// <summary>
    /// The number of spaces per indent level.
    /// </summary>
    public int IndentSize { get; set; } = 1;

    /// <summary>
    /// Whether the next write should apply intent. When indent is applied, NeedIndent is reset to false, requiring that it be set for each line needing indent.
    /// </summary>
    public bool NeedIndent { get; set; } = false;

    public virtual void Close() { }
    public virtual void Dispose() { }

    public virtual void Flush() { }

    /// <summary>
    /// Called by Write and WriteLine to write the indent and reset NeedIndent.
    /// </summary>
    public virtual void WriteIndent()
    {
        NeedIndent = false;

        InternalWrite(new string(' ', IndentSize * IndentLevel));
    }

    /// <summary>
    /// Writes a string to the log without ending the line.
    /// </summary>
    public virtual void Write(string? message)
    {
        if (NeedIndent)
            WriteIndent();

        InternalWrite(message);
    }

    /// <summary>
    /// Writes a line to the log.
    /// </summary>
    public virtual void WriteLine(string? message = null)
    {
        if (NeedIndent)
            WriteIndent();

        InternalWriteLine(message);
    }

    /// <summary>
    /// Internally-used function to write a string to the log.
    /// </summary>
    protected abstract void InternalWrite(string? message);

    /// <summary>
    /// Internally-used function to write a line to the log.
    /// </summary>
    protected abstract void InternalWriteLine(string? message);
}

/// <summary>
/// The default Logger, which does nothing with writes.
/// </summary>
public sealed class NullLogger : Logger
{
    protected override void InternalWrite(string? message) { }
    protected override void InternalWriteLine(string? message) { }
}

/// <summary>
/// A Logger which writes to a TextWriter object.
/// </summary>
public class TextLogger : Logger
{
    TextWriter Writer;

    public TextLogger(TextWriter writer)
    {
        Writer = writer;
    }

    public override void Close() => Writer.Close();
    public override void Dispose() => Writer.Dispose();

    public override void Flush() => Writer.Flush();
    protected override void InternalWrite(string? message) => Writer.Write(message);
    protected override void InternalWriteLine(string? message) => Writer.WriteLine(message);
}

/// <summary>
/// The actual interface for writing to the innermost Logger.
/// </summary>
public static class Log
{
    /// <summary>
    /// The thread-local Logger stacks.
    /// </summary>
    static readonly ThreadLocal<List<Logger>> loggers = new ThreadLocal<List<Logger>>(() => new List<Logger>() { new NullLogger() });

    /// <summary>
    /// Get the list of registered Loggers. The last Logger is the innermost, which will be used for calls to Write.
    /// </summary>
    public static List<Logger> Loggers
    {
        get
        {
            Debug.Assert(loggers.Value is not null);
            return loggers.Value;
        }
    }

    /// <summary>
    /// The indentation level for relevant writes. Only applied when NeedIndent is true.
    /// </summary>
    public static int IndentLevel
    {
        get => Loggers.Last().IndentLevel;
        set => Loggers.Last().IndentLevel = value;
    }

    /// <summary>
    /// The number of spaces per indent level.
    /// </summary>
    public static int IndentSize
    {
        get => Loggers.Last().IndentSize;
        set => Loggers.Last().IndentSize = value;
    }

    /// <summary>
    /// Whether the next write should apply intent. When indent is applied, NeedIndent is reset to false, requiring that it be set for each line needing indent.
    /// </summary>
    public static bool NeedIndent
    {
        get => Loggers.Last().NeedIndent;
        set => Loggers.Last().NeedIndent = value;
    }

    /// <summary>
    /// Push a new innermost Logger to the stack.
    /// </summary>
    public static void Push(Logger logger) => Loggers.Add(logger);

    /// <summary>
    /// Pop the innermost Logger from the stack.
    /// </summary>
    public static Logger Pop(bool close = false)
    {
        int i = Loggers.Count - 1;
        var logger = Loggers[i];

        if (close)
            logger.Close();

        Loggers.RemoveAt(i);

        return logger;
    }

    /// <summary>
    /// Writes a string to the log without ending the line.
    /// </summary>
    public static void Write(string? message) => Loggers.Last().Write(message);

    /// <summary>
    /// Writes a line to the log.
    /// </summary>
    public static void WriteLine(string? message = null) => Loggers.Last().WriteLine(message);
}
