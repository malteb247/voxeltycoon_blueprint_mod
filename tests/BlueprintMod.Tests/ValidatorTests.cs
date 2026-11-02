using System;
using System.Collections.Generic;
using BlueprintMod.Models;
using VoxelTycoon;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;
using Xunit;

namespace BlueprintMod.Tests;

public class ValidatorTests
{
    /// <summary>Every case breaks one rule of a valid blueprint and names the error that must be reported.</summary>
    public static TheoryData<string, Action<Blueprint>, string> InvalidBlueprints() => new()
    {
        { "empty name", b => b.Name = string.Empty, "invalid name" },
        { "name with tag", b => b.Name = "<size=99>x", "invalid name" },
        { "width zero", b => b.Size = new Xyz(0, 4, 6), "invalid size" },
        { "depth negative", b => b.Size = new Xyz(10, 4, -3), "invalid size" },
        { "width too large", b => b.Size = new Xyz(257, 4, 6), "invalid size" },
        { "height too large", b => b.Size = new Xyz(10, 257, 6), "invalid size" },
        { "huge size", b => b.Size = new Xyz(int.MaxValue, 4, int.MaxValue), "invalid size" },

        { "too many rails", b => Repeat(b.Rails, Validator.MaxRails + 1), "too many entries" },
        { "too many parts", b => Repeat(b.Parts, Validator.MaxParts + 1), "too many entries" },
        { "too many signals", b => Repeat(b.Signals, Validator.MaxSignals + 1), "too many entries" },
        { "too many terrain columns", b => Repeat(b.Terrain, Validator.MaxTerrainColumns + 1), "too many entries" },

        { "rail asset empty", b => b.Rails[0].AssetUri = string.Empty, "invalid rail asset" },
        { "rail asset with space", b => b.Rails[0].AssetUri = "base/a b", "invalid rail asset" },
        { "rail asset with control char", b => b.Rails[0].AssetUri = "base/\u0007x", "invalid rail asset" },
        { "rail asset too long", b => b.Rails[0].AssetUri = new string('a', 129), "invalid rail asset" },
        { "rail type undefined", b => b.Rails[0].Type = (RailType)777, "invalid rail value" },
        { "rail type unknown", b => b.Rails[0].Type = RailType.Unknown, "invalid rail value" },
        { "rail rotation undefined", b => b.Rails[0].Rotation = (BuildingRotation)9, "invalid rail value" },
        { "rail electrification undefined", b => b.Rails[0].Electrification = (RailElectrificationMode)5, "invalid rail value" },
        { "rail beyond width", b => b.Rails[0].Position = new Xyz(11, 0, 0), "rail outside of the blueprint" },
        { "rail negative x", b => b.Rails[0].Position = new Xyz(-1, 0, 0), "rail outside of the blueprint" },
        { "rail far away", b => b.Rails[0].Position = new Xyz(int.MaxValue, 0, 0), "rail outside of the blueprint" },
        { "rail footprint zero", b => b.Rails[0].Size = new Xyz(0, 1, 1), "rail outside of the blueprint" },
        { "rail footprint huge", b => b.Rails[0].Size = new Xyz(129, 1, 1), "rail outside of the blueprint" },
        { "rail parent too small", b => b.Rails[0].Parent = -2, "invalid rail parent" },
        { "rail parent missing", b => b.Rails[0].Parent = 3, "invalid rail parent" },

        { "part asset empty", b => b.Parts[0].AssetUri = string.Empty, "invalid part asset" },
        { "part rotation undefined", b => b.Parts[0].Rotation = (BuildingRotation)9, "invalid part value" },
        { "bridge type undefined", b => b.Parts[0].BridgeType = (TrackBridgeType)7, "invalid part value" },
        { "bridge rail type undefined", b => b.Parts[0].RailType = (RailType)777, "invalid part value" },
        { "tunnel flags undefined", b => b.Parts[2].TunnelType = (TrackTunnelType)8, "invalid part value" },
        { "bridge height negative", b => b.Parts[1].Height = -1, "invalid part value" },
        { "bridge height huge", b => b.Parts[1].Height = 1000, "invalid part value" },
        { "part beyond depth", b => b.Parts[0].Position = new Xyz(0, 0, 7), "part outside of the blueprint" },
        { "part far below", b => b.Parts[1].Position = new Xyz(4, -1000, 2), "part outside of the blueprint" },
        { "part is its own parent", b => b.Parts[1].Parent = 1, "invalid part parent" },
        { "part parent comes later", b => b.Parts[0].Parent = 1, "invalid part parent" },
        { "part parent cycle", b => { b.Parts[0].Parent = 2; b.Parts[2].Parent = 0; }, "invalid part parent" },

        { "signal asset empty", b => b.Signals[0].AssetUri = string.Empty, "invalid signal asset" },
        { "signal rail negative", b => b.Signals[0].RailIndex = -1, "invalid signal" },
        { "signal rail missing", b => b.Signals[0].RailIndex = 3, "invalid signal" },
        { "signal connection negative", b => b.Signals[0].ConnectionIndex = -1, "invalid signal" },
        { "signal connection missing", b => b.Signals[0].ConnectionIndex = 2, "invalid signal" },

        { "terrain x beyond width", b => b.Terrain[0] = Column(10, 0, 0), "invalid terrain column" },
        { "terrain z negative", b => b.Terrain[0] = Column(0, -1, 0), "invalid terrain column" },
        { "terrain too high", b => b.Terrain[0] = Column(0, 0, 257), "invalid terrain column" },
        { "terrain too low", b => b.Terrain[0] = Column(0, 0, -257), "invalid terrain column" },
    };

