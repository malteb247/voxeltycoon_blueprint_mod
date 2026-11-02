using BlueprintMod.Models;
using VoxelTycoon;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;

namespace BlueprintMod.Tests;

/// <summary>Builds blueprints for the tests.</summary>
internal static class TestBlueprints
{
    public const string RailAsset = "base/regular.rail";
    public const string BridgeAsset = "base/regular.bridge";
    public const string TunnelAsset = "base/regular.tunnel";
    public const string SignalAsset = "base/regular.signal";

    /// <summary>A valid 10 x 4 x 6 blueprint that uses every kind of entry.</summary>
    public static Blueprint Valid()
    {
        var blueprint = new Blueprint { Name = "Test", Size = new Xyz(10, 4, 6) };

        // Part 0 is a bridge body, part 1 its pier (below the rails), part 2 a tunnel segment.
        blueprint.Parts.Add(new PartEntry
        {
            Kind = PartKind.Bridge,
            AssetUri = BridgeAsset,
            Rotation = BuildingRotation.Rotate90,
            BridgeType = TrackBridgeType.Body,
            RailType = RailType.Straight,
            Position = new Xyz(4, 1, 2),
            Size = new Xyz(1, 1, 3),
        });
        blueprint.Parts.Add(new PartEntry
        {
            Kind = PartKind.Bridge,
            AssetUri = BridgeAsset,
            Rotation = BuildingRotation.Rotate90,
            BridgeType = TrackBridgeType.Pier,
            RailType = RailType.Straight,
            Height = 2,
            Position = new Xyz(4, -1, 2),
            Size = new Xyz(1, 2, 1),
            Parent = 0,
        });
        blueprint.Parts.Add(new PartEntry
        {
            Kind = PartKind.Tunnel,
            AssetUri = TunnelAsset,
            Rotation = BuildingRotation.Rotate0,
            TunnelType = TrackTunnelType.InOut,
            Position = new Xyz(0, 0, 0),
            Size = new Xyz(3, 3, 1),
        });

        blueprint.Rails.Add(new RailEntry
        {
            AssetUri = RailAsset,
            Type = RailType.Straight,
            Rotation = BuildingRotation.Rotate0,
            Position = new Xyz(0, 0, 0),
            Size = new Xyz(3, 3, 1),
            Parent = 2,
        });
        blueprint.Rails.Add(new RailEntry
        {
            AssetUri = RailAsset,
            Type = RailType.TurnRight,
            Rotation = BuildingRotation.Rotate180,
            WithEmbankment = true,
            Electrification = RailElectrificationMode.Left,
            DisallowSignals = true,
            Position = new Xyz(5, 1, 1),
            Size = new Xyz(3, 3, 4),
        });
        blueprint.Rails.Add(new RailEntry
        {
            AssetUri = RailAsset,
            Type = RailType.Straight,
            Rotation = BuildingRotation.Rotate90,
            Position = new Xyz(4, 1, 2),
            Size = new Xyz(1, 3, 2),
            Parent = 0,
        });

        blueprint.Signals.Add(new SignalEntry { AssetUri = SignalAsset, RailIndex = 1, ConnectionIndex = 1 });
        blueprint.Signals.Add(new SignalEntry { AssetUri = SignalAsset, RailIndex = 2, ConnectionIndex = 0 });

        blueprint.Terrain.Add(new TerrainColumn { X = 0, Z = 0, RelHeight = -1 });
        blueprint.Terrain.Add(new TerrainColumn { X = 4, Z = 2, RelHeight = -2 });
        blueprint.Terrain.Add(new TerrainColumn { X = 9, Z = 5, RelHeight = 3 });

        return blueprint;
    }
}
