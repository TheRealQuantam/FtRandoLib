using System;
using System.Collections.Generic;

namespace FtRandoLib.Utility;

/// <summary>
/// A HashSet whose values are case-insensitive strings (via the invariant culture).
/// </summary>
public class IstringSet : HashSet<string>
{
    public IstringSet()
        : base(StringComparer.InvariantCultureIgnoreCase)
    { }

    public IstringSet(IEnumerable<string> values)
        : base(values, StringComparer.InvariantCultureIgnoreCase)
    { }
}
