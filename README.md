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
read either file. (Positions are Unity units on this side, layout pixels on
web — same fields, converted by `WorldLayout.ToUnity`.)
