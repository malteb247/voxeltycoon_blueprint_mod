# Rail Blueprints

A Voxel Tycoon mod: build a rail junction once, save it as a blueprint and paste it anywhere.

Copied together with the rails: signals, electrification, bridges, tunnels (including piers) and the shape of the
terrain underneath. Blueprints can be rotated before placing.

## Installation

Subscribe to the mod on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3832653333) and
enable it in the mod list of the game. To build it yourself, see [CONTRIBUTING.md](CONTRIBUTING.md).

## Usage

Open the **Rail blueprints** button in the toolbar. **Tab** switches between the two modes.

| Mode    | Key                        | Action                                           |
|---------|----------------------------|--------------------------------------------------|
| Capture | Left mouse, drag           | Mark a rectangle; the rails inside are saved     |
| Place   | Q / E                      | Previous / next blueprint                        |
| Place   | R                          | Rotate by 90°                                    |
| Place   | X / Z (or Page Up / Down)  | Move the blueprint one voxel up / down           |
| Place   | F                          | Toggle terrain adjustment                        |
| Place   | Delete                     | Delete the selected blueprint                    |
| Place   | Left mouse                 | Build (the preview shows price, red = blocked)   |

The lowest rail of a blueprint sits one voxel above the ground under the cursor. With terrain adjustment on, the
ground is dug or filled like in the original, which is part of the price.

Blueprints are text files (`*.vtbp`) in `<Unity persistentDataPath>/Blueprints`, e.g. on macOS
`~/Library/Application Support/VoxelTycoon/VoxelTycoon/Blueprints`. They can be renamed and shared.

## Sharing blueprints

A blueprint is a single small text file, so sharing means copying that file. There is no in-game import or export
button.

### Send a blueprint

1. Open the blueprints folder (see the table below).
2. Send the `.vtbp` file, e.g. by chat, forum or Discord. The file name is the blueprint's name in the game, so
   rename it first (`4-way-turbine.vtbp`) if you like.

### Use a blueprint you received

1. Put the `.vtbp` file into your blueprints folder.
2. Open the **Rail blueprints** tool again (blueprints are read when the tool is opened). It shows up in the
   list; switch with **Q / E**.

| System  | Blueprints folder                                                         |
|---------|---------------------------------------------------------------------------|
| macOS   | `~/Library/Application Support/VoxelTycoon/VoxelTycoon/Blueprints`        |
| Windows | `%USERPROFILE%\AppData\LocalLow\VoxelTycoon\VoxelTycoon\Blueprints`        |
| Linux   | `~/.config/unity3d/VoxelTycoon/VoxelTycoon/Blueprints`                    |

The folder is created when the tool is opened for the first time. If your system differs, the exact path is
`Application.persistentDataPath` + `/Blueprints`.

### Good to know

- Blueprints refer to rails, bridges, tunnels and signals by asset name (e.g. `base/regular.rail`). If one of them
  comes from another mod, the other player needs that mod too; otherwise the preview stays red.
- The mod does not check research: pieces the player has not unlocked yet can still be placed from a blueprint.
- Blueprints saved with the first version of this mod (file header `VTBP1`) contain no bridges, tunnels or terrain.
  Capture them again.
- A blueprint is not tied to a savegame: it works in every game on the same computer.

### Safety of blueprint files

Blueprint files are treated as untrusted input. A file is checked when it is loaded and ignored (with a warning in the
game's `Player.log`) if anything is wrong: too large, malformed, values out of range, undefined enum values, broken
indices, parent links that form a cycle, or asset names that are not rails, bridges, tunnels or signals. Prices always
come from the game, never from the file, and file contents are never executed.

## Feedback and contributions

Bug reports and ideas are welcome as [GitHub issues](https://github.com/malteb247/voxeltycoon_blueprint_mod/issues).
If you want to change the code, [CONTRIBUTING.md](CONTRIBUTING.md) explains how the project is built and tested.

## Disclaimer

This is a vibe coded application, with the help of Claude Sonnet 5.5. I'm a software developer and reviewed the code but comes without any guarantees.

## License

[MIT](LICENSE). Voxel Tycoon and its DLLs are not part of this repository and are not covered by this license.
