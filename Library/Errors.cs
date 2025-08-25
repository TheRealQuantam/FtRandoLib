using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using YamlDotNet.Core;

namespace FtRandoLib.Library;

/// <summary>
/// An error that occurred during LibraryInfo.Parse - either a parsing error or data format error.
/// </summary>
public class ParsingError : BaseParsingError
{
    /// <summary>
    /// The type of the item (e.g. module) contained in the library being parsed.
    /// </summary>
    public Type ItemType { get; }

    /// <summary>
    /// The type of the item group contained in the library being parsed.
    /// </summary>
    public Type GroupType { get; }

    /// <summary>
    /// A composite string that can be displayed in error messages indicating where the error occurred, or null if no location information is known.
    /// </summary>
    public string? AtString
    {
        get
        {
            if (!_atStringBuilt)
            {
                _atString = BuildAtString();
                _atStringBuilt = true;
            }

            return _atString;
        }
    }

    string? _atString = null;
    bool _atStringBuilt = false;

    public ParsingError(
        string? message,
        Type itemType,
        Type groupType,
        Exception? innerException)
        : base(message, innerException)
    {
        ItemType = itemType;
        GroupType = groupType;
    }

    public ParsingError(
        BaseParsingError source,
        string? message,
        Type itemType,
        Type groupType,
        Exception? innerException = null)
        : base(source, message, innerException)
    {
        ItemType = itemType;
        GroupType = groupType;
    }

    public ParsingError(
        BaseParsingError source,
        Type itemType,
        Type groupType)
        : this(source,
              source.Message,
              itemType,
              groupType,
              source.InnerException)
    { }

    public ParsingError(
        Type itemType, 
        Type groupType,
        object? sender, 
        Newtonsoft.Json.Serialization.ErrorEventArgs args)
        : base(TrimMessage(args.ErrorContext.Error.Message),
            innerException: args.ErrorContext.Error)
    {
        var ctx = args.ErrorContext;
        var ex = ctx.Error;

        Submessage = ex.InnerException?.Message;
        Path = string.IsNullOrEmpty(ctx.Path) ? null : ctx.Path;
        ItemType = itemType;
        GroupType = groupType;
        Object = ctx.OriginalObject;
        FieldName = (ctx.Member as string);

        if (ex is JsonSerializationException serEx)
        {
            LineNum = serEx.LineNumber;
            ColumnNum = serEx.LinePosition;
        }
        else if (ex is JsonReaderException readEx)
        {
            LineNum = readEx.LineNumber;
            ColumnNum = readEx.LinePosition;
        }
    }

    [DoesNotReturn]
    public static void Throw<TItem, TGroup>(
        object? sender,
        Newtonsoft.Json.Serialization.ErrorEventArgs args)
        => throw new ParsingError(typeof(TItem), typeof(TGroup), sender, args);

    [DoesNotReturn]
    public static void Throw<TItem, TGroup>(YamlException yamlEx)
    {
        Exception ex = yamlEx;
        if (ex.InnerException is not null)
            ex = ex.InnerException;
        if (ex is TargetInvocationException
            && ex.InnerException is not null)
            ex = ex.InnerException;
        if (ex is ParsingError)
            throw ex;

        throw new ParsingError(
            ex.Message, typeof(TItem), typeof(TGroup), ex.InnerException)
        {
            LineNum = checked((int)yamlEx.Start.Line),
            ColumnNum = checked((int)yamlEx.Start.Column),
        };
    }

    /// <summary>
    /// If location information is present on the error, it is often suffixed to the error message. This is redundant as that information will also be present in e.g. LineNum.
    /// </summary>
    /// <param name="msg">The exception error message to trim.</param>
    static string TrimMessage(string msg)
    {
        int dotIdx = msg.IndexOf(". Path '");
        return dotIdx >= 0
            ? msg.Substring(0, dotIdx + 1)
            : msg;
    }

    string? BuildAtString()
    {
        StringBuilder sb = new();

        if (LineNum != 0)
        {
            string colPart = ColumnNum != 0
                ? $", position {ColumnNum}"
                : "";
            sb.Append($"On line {LineNum}{colPart}");
        }

        if (Path is not null || Object is not null)
        {
            if (sb.Length != 0)
                sb.Append('\n');

            string objPart = "";
            if (Object is not null)
            {
                string titlePart = "";
                if (Object is MusicInfo info && !string.IsNullOrEmpty(info.Title))
                    titlePart = $" with title '{info.Title}'";

                objPart = $" ({Object.GetType().Name}{titlePart})";
            }

            string fieldPart = FieldName is not null
                && (Path is null || !Path.EndsWith(FieldName))
                ? $", field '{FieldName}'"
                : "";

            sb.Append($"At path '{Path ?? string.Empty}'{objPart}{fieldPart}");
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }
}
