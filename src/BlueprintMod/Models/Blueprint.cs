using System.Collections.Generic;
using VoxelTycoon;

namespace BlueprintMod.Models;

/// <summary>
/// A saved section of the rail network: rails, bridges and tunnels, signals and the terrain shape under them.
/// Everything is stored relative to the lowest rail of the captured area.
/// </summary>
public class Blueprint
{
    /// <summary>Display name; also the file name without extension.</summary>
    public string Name { get; set; }

    /// <summary>Bounding box of all rails and bridge/tunnel parts.</summary>
    public Xyz Size { get; set; }

    public List<RailEntry> Rails { get; } = [];

    public List<PartEntry> Parts { get; } = [];

    public List<SignalEntry> Signals { get; } = [];

    /// <summary>Empty for blueprints saved by older versions.</summary>
    public List<TerrainColumn> Terrain { get; } = [];
}
