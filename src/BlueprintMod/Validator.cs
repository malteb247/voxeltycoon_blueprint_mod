using System;
using BlueprintMod.Models;
using VoxelTycoon;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;

namespace BlueprintMod;

/// <summary>
/// Checks a <see cref="Blueprint"/> before it is used. Blueprint files can come from other people, so nothing in
/// them is trusted: sizes and counts are limited, values must be in range, enums must be defined, and indices must
/// point to existing entries (parent links must not form cycles, which would hang the game).
/// </summary>
internal static class Validator
{
    public const int MaxRails = 2048;
    public const int MaxParts = 2048;
    public const int MaxSignals = 4096;
    public const int MaxTerrainColumns = 16384;

    private const int MaxExtentXz = 256;
    private const int MaxHeight = 256;
    private const int MaxFootprint = 128;
    private const int MaxAssetUriLength = 128;
    private const int ConnectionsPerRail = 2;

    /// <returns>A description of the first problem, or null if the blueprint is valid.</returns>
    public static string Validate(Blueprint blueprint)
    {
        return ValidateHeader(blueprint)
            ?? ValidateRails(blueprint)
            ?? ValidateParts(blueprint)
            ?? ValidateSignals(blueprint)
            ?? ValidateTerrain(blueprint);
    }

    private static string ValidateHeader(Blueprint blueprint)
    {
        if (string.IsNullOrEmpty(blueprint.Name) || blueprint.Name.IndexOfAny(['<', '>']) >= 0)
            return "invalid name";
        if (blueprint.Size.X < 1 || blueprint.Size.X > MaxExtentXz
            || blueprint.Size.Z < 1 || blueprint.Size.Z > MaxExtentXz
            || blueprint.Size.Y < 1 || blueprint.Size.Y > MaxHeight)
            return "invalid size";
        if (blueprint.Rails.Count > MaxRails || blueprint.Parts.Count > MaxParts
            || blueprint.Signals.Count > MaxSignals || blueprint.Terrain.Count > MaxTerrainColumns)
            return "too many entries";
        return null;
    }

    private static string ValidateRails(Blueprint blueprint)
    {
        foreach (RailEntry rail in blueprint.Rails)
        {
            if (!IsValidAssetUri(rail.AssetUri))
                return "invalid rail asset";
            if (!Enum.IsDefined(typeof(RailType), rail.Type) || rail.Type == RailType.Unknown
                || !Enum.IsDefined(typeof(BuildingRotation), rail.Rotation)
                || !Enum.IsDefined(typeof(RailElectrificationMode), rail.Electrification))
                return "invalid rail value";
            if (!IsValidPlacement(blueprint, rail.Position, rail.Size))
                return "rail outside of the blueprint";
            if (rail.Parent < -1 || rail.Parent >= blueprint.Parts.Count)
                return "invalid rail parent";
        }
        return null;
    }

    private static string ValidateParts(Blueprint blueprint)
    {
        for (int i = 0; i < blueprint.Parts.Count; i++)
        {
            PartEntry part = blueprint.Parts[i];
            if (!IsValidAssetUri(part.AssetUri))
                return "invalid part asset";
            if (!Enum.IsDefined(typeof(PartKind), part.Kind)
                || !Enum.IsDefined(typeof(BuildingRotation), part.Rotation)
                || !Enum.IsDefined(typeof(TrackBridgeType), part.BridgeType)
                || !Enum.IsDefined(typeof(RailType), part.RailType)
                || (part.TunnelType & ~TrackTunnelType.InOut) != 0
                || part.Height < 0 || part.Height > MaxHeight)
                return "invalid part value";
            if (!IsValidPlacement(blueprint, part.Position, part.Size))
                return "part outside of the blueprint";

            // Parents always come first. This also rules out cycles, which would make the game loop forever.
            if (part.Parent < -1 || part.Parent >= i)
                return "invalid part parent";
        }
        return null;
    }

    private static string ValidateSignals(Blueprint blueprint)
    {
        foreach (SignalEntry signal in blueprint.Signals)
        {
            if (!IsValidAssetUri(signal.AssetUri))
                return "invalid signal asset";
            if (signal.RailIndex < 0 || signal.RailIndex >= blueprint.Rails.Count
                || signal.ConnectionIndex < 0 || signal.ConnectionIndex >= ConnectionsPerRail)
                return "invalid signal";
        }
        return null;
    }

    private static string ValidateTerrain(Blueprint blueprint)
    {
        foreach (TerrainColumn column in blueprint.Terrain)
        {
            if (column.X < 0 || column.X >= blueprint.Size.X || column.Z < 0 || column.Z >= blueprint.Size.Z
                || column.RelHeight < -MaxHeight || column.RelHeight > MaxHeight)
                return "invalid terrain column";
        }
        return null;
    }

    private static bool IsValidPlacement(Blueprint blueprint, Xyz position, Xyz size)
    {
        return position.X >= 0 && position.X <= blueprint.Size.X
            && position.Z >= 0 && position.Z <= blueprint.Size.Z
            && position.Y >= -MaxHeight && position.Y <= MaxHeight
            && size.X >= 1 && size.X <= MaxFootprint
            && size.Y >= 1 && size.Y <= MaxFootprint
            && size.Z >= 1 && size.Z <= MaxFootprint;
    }

    private static bool IsValidAssetUri(string uri)
    {
        if (string.IsNullOrEmpty(uri) || uri.Length > MaxAssetUriLength)
            return false;
        foreach (char c in uri)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c))
                return false;
        }
        return true;
    }
}
