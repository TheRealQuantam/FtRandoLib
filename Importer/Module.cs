using System;

namespace FtRandoLib.Importer;

/// <summary>
/// A module is the unit of data that is imported by Importer, containing one or more songs. Importer can automatically handle simple modules consisting of a single contiguous, self-contained block of data, but complex cases, e.g. music formats that have separate track and instrument data such as Capcom 3, require derived classes and implementations of Importer.
/// </summary>
public class Module
{
    /// <summary>
    /// The engine that will play this module.
    /// </summary>
    public readonly string Engine;

    /// <summary>
    /// The title of the module, primarily for debugging.
    /// </summary>
    public readonly string Title;

    /// <summary>
    /// The base address of Data.
    /// </summary>
    public readonly int Address;

    /// <summary>
    /// The song data in its native format.
    /// </summary>
    public readonly byte[] Data;

    public Module(string engine, string title, int addr, byte[] data)
    {
        Engine = engine;
        Title = title;
        Address = addr;
        Data = data;
    }

    public override string ToString() => $"[{Engine} : \"{Title}\"]";

    /// <summary>
    /// Tests whether the module's engine is that specified.
    /// </summary>
    public bool IsEngine(string engine)
    {
        return StringComparer.InvariantCultureIgnoreCase.Equals(Engine, engine);
    }
}
