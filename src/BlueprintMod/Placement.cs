using System.Collections.Generic;
using BlueprintMod.Models;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.Buildings;
using VoxelTycoon.Tools;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;

namespace BlueprintMod;

/// <summary>
/// Previews a blueprint as tinted ghost buildings, works out price and validity, and builds it.
/// Call <see cref="Preview"/> every frame; it only does work when something changed.
/// </summary>
internal sealed class Placement
{
    private readonly List<Rail> _rails = [];

    // Blueprint part index per ghost rail, -1 if there is none.
    private readonly List<int> _railParents = [];
    private readonly List<bool> _railOnBridge = [];

    // Same order as Blueprint.Parts, null if the asset is not available.
    private readonly List<Building> _parts = [];

    private TerrainPlan _terrain = new();

    private Blueprint _blueprint;
    private PlacementTransform _transform;
    private bool _adjustTerrain;
    private bool _hasPreview;

    public bool HasGhosts => _rails.Count > 0;
    public bool IsValid { get; private set; }
    public double Price { get; private set; }

    /// <summary>Forces the next <see cref="Preview"/> call to rebuild the ghosts, e.g. after a change.</summary>
    public void Invalidate()
    {
        _hasPreview = false;
    }

    public void Preview(Blueprint blueprint, Xyz origin, BuildingRotation rotation, bool adjustTerrain)
    {
        var transform = new PlacementTransform(blueprint, origin, rotation);
        if (_hasPreview && _blueprint == blueprint && _transform.Origin == origin
            && _transform.Rotation == rotation && _adjustTerrain == adjustTerrain)
            return;

        Clear();
        _blueprint = blueprint;
        _transform = transform;
        _adjustTerrain = adjustTerrain;
        _hasPreview = true;

        IsValid = true;
        Price = 0;
        CreatePartGhosts();
        CreateRailGhosts();
        PlanTerrain();
        ValidateRails();
        AddSignalPrice();
        if (!Company.Current.HasEnoughMoney(Price))
            IsValid = false;
    }

    /// <summary>Removes all ghosts.</summary>
    public void Clear()
    {
        foreach (Rail rail in _rails)
        {
            if (!rail.IsBuilt)
                rail.Remove();
        }
        foreach (Building part in _parts)
        {
            if (part != null && !part.IsBuilt)
                part.Remove();
        }
        ForgetGhosts();
        IsValid = false;
    }

    /// <summary>Builds the previewed blueprint. The caller has to pay <see cref="Price"/> before.</summary>
    public bool Build()
    {
        Blueprint blueprint = _blueprint;
        PlacementTransform transform = _transform;
        List<Rail> rails = [.. _rails];
        List<Building> parts = [.. _parts];
        var railParents = new Dictionary<Rail, int>();
        for (int i = 0; i < _rails.Count; i++)
            railParents[_rails[i]] = _railParents[i];
        List<KeyValuePair<Xz, int>> terrainTargets = [.. _terrain.Targets];

        // From here on the ghosts belong to the world.
        ForgetGhosts();
        Invalidate();

        List<Xyz> expectedRailPositions = [];
        foreach (RailEntry saved in blueprint.Rails)
            expectedRailPositions.Add(transform.Apply(saved.Position, saved.Size));

        foreach (KeyValuePair<Xz, int> target in terrainTargets)
        {
            var column = new Xyz(target.Key.X, target.Value, target.Key.Z);
            Manager<WorldManager>.Current.FlattenHeight(column, emitDust: true);
        }

        foreach (Rail rail in rails)
            rail.SetTint(null);

        if (!LazyManager<RailRebuilder>.Current.TryRebuildImmediately(rails, []))
        {
            foreach (Building part in parts)
                part?.Remove();
            return false;
        }

        foreach (Rail rail in rails)
            rail.Company = Company.Current;

        BuildParts(parts);
        LinkParents(blueprint, rails, railParents, parts);
        PlaceSignals(blueprint, transform, expectedRailPositions);
        return true;
    }

    private void ForgetGhosts()
    {
        _rails.Clear();
        _railParents.Clear();
        _railOnBridge.Clear();
        _parts.Clear();
        _terrain = new TerrainPlan();
        _hasPreview = false;
    }

