using System;
using System.Collections.Generic;

namespace FtRandoLib.Utility;

/// <summary>
/// A Dictionary whose keys are case-insensitive strings (via the invariant culture) and values are TValue.
/// </summary>
public class IstringDictionary<TValue> : Dictionary<string, TValue>
{
    public IstringDictionary()
        : base(StringComparer.InvariantCultureIgnoreCase)
    { }

    public IstringDictionary(IEnumerable<KeyValuePair<string, TValue>> items)
        : base(items, StringComparer.InvariantCultureIgnoreCase)
    { }
}
