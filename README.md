# Wispmere — Unity Scaffold

A Unity 6 port scaffold of the web prototype (menu → creator → arrival →
free roam). It mirrors the web systems 1:1 so both versions stay in sync:

| Web (`src/*.js`) | Unity (`Runtime/*.cs`) |
|---|---|
| `character.js` | `CharacterData.cs` + `AppearanceLibrary.cs` |
| creator preview | `CharacterCustomizer.cs` + `CreatorUI.cs` |
| `world.js` | `WorldLayout.json` + `WorldLayout.cs` |
| menu/game loop | `GameManager.cs` |
| player/camera | `PlayerController.cs` + `CameraFollow.cs` |
| dialogue/objective/HUD | `DialogueManager.cs` + `GameHUD.cs` |
| resources | `ResourceManager.cs` + `ResourceNode.cs` |
| placeholder texts | `Interactable.cs` |
| `save.js` | `SaveSystem.cs` |

## Third-person camera

The game uses a smooth, moderately zoomed-out third-person camera. The
controller's **right stick** orbits the camera; **middle-mouse drag** remains
available for testing, and the **scroll wheel** zooms. Camera tuning is
available in the **Third-Person Gameplay Camera** section of `CameraFollow`.

## Controller controls

The Xbox-compatible controller is the primary gameplay control scheme:

| Input | Action |
|---|---|
| Left stick | Move directly; while placing, move the preview |
| Right stick | Orbit the camera |
| A | Interact, craft/select in menus, or confirm placement |
| B | Back, close, or cancel placement |
| X | Open or close inventory |
| Start/Menu | Pause |
| LB/RB | Rotate a placement preview |

Approach a resource or interactable to reveal its **A** prompt. World clicks
never move the player. WASD, E, Escape, I, and Tab remain useful keyboard
fallbacks.

## Inventory and crafting

Press **X**, **I**, or **Tab** to open the 22-slot satchel. Navigate its
controls with the D-pad or left stick and use **A** to select or craft. The
combined crafting panel lists Axe,
Pickaxe, Workbench, Campfire, Storage Chest, and Fence recipes with live
material counts. Craft buttons remain selectable when unaffordable so the
recipe costs and feedback remain accessible by controller. **B** closes the
satchel; **Start/Menu** opens the pause menu. Gathering resources and owned tools continue to use
`ResourceManager`, and crafted inventory items persist in the existing save.
An Axe unlocks a large fallen Hardwood log beyond the eastern meadow; harvesting
it yields Hardwood, which is required to craft a Workbench. Furniture and Fence
remain inventory-only prototypes.

Select a crafted Workbench or Tent in the satchel and choose **Place**. The
left stick moves the preview around the player; green is valid and red is
blocked. **A** places, **LB/RB** rotate, and **B** cancels without consuming
the item. Keyboard fallback: **WASD** moves, **Enter** places, **Q/E** rotate,
and **Escape** cancels. Successful placement consumes one item. Placed objects save with the
existing game. Approach a placed Workbench and press **A** to open its separate
three-recipe panel: Metal Axe, Metal Pickaxe, and Tent.

## The 3D swap rule

**No system script references a mesh, material, or prefab directly.**
All visuals resolve through two art-set assets:

- `TownArtSet` — key → prefab for every world object
  (`building/inn`, `well`, `node/wood`, `tree/teal`, …). Empty slot =
  labeled primitive placeholder, so the game runs on day one.
- `CharacterArtSet` — hair prefabs per style id, materials per
  skin/outfit/eye id. Empty slot = procedural primitive fallback.

**To go 3D:** model/rig in Blender → import the FBX → drag it into the
matching art-set slot. Zero code changes. See `SCENE_SETUP.md`.

## Import

1. Open this folder in Unity Hub; it is pinned to Unity **6000.5.0f1**.
2. Let Unity resolve the Input System and uGUI packages from `Packages/manifest.json`.
   Set **Edit → Project Settings → Player → Other Settings → Configuration →
   Active Input Handling** to **Input System Package (New)** or **Both** if it
   is not already selected. Restart the editor if prompted.
3. The scripts and input actions are already under `Assets/Wispmere/`.
   **Generate C# Class** for `WispmereInput.inputactions` is optional; scripts
   use the asset directly.
4. Follow `SCENE_SETUP.md` to wire the scene.
5. Open the folder in **VS Code** for editing — see `VSCODE_SETUP.md`.

## Save parity

`SaveSystem` writes the web save fields (`player, appearance{},
resources{wood,stone,fiber}, scene, pos, town, createdAt`) plus a
`hasSavedPosition` flag to
`Application.persistentDataPath/wispmere_save_v1.json`, so design tools can
read either file. Resource quantities include Wood, Stone, Fiber, Ore, and
Hardwood. (Positions are Unity units on this side, layout pixels on web — same
fields, converted by `WorldLayout.ToUnity`.)
