using System.Collections.Generic;
using BlueprintMod.Models;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.Buildings;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;

namespace BlueprintMod;

/// <summary>Decides how the terrain has to be reshaped for a placed blueprint.</summary>
internal static class TerrainPlanner
{
    /// <summary>Recreates the terrain shape that was saved with the blueprint.</summary>
    public static TerrainPlan FromCapturedTerrain(Blueprint blueprint, PlacementTransform transform)
    {
        var plan = new TerrainPlan();
        foreach (TerrainColumn column in blueprint.Terrain)
            plan.Set(transform.Apply(column), transform.Origin.Y + column.RelHeight);
        return plan;
    }

    /// <summary>
    /// For blueprints without saved terrain (older versions): same rules as the game's rail tool.
    /// Rails get level ground, bridge piers rest on the ground and tunnels need earth on top.
    /// </summary>
    public static TerrainPlan FromBuildingRules(
        IList<Rail> rails, IList<bool> railIsOnBridge, IEnumerable<Building> parts)
    {
        var plan = new TerrainPlan();

        for (int i = 0; i < rails.Count; i++)
        {
            if (railIsOnBridge[i])
                continue;
            using PooledList<Xz> columns = rails[i].TakeXzPositions();
            foreach (Xz xz in columns)
                plan.Set(xz, rails[i].Position.Y - 1, onlyIfLower: true);
        }

        foreach (Building part in parts)
        {
            if (part == null)
                continue;
            using PooledList<Xz> columns = part.TakeXzPositions();
            foreach (Xz xz in columns)
                AddPartRule(plan, part, xz);
        }
        return plan;
    }

    private static void AddPartRule(TerrainPlan plan, Building part, Xz column)
    {
        if (part is RailBridge bridge)
        {
            if (bridge.Type == TrackBridgeType.Pier)
                plan.Set(column, part.Position.Y - 1, onlyIfHigher: true);
            return;
        }

        int tunnelTop = part.Position.Y - 1 + part.Size.Y;
        plan.Set(column, Mathf.Max(Manager<WorldManager>.Current.GetHeight(column), tunnelTop));
    }
}
