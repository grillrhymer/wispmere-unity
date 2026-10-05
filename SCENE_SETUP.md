# Scene setup

Packages: Input System and uGUI are declared in `Packages/manifest.json`.
Set **Edit → Project Settings → Player → Other Settings → Configuration →
Active Input Handling** to **Input System Package (New)** or **Both**. Restart
the editor if Unity prompts you.

The project includes a wired, playable starter scene at
`Assets/Wispmere/Scenes/Wispmere.unity`, already enabled in Build Settings.
Open that scene and press Play. Complete the arrival dialogue, then follow the
old footpaths to explore. The Xbox-compatible controller is the primary
scheme: use the **left stick** to move, the **right stick** to orbit the
camera, and approach a resource or interactable before pressing **A**.
**X** opens inventory, **B** backs out or cancels, and **Start/Menu** pauses.
WASD, **E**, Escape, **I**, and Tab remain keyboard fallbacks. World mouse
clicks do not move or interact with the player; middle-mouse drag and the
scroll wheel remain optional camera controls. Gather marked trees or fallen
timber for wood, loose stones for stone, and overgrown plants for fiber, then
approach the unfinished **Old Workshop** beside the east path and press **A**
to learn what materials it needs. This slice does not yet repair the workshop.

Inventory and crafting controls can be navigated with the D-pad or left stick;
press **A** to select a slot or activate a recipe. When placing a Workbench or
Tent, the left stick moves the preview, **A** places it, **B** cancels, and
**LB/RB** rotate it. The right stick continues to orbit the camera.

The gameplay camera keeps its elevated action-RPG framing around obstacles by
smoothing toward a nearby side or higher bearing when one has a clearer view.
Its existing zoom and orbit controls remain available.

The character's opening walk uses the existing arrival autopilot at a faster
pace, followed by two short introductory lines. The speaker name is displayed
above the separate dialogue text area. In the town layout, the existing shop
and inn are assigned as the General Store and Restaurant; the existing `house`
and `house2` are identified as the future homes of their respective owners.

The creator previews appearance changes immediately; choose each labelled
option with its buttons before beginning the arrival sequence. To regenerate
the starter scene and creator UI, use
**Wispmere → Create → Rebuild Starter Scene**. The generator validates the
scene's Inspector references before it finishes. To exercise the arrival,
movement/camera, gathering, workshop, and objective flow automatically, use
**Wispmere → Test → Run Playable Town Slice Test**.

Without an art set, fallback visuals use colored houses with roofs, doors, and
dark shuttered windows; the workshop has exposed rafters and a repair sign;
trees have cylindrical trunks and leafy canopies; and wood, stone, and fiber
nodes have distinct silhouettes. The abandoned town is intentionally small,
with a worn central plaza, a still fountain, footpaths, and overgrown beds.
The HUD uses compact parchment-toned counters and objective/prompt plates.

## OccaSoftware village art

The playable town can use the imported OccaSoftware Low Poly Fantasy Village
prefabs through its `TownArtSet`, without changing the gameplay components.
Use **Wispmere → Integrate → Apply Occa Village Art Set** to create/update
`Assets/Wispmere/Art/OccaVillageTownArtSet.asset`, assign it to the open
`TownBuilder`, rebuild the town visuals, and save the gameplay scene. Rebuilding
the starter scene also assigns this art set. Clear the `TownBuilder.Art Set`
reference to return to the procedural fallback visuals.

The Occa materials reference a shader that is not included in this Built-in
Render Pipeline project. Integration creates Wispmere-owned Standard-shader
replacements in `Assets/Wispmere/Art/Materials`, preserving the palette texture
and the light material's emission. Only generated prefab instances are remapped;
the imported vendor materials and prefabs remain unchanged.

The imported houses are scaled to about 30% and the trees to 43–48%; the player
remains at its existing 1.8-unit collider height. The layout now uses 40 layout
pixels per Unity unit, expanding the ground to about 40 by 30 units. Existing
saved player positions are converted when loaded.

## Art direction test

Open `Assets/Wispmere/Scenes/ART_DIRECTION_TEST.unity` to review the compact
outdoor style lab. It contains exactly eight core visual tests: the existing
procedural player, one static NPC, cottage, tree, faceted rock, moonstone ore,
chunky ground cover, and magical plant. Rolling terrain, scattered accents,
warm afternoon lighting, and a presentation camera support the display; this
scene does not add gameplay behavior.

To recreate it, use **Wispmere → Art Direction → Create Test Scene**. The
generator validates all eight objects and adds this scene to Build Settings
without replacing the gameplay starter scene.

