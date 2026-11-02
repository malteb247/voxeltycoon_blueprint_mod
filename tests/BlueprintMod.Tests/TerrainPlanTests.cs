using System.Collections.Generic;
using System.Linq;
using BlueprintMod.Models;
using VoxelTycoon;
using Xunit;

namespace BlueprintMod.Tests;

public class TerrainPlanTests
{
    private static readonly Xz Column = new(1, 1);

    [Fact]
    public void First_value_is_always_taken()
    {
        var plan = new TerrainPlan();

        plan.Set(Column, 5, onlyIfLower: true);

        Assert.Equal(5, Heights(plan)[Column]);
    }

    [Fact]
    public void Later_value_replaces_the_earlier_one_by_default()
    {
        var plan = new TerrainPlan();

        plan.Set(Column, 5);
        plan.Set(Column, 9);
        plan.Set(Column, 2);

        Assert.Equal(2, Heights(plan)[Column]);
    }

    [Theory]
    [InlineData(8, 5)]
    [InlineData(5, 5)]
    [InlineData(3, 3)]
    public void Only_if_lower_keeps_the_lower_value(int candidate, int expected)
    {
        var plan = new TerrainPlan();
        plan.Set(Column, 5);

        plan.Set(Column, candidate, onlyIfLower: true);

        Assert.Equal(expected, Heights(plan)[Column]);
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(5, 5)]
    [InlineData(9, 9)]
    public void Only_if_higher_keeps_the_higher_value(int candidate, int expected)
    {
        var plan = new TerrainPlan();
        plan.Set(Column, 5);

        plan.Set(Column, candidate, onlyIfHigher: true);

        Assert.Equal(expected, Heights(plan)[Column]);
    }

    [Fact]
    public void Columns_keep_the_order_in_which_they_were_added()
    {
        var plan = new TerrainPlan();

        plan.Set(new Xz(3, 3), 1);
        plan.Set(new Xz(1, 2), 1);
        plan.Set(new Xz(3, 3), 4);
        plan.Set(new Xz(0, 0), 1);

        Assert.Equal(
            new[] { new Xz(3, 3), new Xz(1, 2), new Xz(0, 0) },
            plan.Targets.Select(target => target.Key));
    }

    [Fact]
    public void Empty_plan_has_no_targets()
    {
        Assert.Empty(new TerrainPlan().Targets);
    }

    [Fact]
    public void Captured_terrain_is_placed_relative_to_the_origin()
    {
        Blueprint blueprint = TestBlueprints.Valid();
        var transform = new PlacementTransform(blueprint, new Xyz(100, 20, 200), BuildingRotation.Rotate0);

        TerrainPlan plan = TerrainPlanner.FromCapturedTerrain(blueprint, transform);

        Dictionary<Xz, int> heights = Heights(plan);
        Assert.Equal(3, heights.Count);
        Assert.Equal(19, heights[new Xz(100, 200)]);
        Assert.Equal(18, heights[new Xz(104, 202)]);
        Assert.Equal(23, heights[new Xz(109, 205)]);
    }

    [Theory]
    [InlineData(BuildingRotation.Rotate90)]
    [InlineData(BuildingRotation.Rotate180)]
    [InlineData(BuildingRotation.Rotate270)]
    public void Rotating_moves_the_columns_but_keeps_their_heights(BuildingRotation rotation)
    {
        Blueprint blueprint = TestBlueprints.Valid();
        var origin = new Xyz(100, 20, 200);
        var straight = new PlacementTransform(blueprint, origin, BuildingRotation.Rotate0);
        var turned = new PlacementTransform(blueprint, origin, rotation);

        List<int> before = [.. TerrainPlanner.FromCapturedTerrain(blueprint, straight).Targets.Select(t => t.Value)];
        List<int> after = [.. TerrainPlanner.FromCapturedTerrain(blueprint, turned).Targets.Select(t => t.Value)];

        Assert.Equal(before, after);
    }

    [Fact]
    public void Blueprint_without_terrain_gives_an_empty_plan()
    {
        Blueprint blueprint = TestBlueprints.Valid();
        blueprint.Terrain.Clear();
        var transform = new PlacementTransform(blueprint, Xyz.Zero, BuildingRotation.Rotate0);

        Assert.Empty(TerrainPlanner.FromCapturedTerrain(blueprint, transform).Targets);
    }

    private static Dictionary<Xz, int> Heights(TerrainPlan plan) =>
        plan.Targets.ToDictionary(target => target.Key, target => target.Value);
}