    /// <summary>Parts come first: rails need to know whether they sit on a bridge or in a tunnel.</summary>
    private void CreatePartGhosts()
    {
        foreach (PartEntry saved in _blueprint.Parts)
        {
            Building part = CreatePart(saved);
            _parts.Add(part);
            if (part == null)
            {
                // The asset (for example from another mod) is not available.
                IsValid = false;
                continue;
            }

            part.Place(_transform.Apply(saved.Position, saved.Size));
            Price += part.Price;
            bool allowed = IsInUnlockedRegion(part);
            IsValid &= allowed;
            Tint(part, allowed);
        }
    }

    private void CreateRailGhosts()
    {
        for (int i = 0; i < _blueprint.Rails.Count; i++)
        {
            RailEntry saved = _blueprint.Rails[i];
            Rail rail = CreateRail(saved);
            if (rail == null)
            {
                IsValid = false;
                continue;
            }

            rail.Place(_transform.Apply(saved.Position, saved.Size));
            if (saved.Parent >= 0)
                // Keep rails on bridges and tunnels as they are.
                rail.RebuildMode = RebuildMode.Immutable;
            rail.RebuildIndex = i + 1;

            _rails.Add(rail);
            _railParents.Add(saved.Parent);
            _railOnBridge.Add(IsBridge(saved.Parent));

            Price += rail.Price;
            if (saved.Electrification != RailElectrificationMode.None)
                Price += rail.ElectrificationPrice;
        }
    }

    private void PlanTerrain()
    {
        if (!_adjustTerrain)
            return;

        _terrain = _blueprint.Terrain.Count > 0
            ? TerrainPlanner.FromCapturedTerrain(_blueprint, _transform)
            : TerrainPlanner.FromBuildingRules(_rails, _railOnBridge, _parts);

        WorldManager world = Manager<WorldManager>.Current;
        foreach (KeyValuePair<Xz, int> target in _terrain.Targets)
        {
            // Don't touch (or even generate) terrain in regions the player can't build in.
            if (!Manager<RegionManager>.Current.IsUnlocked(target.Key))
            {
                IsValid = false;
                continue;
            }

            Xyz surface = world.GetSurface(target.Key);
            IsValid &= FlattenHelper.CanFlatten(surface, target.Value);
            Price += ToolHelper.GetFlattenPrice(surface, target.Value);
        }
    }

    private void ValidateRails()
    {
        for (int i = 0; i < _rails.Count; i++)
        {
            bool allowed = CanBuild(_rails[i], _railOnBridge[i]);
            IsValid &= allowed;
            Tint(_rails[i], allowed);
        }
    }

    private void AddSignalPrice()
    {
        foreach (SignalEntry saved in _blueprint.Signals)
        {
            if (Manager<AssetLibrary>.Current.TryGetAsset(saved.AssetUri, out _, out RailSignal signal))
                Price += signal.SharedData.Price;
        }
    }

    private bool IsBridge(int partIndex)
    {
        return partIndex >= 0 && _blueprint.Parts[partIndex].Kind == PartKind.Bridge;
    }

    private static void Tint(Building building, bool allowed)
    {
        building.SetTint(allowed ? GameColors.BuildAllowedTintColor : GameColors.BuildBlockedTintColor);
    }

    private Rail CreateRail(RailEntry saved)
    {
        // The asset name comes from a file: only accept assets that really are rails.
        if (!Manager<AssetLibrary>.Current.TryGetAsset(saved.AssetUri, out int assetId, out Rail _))
            return null;

        Rail rail = LazyManager<RailManager>.Current.Take(
            assetId, saved.Rotation.Add(_transform.Rotation), saved.Type, saved.WithEmbankment);
        rail.DisallowSignals = saved.DisallowSignals;
        rail.ElectrificationMode = saved.Electrification;
        return rail;
    }

    private Building CreatePart(PartEntry saved)
    {
        BuildingRotation rotation = saved.Rotation.Add(_transform.Rotation);
        if (saved.Kind == PartKind.Bridge)
        {
            if (!Manager<AssetLibrary>.Current.TryGetAsset(saved.AssetUri, out _, out RailBridge bridgeAsset))
                return null;
            RailBridge bridge = Object.Instantiate(bridgeAsset);
            bridge.SetType(saved.BridgeType, saved.RailType, saved.Height);
            bridge.SetRotation(rotation);
            return bridge;
        }

        if (!Manager<AssetLibrary>.Current.TryGetAsset(saved.AssetUri, out _, out RailTunnel tunnelAsset))
            return null;
        RailTunnel tunnel = Object.Instantiate(tunnelAsset);
        tunnel.SetType(saved.TunnelType);
        tunnel.SetRotation(rotation);
        return tunnel;
    }

