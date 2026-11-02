using System.Collections.Generic;
using VoxelTycoon;

namespace BlueprintMod;

/// <summary>Target heights for the terrain columns that have to change when a blueprint is placed.</summary>
internal sealed class TerrainPlan
{
    // Dictionary expressions need a newer C# version than the one this project targets.
#pragma warning disable IDE0028
    private readonly Dictionary<Xz, int> _heights = new();
#pragma warning restore IDE0028
    private readonly List<Xz> _order = [];

    public IEnumerable<KeyValuePair<Xz, int>> Targets
    {
        get
        {
            foreach (Xz xz in _order)
                yield return new KeyValuePair<Xz, int>(xz, _heights[xz]);
        }
    }

    /// <summary>Sets the target height of a column; later calls can only lower or only raise it.</summary>
    public void Set(Xz column, int height, bool onlyIfLower = false, bool onlyIfHigher = false)
    {
        if (_heights.TryGetValue(column, out int current))
        {
            if ((!onlyIfLower || height < current) && (!onlyIfHigher || height > current))
                _heights[column] = height;
        }
        else
        {
            _heights[column] = height;
            _order.Add(column);
        }
    }
}
