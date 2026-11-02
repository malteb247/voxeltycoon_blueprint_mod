using VoxelTycoon;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;

namespace BlueprintMod.Models;

/// <summary>A bridge element (body, span, pier) or a tunnel segment.</summary>
public class PartEntry
{
    public PartKind Kind { get; set; }

    /// <summary>Asset name of the bridge or tunnel.</summary>
    public string AssetUri { get; set; }

    public BuildingRotation Rotation { get; set; }

    /// <summary>Only used for bridges.</summary>
    public TrackBridgeType BridgeType { get; set; }

    /// <summary>Only used for bridges.</summary>
    public RailType RailType { get; set; }

    /// <summary>Only used for bridges.</summary>
    public int Height { get; set; }

    /// <summary>Only used for tunnels.</summary>
    public TrackTunnelType TunnelType { get; set; }

    /// <summary>Lowest corner of the part, relative to the blueprint origin.</summary>
    public Xyz Position { get; set; }

    /// <summary>Footprint at the saved rotation.</summary>
    public Xyz Size { get; set; }

    /// <summary>Index into <see cref="Blueprint.Parts"/> (e.g. pier to bridge body), or -1.</summary>
    public int Parent { get; set; } = -1;
}
