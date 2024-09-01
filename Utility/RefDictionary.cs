using System.Collections.Generic;

namespace FtRandoLib.Utility;

/// <summary>
/// A Dictionary whose keys are compared by reference.
/// </summary>
public class RefDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    where TKey : class
{
    public RefDictionary()
        : base(ReferenceEqualityComparer.Instance)
    { }

    public RefDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items)
        : base(items, ReferenceEqualityComparer.Instance)
    { }
}
