using System.Collections.Generic;

namespace FtRandoLib.Utility;

/// <summary>
/// A HashSet whose items are compared by reference.
/// </summary>
public class RefSet<T> : HashSet<T>
    where T: class
{
    public RefSet()
        : base(ReferenceEqualityComparer.Instance)
    { }

    public RefSet(IEnumerable<T> items)
        : base(items, ReferenceEqualityComparer.Instance)
    { }
}
