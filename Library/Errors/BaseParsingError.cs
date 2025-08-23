using System;

namespace FtRandoLib.Library;

/// <summary>
/// The base form of a ParsingError that can be thrown directly, without needing to know the types involved in deserialization. This should be caught at the appropriate level and converted into a proper ParsingError.
/// </summary>
public class BaseParsingError : Exception
{
    /// <summary>
    /// If the parsing error was caused by something else - e.g. a base-64 format error - contains the Message from that error.
    /// </summary>
    public string? Submessage { get; init; } = null;

    /// <summary>
    /// The line number the error occurred at, or 0 if unknown.
    /// </summary>
    public int LineNum { get; init; } = 1;

    /// <summary>
    /// The column at which the error occurred, or 0 if unknown.
    /// </summary>
    public int ColumnNum { get; init; } = 1;

    /// <summary>
    /// The hierarchy path to the node causing the error, e.g. single[0].title, or null if unknown.
    /// </summary>
    public string? Path { get; init; } = null;

    /// <summary>
    /// The object being parsed at the time of error, or null if unknown.
    /// </summary>
    public object? Object { get; init; } = null;

    /// <summary>
    /// The name of the field in the object which caused the error, or null if unknown.
    /// </summary>
    public string? FieldName { get; init; } = null;

    public BaseParsingError(
        string? message = null, 
        Exception? innerException = null)
        : base(message, innerException)
    { }

    protected BaseParsingError(
        BaseParsingError source,
        string? message,
        Exception? innerException = null)
        : this(message, innerException)
    {
        Submessage = source.Submessage;
        LineNum = source.LineNum;
        ColumnNum = source.ColumnNum;
        Path = source.Path;
        Object = source.Object;
        FieldName = source.FieldName;
    }

    protected BaseParsingError(BaseParsingError source)
        : this(source.Message, source.InnerException)
    { }
}
