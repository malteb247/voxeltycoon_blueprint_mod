using BlueprintMod.Models;
using VoxelTycoon;

namespace BlueprintMod;

/// <summary>
/// Maps positions stored in a blueprint to world positions for a given placement:
/// the whole blueprint is rotated around its bounding box and then moved to <c>origin</c>.
/// </summary>
internal readonly struct PlacementTransform(Blueprint blueprint, Xyz origin, BuildingRotation rotation)
{
    private readonly Xyz _blueprintSize = blueprint.Size;

    public BuildingRotation Rotation { get; } = rotation;

    public Xyz Origin { get; } = origin;

    /// <summary>World position (lowest corner) of an element with the given saved position and footprint.</summary>
    public Xyz Apply(Xyz position, Xyz size)
    {
        Xyz translation = BuildingHelper.GetTranslation(_blueprintSize, Rotation);
        Xyz relative = position.Rotate(Rotation, translation) - BuildingHelper.GetTranslation(size, Rotation);
        return Origin + relative;
    }

    /// <summary>World column of a saved terrain column.</summary>
    public Xz Apply(TerrainColumn column)
    {
        return (Xz)Apply(new Xyz(column.X, 0, column.Z), Xyz.One);
    }
}
