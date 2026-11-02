namespace BlueprintMod.Models;

/// <summary>A signal on one end of a saved rail.</summary>
public class SignalEntry
{
    /// <summary>Asset name of the signal.</summary>
    public string AssetUri { get; set; }

    /// <summary>Index into <see cref="Blueprint.Rails"/>.</summary>
    public int RailIndex { get; set; }

    /// <summary>Index of the rail connection that carries the signal.</summary>
    public int ConnectionIndex { get; set; }
}
