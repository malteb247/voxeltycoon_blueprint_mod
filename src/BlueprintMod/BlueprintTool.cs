using System.Collections.Generic;
using BlueprintMod.Models;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Tools;
using VoxelTycoon.UI;

namespace BlueprintMod;

/// <summary>
/// Toolbar tool with two modes (Tab switches):
/// <list type="bullet">
/// <item><description>Capture: drag a rectangle over existing rails to save them as a blueprint.</description></item>
/// <item><description>Place: pick a blueprint, rotate it, move it up or down and click to build.</description></item>
/// </list>
/// </summary>
public class BlueprintTool : ITool
{
    private enum Mode
    {
        Capture,
        Place
    }

    private static readonly Hotkey ToggleModeKey = new(KeyCode.Tab);
    private static readonly Hotkey PreviousKey = new(KeyCode.Q);
    private static readonly Hotkey NextKey = new(KeyCode.E);
    private static readonly Hotkey RotateKey = new(KeyCode.R);
    private static readonly Hotkey DeleteKey = new(KeyCode.Delete);
    private static readonly Hotkey TerrainKey = new(KeyCode.F);
    private static readonly Hotkey PageUpKey = new(KeyCode.PageUp);
    private static readonly Hotkey PageDownKey = new(KeyCode.PageDown);

    private readonly Placement _placement = new();

    private Mode _mode;
    private List<Blueprint> _blueprints = [];
    private int _index;

    // State of the capture mode.
    private AreaSelection _selection;
    private Xz? _dragStart;

    // Settings of the place mode.
    private BuildingRotation _rotation;
    private bool _adjustTerrain = true;
    private int _heightOffset;

    // Entries of the hotkey panel.
    private HotkeyPanelItem _modeItem;
    private HotkeyPanelItem _terrainItem;
    private HotkeyPanelItem _heightItem;

    private Blueprint Current => _blueprints[_index];

    public void Activate()
    {
        _selection = new AreaSelection();
        _blueprints = Storage.LoadAll();
        _mode = _blueprints.Count > 0 ? Mode.Place : Mode.Capture;

        CreateHotkeyPanel();
        UpdateCaptions();
        Announce();
    }

    public bool Deactivate(bool soft)
    {
        _placement.Clear();
        _selection.Clear();
        _selection.Destroy();
        _selection = null;
        ToolHintManager.Current.Hide();
        HotkeyPanel.Current.Clear();
        return true;
    }

    public bool OnUpdate()
    {
        if (ToolHelper.IsHotkeyDown(ToggleModeKey))
            SwitchMode();

        if (_mode == Mode.Capture)
            UpdateCapture();
        else
            UpdatePlace();
        return false;
    }

    private void SwitchMode()
    {
        _mode = _mode == Mode.Capture ? Mode.Place : Mode.Capture;
        _placement.Clear();
        ClearSelection();
        _dragStart = null;
        ToolHintManager.Current.Hide();
        UpdateCaptions();
        Announce();
    }

    private void UpdateCapture()
    {
        var cursor = (Xz)VoxelRaycaster.GetVoxelPosition();
        if (InputHelper.WorldMouseDown)
            _dragStart = cursor;
        if (!_dragStart.HasValue)
            return;

        var min = Xz.Min(_dragStart.Value, cursor);
        var max = Xz.Max(_dragStart.Value, cursor);
        ShowSelection(min, max);

        if (InputHelper.MouseUp)
        {
            _dragStart = null;
            ClearSelection();
            CaptureAndSave(min, max);
        }
    }

    private void ShowSelection(Xz min, Xz max)
    {
        _selection.Clear();
        for (int x = min.X; x <= max.X; x++)
        {
            for (int z = min.Z; z <= max.Z; z++)
                _selection.Select(new Xz(x, z), GameColors.BuildAllowedTintColor);
        }
        _selection.Rebuild();
    }

    private void ClearSelection()
    {
        _selection.Clear();
        _selection.Rebuild();
    }

    private void CaptureAndSave(Xz min, Xz max)
    {
        Blueprint blueprint = Capture.FromArea(min, max);
        if (blueprint == null)
        {
            OverlayMessage.ShowAtScreenCenter("No rails in the selected area.");
            return;
        }

        string error = Validator.Validate(blueprint);
        if (error != null)
        {
            OverlayMessage.ShowAtScreenCenter($"Can't save this area ({error}). Mark a smaller area.");
            return;
        }

        Storage.Save(blueprint);
        _blueprints = Storage.LoadAll();
        _index = Mathf.Max(0, _blueprints.FindIndex(b => b.Name == blueprint.Name));

        string summary = $"{blueprint.Rails.Count} rails, {blueprint.Parts.Count} bridge/tunnel parts, " +
            $"{blueprint.Signals.Count} signals";
        OverlayMessage.ShowAtScreenCenter($"Saved \"{blueprint.Name}\" ({summary}).\nPress Tab to place it.");
    }

