using System.Collections.Generic;
using BlueprintMod.Models;
using VoxelTycoon;
using Xunit;

namespace BlueprintMod.Tests;

public class PlacementTransformTests
{
    private static readonly Xyz Origin = new(100, 5, 200);

    public static TheoryData<BuildingRotation> Rotations() =>
    [
        BuildingRotation.Rotate0,
        BuildingRotation.Rotate90,
        BuildingRotation.Rotate180,
        BuildingRotation.Rotate270,
    ];

    [Theory]
    [MemberData(nameof(Rotations))]
    public void Every_placement_stays_inside_the_rotated_blueprint(BuildingRotation rotation)
    {
        Blueprint blueprint = TestBlueprints.Valid();
        var transform = new PlacementTransform(blueprint, Origin, rotation);
        Xyz box = blueprint.Size.RotateSize(rotation);
        var footprint = new Xyz(3, 1, 2);
        Xyz rotatedFootprint = footprint.RotateSize(rotation);

        for (int x = 0; x <= blueprint.Size.X - footprint.X; x++)
        {
            for (int z = 0; z <= blueprint.Size.Z - footprint.Z; z++)
            {
                Xyz world = transform.Apply(new Xyz(x, 0, z), footprint);

                Assert.InRange(world.X, Origin.X, Origin.X + box.X - rotatedFootprint.X);
                Assert.InRange(world.Z, Origin.Z, Origin.Z + box.Z - rotatedFootprint.Z);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void Different_placements_stay_different(BuildingRotation rotation)
    {
        Blueprint blueprint = TestBlueprints.Valid();
        var transform = new PlacementTransform(blueprint, Origin, rotation);
        var seen = new HashSet<Xyz>();
        int count = 0;

        for (int x = 0; x < blueprint.Size.X; x++)
        {
            for (int z = 0; z < blueprint.Size.Z; z++)
            {
                seen.Add(transform.Apply(new Xyz(x, 0, z), Xyz.One));
                count++;
            }
        }

        Assert.Equal(count, seen.Count);
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void Height_is_never_changed_by_rotation(BuildingRotation rotation)
    {
        Blueprint blueprint = TestBlueprints.Valid();
        var transform = new PlacementTransform(blueprint, Origin, rotation);

        Xyz world = transform.Apply(new Xyz(2, -3, 1), Xyz.One);

        Assert.Equal(Origin.Y - 3, world.Y);
    }

    [Fact]
    public void Without_rotation_positions_are_only_moved()
    {
        Blueprint blueprint = TestBlueprints.Valid();
        var transform = new PlacementTransform(blueprint, Origin, BuildingRotation.Rotate0);

        Xyz world = transform.Apply(new Xyz(2, 1, 3), new Xyz(3, 3, 1));

        Assert.Equal(new Xyz(102, 6, 203), world);
    }

    [Fact]
    public void Half_turn_moves_the_first_corner_to_the_opposite_one()
    {
        Blueprint blueprint = TestBlueprints.Valid();
        var transform = new PlacementTransform(blueprint, Origin, BuildingRotation.Rotate180);

        Xyz world = transform.Apply(Xyz.Zero, Xyz.One);

        Assert.Equal(new Xyz(100 + 9, 5, 200 + 5), world);
    }

    [Fact]
    public void Quarter_turn_swaps_width_and_depth()
    {
        Blueprint blueprint = TestBlueprints.Valid();
        var transform = new PlacementTransform(blueprint, Origin, BuildingRotation.Rotate90);
        Xyz box = blueprint.Size.RotateSize(BuildingRotation.Rotate90);
        Xyz farCorner = transform.Apply(new Xyz(9, 0, 5), Xyz.One);

        Assert.Equal(new Xyz(6, 4, 10), box);
        Assert.InRange(farCorner.X, Origin.X, Origin.X + box.X - 1);
        Assert.InRange(farCorner.Z, Origin.Z, Origin.Z + box.Z - 1);
    }

    [Fact]
    public void Transform_remembers_rotation_and_origin()
    {
        var transform = new PlacementTransform(TestBlueprints.Valid(), Origin, BuildingRotation.Rotate270);

        Assert.Equal(BuildingRotation.Rotate270, transform.Rotation);
        Assert.Equal(Origin, transform.Origin);
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void Terrain_column_uses_the_same_mapping_as_a_one_voxel_placement(BuildingRotation rotation)
    {
        Blueprint blueprint = TestBlueprints.Valid();
        var transform = new PlacementTransform(blueprint, Origin, rotation);
        var column = new TerrainColumn { X = 4, Z = 2, RelHeight = 0 };

        Xz mapped = transform.Apply(column);
        Xyz expected = transform.Apply(new Xyz(4, 0, 2), Xyz.One);

        Assert.Equal(new Xz(expected.X, expected.Z), mapped);
    }
}
