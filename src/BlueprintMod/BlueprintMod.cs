using VoxelTycoon;
using VoxelTycoon.Game.UI.ModernUI;
using VoxelTycoon.Modding;
using VoxelTycoon.UI;

namespace BlueprintMod;

/// <summary>Entry point of the mod: adds the blueprint tool to the toolbar.</summary>
public class BlueprintMod : Mod
{
    protected override void OnGameStarted()
    {
        Toolbar.Current.AddButton(
            FontIcon.FaSolid("\uf5fd"),
            "Rail blueprints",
            new ToolToolbarAction(() => new BlueprintTool()));
    }
}