    [Fact]
    public void Valid_blueprint_is_accepted()
    {
        Assert.Null(Validator.Validate(TestBlueprints.Valid()));
    }

    [Fact]
    public void Blueprint_without_terrain_is_accepted()
    {
        Blueprint blueprint = TestBlueprints.Valid();
        blueprint.Terrain.Clear();

        Assert.Null(Validator.Validate(blueprint));
    }

    [Fact]
    public void Blueprint_with_only_rails_is_accepted()
    {
        Blueprint blueprint = TestBlueprints.Valid();
        blueprint.Parts.Clear();
        blueprint.Signals.Clear();
        foreach (RailEntry rail in blueprint.Rails)
            rail.Parent = -1;

        Assert.Null(Validator.Validate(blueprint));
    }

    [Theory]
    [MemberData(nameof(InvalidBlueprints))]
    public void Invalid_blueprint_is_rejected(string description, Action<Blueprint> breakIt, string expectedError)
    {
        Blueprint blueprint = TestBlueprints.Valid();
        breakIt(blueprint);

        Assert.Equal(expectedError, Validator.Validate(blueprint));
        Assert.False(string.IsNullOrEmpty(description));
    }

    [Fact]
    public void Tunnel_flag_combinations_are_accepted()
    {
        foreach (TrackTunnelType flags in new[]
            { TrackTunnelType.Default, TrackTunnelType.In, TrackTunnelType.Out, TrackTunnelType.InOut })
        {
            Blueprint blueprint = TestBlueprints.Valid();
            blueprint.Parts[2].TunnelType = flags;

            Assert.Null(Validator.Validate(blueprint));
        }
    }

    [Fact]
    public void Limits_are_inclusive()
    {
        Blueprint blueprint = TestBlueprints.Valid();
        blueprint.Size = new Xyz(256, 256, 256);
        blueprint.Rails[0].AssetUri = new string('a', 128);

        Assert.Null(Validator.Validate(blueprint));
    }

    private static void Repeat<T>(List<T> list, int count)
    {
        T first = list[0];
        while (list.Count < count)
            list.Add(first);
    }

    private static TerrainColumn Column(int x, int z, int relHeight) =>
        new() { X = x, Z = z, RelHeight = relHeight };
}