The presentation camera starts with a pulled-back 43-degree view, then smoothly
tracks and turns with the player while checking for obstacles. For a Play Mode
camera check, use **Wispmere → Test → Run Art Direction Camera Play Mode Test**;
it checks that all eight subjects start in frame and that the camera follows a
moving, turning player.

## Manual setup reference

Use the following steps only if creating a different scene by hand.

## 1. Town

1. Hierarchy → Create Empty → name it `Town`. Add component **`TownBuilder`**.
2. Assign `Layout Json` → `Assets/Wispmere/Runtime/WorldLayout.json`, `Art Set` → leave empty
   for now (primitive placeholders), `Player Prefab` → leave empty (capsule).
3. Menu: **Wispmere → Build Town From Json**. The full prototype town appears:
   inn, hall, shop, cottages (one marked `!` = restoration target), well,
   stalls, arch, trees, gardens, 9 resource nodes, town sign.

## 2. Player

1. Create Empty `Player` at (0, 0, 9.2). Add **`CharacterController`**,
   **`PlayerController`**, **`CharacterCustomizer`**.
2. Assign `Input Asset` → `Assets/Wispmere/Runtime/WispmereInput.inputactions`.
3. Tag it `Player` (or drag it into `CameraFollow.Target`).

## 3. Camera

Main Camera: add **`CameraFollow`**, drag `Player` into Target. It handles the
arrival walk-in framing and the square reveal pull-back automatically.

## 4. UI (UGUI Canvas)

Create `Canvas` + `EventSystem`, then children (all standard UI components):

| Object | Component / notes |
|---|---|
| `HUD/ResourcesText` | `Text` — wired to `GameHUD.Resources Text` |
| `HUD/ObjectiveBanner` | `Text` — `GameHUD.Objective Text` (+ label "OBJECTIVE") |
| `HUD/Prompt` | `Button` — `GameHUD.Prompt Button` + child Text |
| `HUD/InventoryButton` / `HUD/InventoryPanel` | Generated by `StarterSceneBuilder`; the panel shows ResourceManager counts and Axe/Pickaxe crafting |
| `Dialogue` | Panel + `DialogueManager` (Name Text, Body Text, click = Advance) |
| `MainMenu` | Panel with New Game and Continue buttons; wire both buttons on `GameManager` |
| `Creator` | Panel (hidden) + `CreatorUI`: `Name Input` (InputField), `Rows` (empty RectTransform), `Row Prefab`, `Preview Root`, `Begin Button`, `Warn Text` |
| `Pause` | Panel (hidden) + Resume / Save&Title buttons → `GameManager` |

`CreatorUI` builds labelled, directly selectable buttons for Body, Skin, Hair
style, Hair color, Eye color, and Outfit from `AppearanceLibrary`. Choices
update the isolated live preview immediately; selected buttons are highlighted.

## 5. Game flow

1. Create Empty `Game` → **`GameManager`**, **`ResourceManager`**,
   **`DialogueManager`**, **`GameHUD`**; wire the UI references, including
   `MainMenu Panel`, `New Game Button`, and `Continue Button` on `GameManager`.
   New Game opens the creator; Continue is enabled only when a save exists.
2. Press Play: **main menu → New Game → creator → BEGIN → walk-in → reveal →
   "Explore the town."** Continue loads the saved game.
3. Save & Title returns to the in-scene main menu. A separate menu scene is
   optional; it is not required for this scaffold's flow.
4. Gather wood and stone, open Inventory, then craft an Axe or Pickaxe for
   1 Wood + 1 Stone. Tool ownership and resource counts use the existing
   ResourceManager/SaveSystem flow. The Old Rubble Vein reuses the stone-rock
   visuals and yields Ore only after the player owns a Pickaxe.

## 6. Going 3D (the swap)

1. `Wispmere → Create → Town Art Set` (or right-click → Create → Wispmere).
   One row per `prefabKey` (see `TownArtSet.cs` for the full key list).
2. Drag models into rows → assign the asset to `TownBuilder.Art Set` →
   **Wispmere → Build Town From Json** again. Placeholders swap out; positions,
   colliders, interactions, and nodes follow automatically.
3. Same for `CharacterArtSet` (hair prefabs, skin/outfit/eye materials) →
   assign to `CharacterCustomizer.Art Set`.
4. Part anchors on the generated rig (`HairAnchor`, `HeadMesh`, `TorsoMesh`)
   stay named, so props/hats parented there survive art swaps.
