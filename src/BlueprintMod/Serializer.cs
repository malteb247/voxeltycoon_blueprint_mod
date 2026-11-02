using System;
using System.Collections.Generic;
using System.Globalization;
using BlueprintMod.Models;
using VoxelTycoon;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;

namespace BlueprintMod;

/// <summary>
/// Converts a <see cref="Blueprint"/> to and from the lines of a <c>.vtbp</c> text file. Has no file access, so it
/// can be tested on its own. Unknown line types are ignored, so files stay loadable across versions.
/// </summary>
internal static class Serializer
{
    public const int MaxLines = 30000;
    public const int MaxLineLength = 512;

    private const string CurrentHeader = "VTBP2";
    private const string LegacyHeader = "VTBP1";

    public static List<string> Serialize(Blueprint blueprint)
    {
        var lines = new List<string>
        {
            CurrentHeader,
            Join(blueprint.Size.X, blueprint.Size.Y, blueprint.Size.Z)
        };

        foreach (RailEntry r in blueprint.Rails)
        {
            lines.Add(Join("rail", r.AssetUri, (int)r.Type, (int)r.Rotation, Flag(r.WithEmbankment),
                (int)r.Electrification, Flag(r.DisallowSignals),
                r.Position.X, r.Position.Y, r.Position.Z, r.Size.X, r.Size.Y, r.Size.Z, r.Parent));
        }

        foreach (PartEntry p in blueprint.Parts)
        {
            lines.Add(p.Kind == PartKind.Bridge
                ? Join("bridge", p.AssetUri, (int)p.Rotation, (int)p.BridgeType, (int)p.RailType, p.Height,
                    p.Position.X, p.Position.Y, p.Position.Z, p.Size.X, p.Size.Y, p.Size.Z, p.Parent)
                : Join("tunnel", p.AssetUri, (int)p.Rotation, (int)p.TunnelType,
                    p.Position.X, p.Position.Y, p.Position.Z, p.Size.X, p.Size.Y, p.Size.Z, p.Parent));
        }

        foreach (TerrainColumn c in blueprint.Terrain)
            lines.Add(Join("terrain", c.X, c.Z, c.RelHeight));

        foreach (SignalEntry s in blueprint.Signals)
            lines.Add(Join("signal", s.AssetUri, s.RailIndex, s.ConnectionIndex));

        return lines;
    }

    /// <summary>Parses and validates the lines of a blueprint file.</summary>
    /// <exception cref="FormatException">The file is malformed, too large or fails validation.</exception>
    /// <exception cref="OverflowException">A number is out of range.</exception>
    /// <exception cref="IndexOutOfRangeException">A line has too few values.</exception>
    public static Blueprint Parse(string name, string[] lines)
    {
        if (lines.Length > MaxLines || Array.Exists(lines, line => line.Length > MaxLineLength))
            throw new FormatException("file too large");
        if (lines.Length < 2 || (lines[0] != CurrentHeader && lines[0] != LegacyHeader))
            throw new FormatException("unknown format");

        int[] size = Ints(lines[1].Split(' '), 0, 3);
        var blueprint = new Blueprint
        {
            Name = name,
            Size = new Xyz(size[0], size[1], size[2])
        };

        for (int i = 2; i < lines.Length; i++)
        {
            string[] p = lines[i].Split(' ');
            switch (p[0])
            {
                case "rail":
                    blueprint.Rails.Add(ParseRail(p));
                    break;
                case "bridge":
                    blueprint.Parts.Add(ParseBridge(p));
                    break;
                case "tunnel":
                    blueprint.Parts.Add(ParseTunnel(p));
                    break;
                case "terrain":
                    int[] t = Ints(p, 1, 3);
                    blueprint.Terrain.Add(new TerrainColumn { X = t[0], Z = t[1], RelHeight = t[2] });
                    break;
                case "signal":
                    int[] s = Ints(p, 2, 2);
                    blueprint.Signals.Add(
                        new SignalEntry { AssetUri = p[1], RailIndex = s[0], ConnectionIndex = s[1] });
                    break;
            }
        }

        string error = Validator.Validate(blueprint);
        if (error != null)
            throw new FormatException(error);

        return blueprint;
    }

    private static RailEntry ParseRail(string[] p)
    {
        int[] n = Ints(p, 2, 11);
        return new RailEntry
        {
            AssetUri = p[1],
            Type = (RailType)n[0],
            Rotation = (BuildingRotation)n[1],
            WithEmbankment = n[2] == 1,
            Electrification = (RailElectrificationMode)n[3],
            DisallowSignals = n[4] == 1,
            Position = new Xyz(n[5], n[6], n[7]),
            Size = new Xyz(n[8], n[9], n[10]),

            // The parent index is missing in files of the first version.
            Parent = p.Length > 13 ? int.Parse(p[13], CultureInfo.InvariantCulture) : -1,
        };
    }

    private static PartEntry ParseBridge(string[] p)
    {
        int[] n = Ints(p, 2, 11);
        return new PartEntry
        {
            Kind = PartKind.Bridge,
            AssetUri = p[1],
            Rotation = (BuildingRotation)n[0],
            BridgeType = (TrackBridgeType)n[1],
            RailType = (RailType)n[2],
            Height = n[3],
            Position = new Xyz(n[4], n[5], n[6]),
            Size = new Xyz(n[7], n[8], n[9]),
            Parent = n[10]
        };
    }

    private static PartEntry ParseTunnel(string[] p)
    {
        int[] n = Ints(p, 2, 9);
        return new PartEntry
        {
            Kind = PartKind.Tunnel,
            AssetUri = p[1],
            Rotation = (BuildingRotation)n[0],
            TunnelType = (TrackTunnelType)n[1],
            Position = new Xyz(n[2], n[3], n[4]),
            Size = new Xyz(n[5], n[6], n[7]),
            Parent = n[8]
        };
    }

    private static int Flag(bool value) => value ? 1 : 0;

    // Numbers must be written the way int.Parse(InvariantCulture) reads them, whatever language the system uses.
    private static string Join(params object[] values) =>
        string.Join(" ", Array.ConvertAll(values, value => Convert.ToString(value, CultureInfo.InvariantCulture)));

    private static int[] Ints(string[] parts, int start, int count)
    {
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = int.Parse(parts[start + i], CultureInfo.InvariantCulture);
        return result;
    }
}