    private void UpdatePlace()
    {
        if (_blueprints.Count == 0)
        {
            _placement.Clear();
            return;
        }

        HandlePlaceHotkeys();
        if (_blueprints.Count == 0)
            // The last blueprint was just deleted.
            return;

        Xyz cursor = VoxelRaycaster.GetVoxelPosition();
        Xyz rotatedSize = Current.Size.RotateSize(_rotation);
        // The lowest rail sits one voxel above the cursor ground, the height offset moves it from there.
        var origin = new Xyz(cursor.X - rotatedSize.X / 2, cursor.Y + 1 + _heightOffset, cursor.Z - rotatedSize.Z / 2);

        _placement.Preview(Current, origin, _rotation, _adjustTerrain);

        Vector3 hintPosition = (Vector3)origin + new Vector3(rotatedSize.X / 2f, 2f, rotatedSize.Z / 2f);
        ShowHint(hintPosition);

        if (InputHelper.WorldMouseDown && _placement.IsValid && _placement.HasGhosts)
            TryBuild(hintPosition);
    }

    private void HandlePlaceHotkeys()
    {
        if (ToolHelper.IsHotkeyDown(NextKey))
            SelectBlueprint(_index + 1);
        if (ToolHelper.IsHotkeyDown(PreviousKey))
            SelectBlueprint(_index - 1);
        if (ToolHelper.IsHotkeyDown(RotateKey))
            _rotation = _rotation.Add(BuildingRotation.Rotate90);
        if (ToolHelper.IsHotkeyDown(TerrainKey))
        {
            _adjustTerrain = !_adjustTerrain;
            UpdateCaptions();
        }

        // Same keys as the game's rail tool (X / Z), plus Page Up / Page Down.
        Settings settings = Manager<Settings>.Current;
        bool up = ToolHelper.IsHotkeyDown(settings.IncreaseCursorHeightKey) || ToolHelper.IsHotkeyDown(PageUpKey);
        bool down = ToolHelper.IsHotkeyDown(settings.DecreaseCursorHeightKey) || ToolHelper.IsHotkeyDown(PageDownKey);
        if (up || down)
        {
            _heightOffset += (up ? 1 : 0) - (down ? 1 : 0);
            UpdateCaptions();
            OverlayMessage.ShowAtScreenCenter($"Height offset: {FormatOffset(_heightOffset)}");
        }

        if (ToolHelper.IsHotkeyDown(DeleteKey))
            DeleteCurrent();
    }

    private void SelectBlueprint(int index)
    {
        _index = (index + _blueprints.Count) % _blueprints.Count;
        Announce();
    }

    private void DeleteCurrent()
    {
        Blueprint blueprint = Current;
        Storage.Delete(blueprint);
        OverlayMessage.ShowAtScreenCenter($"Deleted \"{blueprint.Name}\".");

        _placement.Clear();
        _blueprints = Storage.LoadAll();
        _index = Mathf.Clamp(_index, 0, Mathf.Max(0, _blueprints.Count - 1));
    }

    private void ShowHint(Vector3 position)
    {
        if (!_placement.HasGhosts)
            return;

        if (_placement.IsValid)
            ToolHintManager.Current.ShowLegacy(position, _placement.Price);
        else
            ToolHintManager.Current.ShowError(position, _placement.Price, "Blocked");
    }

    private void TryBuild(Vector3 hintPosition)
    {
        double price = _placement.Price;
        if (!ToolHelper.Execute(price, BudgetItem.Buildings, hintPosition, R.Audio.Build, R.Audio.BuildBlocked))
            return;

        if (!_placement.Build())
        {
            // Refund the price, nothing was built.
            Company.Current.AddMoney(price, BudgetItem.Buildings);
            OverlayMessage.ShowAtScreenCenter("Can't build blueprint here.");
        }
    }

    private void CreateHotkeyPanel()
    {
        HotkeyPanel panel = HotkeyPanel.Current;
        Settings settings = Manager<Settings>.Current;

        _modeItem = panel.Add(string.Empty).AddKey(ToggleModeKey);
        panel.Add("Previous / next blueprint").AddKey(PreviousKey).AddKey(NextKey);
        panel.Add("Rotate").AddKey(RotateKey);
        panel.Add("Delete blueprint").AddKey(DeleteKey);
        _terrainItem = panel.Add(string.Empty).AddKey(TerrainKey);
        _heightItem = panel.Add(string.Empty)
            .AddKey(settings.DecreaseCursorHeightKey).AddKey(settings.IncreaseCursorHeightKey)
            .AddKey(PageDownKey).AddKey(PageUpKey);
    }

    private void UpdateCaptions()
    {
        _modeItem.SetCaption(_mode == Mode.Capture
            ? "Capture mode (drag over rails) - switch to place"
            : "Place mode - switch to capture");
        _terrainItem.SetCaption(_adjustTerrain ? "Terrain: adjust (on)" : "Terrain: leave as is (off)");
        _heightItem.SetCaption($"Height offset: {FormatOffset(_heightOffset)}");
    }

    private void Announce()
    {
        if (_mode == Mode.Capture)
            OverlayMessage.ShowAtScreenCenter("Drag a rectangle over the rails to save as a blueprint.");
        else if (_blueprints.Count == 0)
            OverlayMessage.ShowAtScreenCenter("No blueprints yet - press Tab and capture one.");
        else
            OverlayMessage.ShowAtScreenCenter($"{Current.Name} ({Current.Rails.Count} rails)");
    }

    private static string FormatOffset(int offset) => offset > 0 ? $"+{offset}" : offset.ToString();
}
