using System.Collections.Generic;
using BlueprintMod.Models;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.Buildings;
using VoxelTycoon.Tracks.Rails;

namespace BlueprintMod;

/// <summary>Turns the rails in a rectangle of the world into a <see cref="Blueprint"/>.</summary>
internal static class Capture
{
    /// <returns>The blueprint (not saved yet), or null if there are no rails in the area.</returns>
    public static Blueprint FromArea(Xz min, Xz max)
    {
        List<Rail> rails = FindRails(min, max);
        if (rails.Count == 0)
            return null;

        List<Building> parts = [];
        var partIndex = new Dictionary<Building, int>();
        CollectParts(rails, parts, partIndex);

        Bounds bounds = Measure(rails, parts, out int lowestRailY);
        // Everything is stored relative to the lowest rail, so a blueprint sits on the cursor when placed.
        var origin = new Xyz(bounds.Min.X, lowestRailY, bounds.Min.Z);

        AssetLibrary assets = Manager<AssetLibrary>.Current;
        var blueprint = new Blueprint
        {
            Name = Storage.NextFreeName(),
            Size = bounds.Max - origin
        };

        foreach (Building part in parts)
            blueprint.Parts.Add(SavePart(part, origin, partIndex, assets));

        for (int i = 0; i < rails.Count; i++)
        {
            blueprint.Rails.Add(SaveRail(rails[i], origin, partIndex, assets));
            SaveSignals(rails[i], i, blueprint.Signals, assets);
        }

        SaveTerrain(blueprint, rails, parts, origin);
        return blueprint;
    }

    private static List<Rail> FindRails(Xz min, Xz max)
    {
        ImmutableList<Rail> all = LazyManager<BuildingManager>.Current.GetAll<Rail>();
        List<Rail> result = [];
        for (int i = 0; i < all.Count; i++)
        {
            Xyz p = all[i].Position;
            if (p.X >= min.X && p.X <= max.X && p.Z >= min.Z && p.Z <= max.Z)
                result.Add(all[i]);
        }
        return result;
    }

    /// <summary>Collects the bridges/tunnels carrying the rails, plus everything attached to them (piers).</summary>
    private static void CollectParts(List<Rail> rails, List<Building> parts, Dictionary<Building, int> index)
    {
        foreach (Rail rail in rails)
            AddPart(rail.Parent, parts, index);

        for (int i = 0; i < parts.Count; i++)
        {
            for (int c = 0; c < parts[i].ChildrenCount; c++)
                AddPart(parts[i].GetChild(c), parts, index);
        }
    }

    private static void AddPart(Building candidate, List<Building> parts, Dictionary<Building, int> index)
    {
        if (candidate == null || index.ContainsKey(candidate) || candidate is not (RailBridge or RailTunnel))
            return;

        // Add parents first, so their index is smaller.
        AddPart(candidate.Parent, parts, index);
        if (index.ContainsKey(candidate))
            return;
        index.Add(candidate, parts.Count);
        parts.Add(candidate);
    }

    private struct Bounds
    {
        public Xyz Min { get; set; }

        public Xyz Max { get; set; }
    }

    private static Bounds Measure(List<Rail> rails, List<Building> parts, out int lowestRailY)
    {
        var bounds = new Bounds
        {
            Min = new Xyz(int.MaxValue, int.MaxValue, int.MaxValue),
            Max = new Xyz(int.MinValue, int.MinValue, int.MinValue)
        };
        lowestRailY = int.MaxValue;

        foreach (Rail rail in rails)
        {
            Include(ref bounds, rail);
            lowestRailY = Mathf.Min(lowestRailY, rail.Position.Y);
        }
        foreach (Building part in parts)
            Include(ref bounds, part);
        return bounds;
    }

    private static void Include(ref Bounds bounds, Building building)
    {
        bounds.Min = Xyz.Min(bounds.Min, building.Position);
        bounds.Max = Xyz.Max(bounds.Max, building.Position + building.Size);
    }

    private static PartEntry SavePart(
        Building part, Xyz origin, Dictionary<Building, int> partIndex, AssetLibrary assets)
    {
        var saved = new PartEntry
        {
            AssetUri = assets.GetAssetUri(part.AssetId),
            Rotation = part.Rotation,
            Position = part.Position - origin,
            Size = part.Size,
            Parent = IndexOf(part.Parent, partIndex)
        };

        if (part is RailBridge bridge)
        {
            saved.Kind = PartKind.Bridge;
            saved.BridgeType = bridge.Type;
            saved.RailType = bridge.RailType;
            saved.Height = bridge.Height;
        }
        else
        {
            saved.Kind = PartKind.Tunnel;
            saved.TunnelType = ((RailTunnel)part).Type;
        }
        return saved;
    }

    private static RailEntry SaveRail(
        Rail rail, Xyz origin, Dictionary<Building, int> partIndex, AssetLibrary assets)
    {
        return new RailEntry
        {
            AssetUri = assets.GetAssetUri(rail.AssetId),
            Type = rail.Type,
            Rotation = rail.Rotation,
            WithEmbankment = rail.WithEmbankment,
            Electrification = rail.ElectrificationMode,
            DisallowSignals = rail.DisallowSignals,
            Position = rail.Position - origin,
            Size = rail.Size,
            Parent = IndexOf(rail.Parent, partIndex)
        };
    }

    private static void SaveSignals(Rail rail, int railIndex, List<SignalEntry> signals, AssetLibrary assets)
    {
        for (int c = 0; c < rail.ConnectionCount; c++)
        {
            RailSignal signal = rail.GetConnection(c).Signal;
            if (signal == null)
                continue;
            signals.Add(new SignalEntry
            {
                AssetUri = assets.GetAssetUri(signal.SharedData.AssetId),
                RailIndex = railIndex,
                ConnectionIndex = c
            });
        }
    }

    /// <summary>Stores the terrain height under every column the blueprint covers.</summary>
    private static void SaveTerrain(Blueprint blueprint, List<Rail> rails, List<Building> parts, Xyz origin)
    {
        HashSet<Xz> columns = [];
        foreach (Rail rail in rails)
            AddColumns(rail, columns);
        foreach (Building part in parts)
            AddColumns(part, columns);

        WorldManager world = Manager<WorldManager>.Current;
        foreach (Xz xz in columns)
        {
            blueprint.Terrain.Add(new TerrainColumn
            {
                X = xz.X - origin.X,
                Z = xz.Z - origin.Z,
                RelHeight = world.GetHeight(xz) - origin.Y
            });
        }
    }

    private static void AddColumns(Building building, HashSet<Xz> columns)
    {
        using PooledList<Xz> xzs = building.TakeXzPositions();
        foreach (Xz xz in xzs)
            columns.Add(xz);
    }

    private static int IndexOf(Building building, Dictionary<Building, int> index)
    {
        return building != null && index.TryGetValue(building, out int i) ? i : -1;
    }
}