    private static bool IsInUnlockedRegion(Building part)
    {
        using PooledList<Xyz> positions = part.TakeXyzPositions();
        foreach (Xyz xyz in positions)
        {
            if (xyz.Y == part.Position.Y && !Manager<RegionManager>.Current.IsUnlocked((Xz)xyz))
                return false;
        }
        return true;
    }

    private bool CanBuild(Rail ghost, bool onBridge)
    {
        WorldManager world = Manager<WorldManager>.Current;
        using PooledList<Xyz> positions = ghost.TakeXyzPositions();
        foreach (Xyz xyz in positions)
        {
            if (xyz.Y == ghost.Position.Y && !CanStandOnColumn(ghost, xyz, onBridge, world))
                return false;

            using var buildings = PooledList<Building>.Take();
            world.GetBuildingList(xyz, buildings);
            foreach (Building other in buildings)
            {
                if (other != ghost && other is not Plant)
                    return false;
            }
        }
        return true;
    }

    private bool CanStandOnColumn(Rail ghost, Xyz position, bool onBridge, WorldManager world)
    {
        var column = (Xz)position;
        if (!Manager<RegionManager>.Current.IsUnlocked(column))
            return false;

        // With terrain adjustment the ground is reshaped, and bridges don't touch the ground at all.
        if (_adjustTerrain || onBridge)
            return true;

        return world.GetHeight(column) < ghost.Position.Y
            && !world.GetVoxel(new Xyz(position.X, position.Y - 1, position.Z)).IsLiquid;
    }

    private static void BuildParts(List<Building> parts)
    {
        foreach (Building part in parts)
        {
            if (part == null)
                continue;
            part.SetTint(null);
            part.Build(part.Position);
            part.Company = Company.Current;
        }
    }

    private static void LinkParents(
        Blueprint blueprint, List<Rail> builtRails, Dictionary<Rail, int> railParents, List<Building> parts)
    {
        foreach (Rail rail in builtRails)
        {
            // The rebuilder may have replaced rails; replacements have no entry and keep no parent.
            if (railParents.TryGetValue(rail, out int parent) && parent >= 0 && parts[parent] != null && rail.IsBuilt)
                rail.Parent = parts[parent];
        }

        for (int i = 0; i < blueprint.Parts.Count; i++)
        {
            int parent = blueprint.Parts[i].Parent;
            if (parent >= 0 && parts[i] != null && parts[parent] != null)
                parts[i].Parent = parts[parent];
        }
    }

    private static void PlaceSignals(Blueprint blueprint, PlacementTransform transform, List<Xyz> expectedRailPositions)
    {
        foreach (SignalEntry saved in blueprint.Signals)
        {
            if (!Manager<AssetLibrary>.Current.TryGetAsset(saved.AssetUri, out _, out RailSignal asset))
                continue;

            Rail rail = FindBuiltRail(expectedRailPositions[saved.RailIndex]);
            if (rail == null)
                continue;

            // The rebuilder turns rails by 180 degrees when it swaps straight pieces for end caps.
            RailEntry savedRail = blueprint.Rails[saved.RailIndex];
            bool flipped = rail.Rotation != savedRail.Rotation.Add(transform.Rotation) && rail.ConnectionCount == 2;
            int connectionIndex = flipped ? 1 - saved.ConnectionIndex : saved.ConnectionIndex;
            if (connectionIndex >= rail.ConnectionCount)
                continue;

            RailConnection connection = rail.GetConnection(connectionIndex);
            if (!connection.CanSetSignal())
                continue;

            RailSignal signal = Object.Instantiate(asset);
            signal.Place(connection);
            signal.Build(signal.Connection);
        }
    }

    private static Rail FindBuiltRail(Xyz position)
    {
        using var buildings = PooledList<Building>.Take();
        Manager<WorldManager>.Current.GetBuildingList(position, buildings);
        foreach (Building building in buildings)
        {
            if (building is Rail rail && rail.IsBuilt && rail.Position == position)
                return rail;
        }
        return null;
    }
}
