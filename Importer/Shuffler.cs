using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FtRandoLib.Importer;

public interface IShuffler
{
    IList<T> Shuffle<T>(IReadOnlyList<T> items);
}

/// <summary>
/// Simple implementation of IShuffler based on Random.
/// </summary>
public class RandomShuffler : IShuffler
{
    Random _rnd;

    public RandomShuffler(Random rnd)
    {
        _rnd = rnd;
    }

    public IList<T> Shuffle<T>(IReadOnlyList<T> items)
    {
        var array = items.ToArray();

        _rnd.Shuffle(array);

        return array;
    }
}