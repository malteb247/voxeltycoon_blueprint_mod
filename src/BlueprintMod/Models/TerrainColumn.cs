namespace BlueprintMod.Models;

/// <summary>Terrain height of one column, relative to the lowest rail of the blueprint.</summary>
public struct TerrainColumn
{
    public int X { get; set; }

    public int Z { get; set; }

    public int RelHeight { get; set; }
}
