using VoxelTycoon;
using VoxelTycoon.Tracks.Rails;

namespace BlueprintMod.Models;

/// <summary>A saved rail piece. Positions are relative to the blueprint origin.</summary>
public class RailEntry
{
    /// <summary>Asset name of the rail, e.g. <c>base/regular.rail</c>.</summary>
    public string AssetUri { get; set; }

    public RailType Type { get; set; }

    public BuildingRotation Rotation { get; set; }

    public bool WithEmbankment { get; set; }

    public RailElectrificationMode Electrification { get; set; }

    public bool DisallowSignals { get; set; }

    /// <summary>Lowest corner of the rail, relative to the blueprint origin.</summary>
    public Xyz Position { get; set; }

    /// <summary>Footprint at the saved rotation.</summary>
    public Xyz Size { get; set; }

    /// <summary>Index into <see cref="Blueprint.Parts"/> of the bridge or tunnel carrying this rail, or -1.</summary>
    public int Parent { get; set; } = -1;
}
