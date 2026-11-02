using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BlueprintMod.Models;
using VoxelTycoon;
using Xunit;

namespace BlueprintMod.Tests;

public class SerializerTests
{
    [Fact]
    public void Round_trip_keeps_everything()
    {
        Blueprint original = TestBlueprints.Valid();

        Blueprint loaded = Serializer.Parse("Test", [.. Serializer.Serialize(original)]);

        Assert.Equal(original.Size, loaded.Size);
        Assert.Equal(original.Rails.Count, loaded.Rails.Count);
        for (int i = 0; i < original.Rails.Count; i++)
        {
            RailEntry a = original.Rails[i];
            RailEntry b = loaded.Rails[i];
            Assert.Equal(
                (a.AssetUri, a.Type, a.Rotation, a.WithEmbankment, a.Electrification, a.DisallowSignals, a.Position, a.Size, a.Parent),
                (b.AssetUri, b.Type, b.Rotation, b.WithEmbankment, b.Electrification, b.DisallowSignals, b.Position, b.Size, b.Parent));
        }

        Assert.Equal(original.Parts.Count, loaded.Parts.Count);
        for (int i = 0; i < original.Parts.Count; i++)
        {
            PartEntry a = original.Parts[i];
            PartEntry b = loaded.Parts[i];
            Assert.Equal(
                (a.Kind, a.AssetUri, a.Rotation, a.BridgeType, a.RailType, a.Height, a.TunnelType, a.Position, a.Size, a.Parent),
                (b.Kind, b.AssetUri, b.Rotation, b.BridgeType, b.RailType, b.Height, b.TunnelType, b.Position, b.Size, b.Parent));
        }

        Assert.Equal(
            original.Signals.Select(s => (s.AssetUri, s.RailIndex, s.ConnectionIndex)),
            loaded.Signals.Select(s => (s.AssetUri, s.RailIndex, s.ConnectionIndex)));
        Assert.Equal(
            original.Terrain.Select(c => (c.X, c.Z, c.RelHeight)),
            loaded.Terrain.Select(c => (c.X, c.Z, c.RelHeight)));
    }

    [Fact]
    public void Name_comes_from_the_caller()
    {
        Blueprint loaded = Serializer.Parse("My junction", [.. Serializer.Serialize(TestBlueprints.Valid())]);

        Assert.Equal("My junction", loaded.Name);
    }

    [Fact]
    public void File_starts_with_header_and_size()
    {
        List<string> lines = Serializer.Serialize(TestBlueprints.Valid());

        Assert.Equal("VTBP2", lines[0]);
        Assert.Equal("10 4 6", lines[1]);
    }

    [Fact]
    public void Serializing_twice_gives_the_same_lines()
    {
        Blueprint blueprint = TestBlueprints.Valid();

        Assert.Equal(Serializer.Serialize(blueprint), Serializer.Serialize(blueprint));
    }

    [Fact]
    public void Numbers_are_written_culture_independent()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            // Swedish uses a real minus sign (U+2212) for negative numbers, which int.Parse would refuse.
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            Blueprint blueprint = TestBlueprints.Valid();

            List<string> lines = Serializer.Serialize(blueprint);

            Assert.DoesNotContain(lines, line => line.Contains('−'));
            Blueprint loaded = Serializer.Parse("Test", [.. lines]);
            Assert.Equal(-2, loaded.Terrain[1].RelHeight);
            Assert.Equal(-1, loaded.Parts[1].Position.Y);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Files_of_the_first_version_have_no_parent_index()
    {
        string[] lines =
        [
            "VTBP1",
            "10 4 6",
            "rail base/regular.rail 1 0 0 0 0 1 0 1 3 1 1",
        ];

        Blueprint loaded = Serializer.Parse("Old", lines);

        Assert.Equal(-1, loaded.Rails[0].Parent);
        Assert.Empty(loaded.Terrain);
    }

    [Fact]
    public void Unknown_lines_are_ignored()
    {
        List<string> lines = Serializer.Serialize(TestBlueprints.Valid());
        lines.Add("ground 3");
        lines.Add("something from a newer version 1 2 3");

        Blueprint loaded = Serializer.Parse("Test", [.. lines]);

        Assert.Equal(3, loaded.Rails.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello world")]
    [InlineData("VTBP3")]
    [InlineData("vtbp2")]
    public void Unknown_header_is_rejected(string header)
    {
        Assert.Throws<FormatException>(() => Serializer.Parse("x", [header, "10 4 6"]));
    }

    [Fact]
    public void Empty_file_is_rejected()
    {
        Assert.Throws<FormatException>(() => Serializer.Parse("x", []));
        Assert.Throws<FormatException>(() => Serializer.Parse("x", ["VTBP2"]));
    }

    [Fact]
    public void Text_instead_of_a_number_is_rejected()
    {
        Assert.Throws<FormatException>(() => Serializer.Parse("x", ["VTBP2", "10 five 6"]));
    }

    [Fact]
    public void Number_out_of_range_is_rejected()
    {
        Assert.Throws<OverflowException>(() => Serializer.Parse("x", ["VTBP2", "99999999999 4 6"]));
    }

    [Fact]
    public void Line_with_too_few_values_is_rejected()
    {
        Assert.Throws<IndexOutOfRangeException>(() => Serializer.Parse("x", ["VTBP2", "10 4 6", "rail base/x 1"]));
        Assert.Throws<IndexOutOfRangeException>(() => Serializer.Parse("x", ["VTBP2", "10 4"]));
    }

    [Fact]
    public void Too_many_lines_are_rejected()
    {
        List<string> lines = ["VTBP2", "10 4 6"];
        lines.AddRange(Enumerable.Repeat("terrain 1 1 0", Serializer.MaxLines));

        Assert.Throws<FormatException>(() => Serializer.Parse("x", [.. lines]));
    }

    [Fact]
    public void Too_long_line_is_rejected()
    {
        string[] lines = ["VTBP2", "10 4 6", "evil " + new string('x', Serializer.MaxLineLength)];

        Assert.Throws<FormatException>(() => Serializer.Parse("x", lines));
    }

    [Fact]
    public void Content_is_validated_after_parsing()
    {
        // Parent of part 0 is part 0: would be a cycle in the game.
        string[] lines =
        [
            "VTBP2",
            "10 4 6",
            "tunnel base/regular.tunnel 0 0 0 0 0 3 3 1 0",
        ];

        FormatException error = Assert.Throws<FormatException>(() => Serializer.Parse("x", lines));

        Assert.Equal("invalid part parent", error.Message);
    }

    [Fact]
    public void Asset_names_are_only_looked_up_never_used_as_paths()
    {
        string[] lines =
        [
            "VTBP2",
            "10 4 6",
            "rail ../../../etc/passwd 1 0 0 0 0 1 0 1 3 1 1 -1",
        ];

        Blueprint loaded = Serializer.Parse("x", lines);

        Assert.Equal("../../../etc/passwd", loaded.Rails[0].AssetUri);
    }
}
