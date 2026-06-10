using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace FtRandoLib.Library;

/// <summary>
/// YamlDotNet does not have a built-in mechanism to require that fields be present, so it must be implemented here.
/// </summary>
public class ValidatingYamlNodeDeserializer : INodeDeserializer
{
    readonly INodeDeserializer _inner;
    readonly List<ValidationResult> _valResults = new();

    public ValidatingYamlNodeDeserializer(INodeDeserializer inner)
    {
        _inner = inner;
    }

    public bool Deserialize(IParser reader,
        Type expectedType,
        Func<IParser, Type, object?> nestedObjectDeserializer,
        out object? value,
        ObjectDeserializer rootDeserializer)
    {
        value = null;

        var evt = reader.Current!;
        YamlException? yamlEx = null;

        try
        {
            if (!_inner.Deserialize(
                reader,
                expectedType,
                nestedObjectDeserializer,
                out value,
                rootDeserializer))
                return false; // Not an object
        }
        catch (YamlException e)
        {
            // An error occurred deserializing the object
            yamlEx = e;
        }

        if (value is not null)
        {
            bool throwEx = false;
            string? errMsg = null, fieldName = null;
            Mark mark = Mark.Empty;
            if (yamlEx != null)
            {
                Exception e = yamlEx;
                if (e.InnerException is not null)
                    e = e.InnerException;
                if (e is TargetInvocationException
                    && e.InnerException is not null)
                    e = e.InnerException;
                if (e is BaseParsingError)
                    throw e;

                errMsg = e.Message;
                mark = yamlEx.Start;
                //fieldName = 

                throwEx = true;
            }
            else
            {
                _valResults.Clear();
                if (!TryValidateObject(value, null, _valResults, true))
                {
                    var res = _valResults[0];
                    
                    errMsg = res.ErrorMessage;
                    fieldName = res.MemberNames.FirstOrDefault();
                    mark = evt.Start;
                    throwEx = true;
                }
            }

            if (throwEx)
                throw new BaseParsingError(errMsg)
                {
                    LineNum = checked((int)mark.Line),
                    ColumnNum = checked((int)mark.Column),
                    Object = value,
                    FieldName = fieldName,
                };
        }

        return true;
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
    Justification = "DynamicallyAccessedMemberTypes.All is required of the type")]
    protected static bool TryValidateObject<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        T instance,
        ValidationContext? validationContext,
        ICollection<ValidationResult>? validationResults,
        bool validateAllProperties)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));
        if (validationContext == null)
            validationContext = new(instance);

        return Validator.TryValidateObject(
            instance, 
            validationContext, 
            validationResults, 
            validateAllProperties);
    }
}