# VS Code + Unity setup

Use VS Code as Unity's script editor (IntelliSense, debugging, this scaffold).

## 1. Install

- **Unity 6.5** via Unity Hub (6000.5.0f1).
- **VS Code** (latest stable).
- VS Code extensions:
  - `ms-dotnettools.csdevkit` — **C# Dev Kit** (IntelliSense, Solution Explorer)
  - `visualstudiotoolsforunity.vstuc` — **Unity** (attach debugger, Unity explorer)

## 2. Point Unity at VS Code

Unity → **Edit → Preferences → External Tools**:

- External Script Editor → **Visual Studio Code**.
- (The Unity extension wires the debugger automatically.)

## 3. Open the project

- Open the Unity project folder in Unity Hub once (it generates the
  `.sln`/`.csproj` files).
- Then **File → Open in VS Code** from Unity, or `File → Open Folder…` on the
  project root in VS Code. Open the generated `*.sln`, not individual files.
- If IntelliSense misses Unity APIs: in Unity, **Assets → Open C# Project**
  once to regenerate project files.

## 4. Style

This scaffold ships an `.editorconfig` (4-space indent, `PascalCase` public
members, `_camelCase` private fields). The C# Dev Kit enforces it as you
type — no setup needed.

## 5. Handy tasks

- **Attach debugger:** `Run and Debug` → **Attach to Unity** (or the
  play-button in the Unity extension panel), then press Play in Unity.
- **Breakpoints** in `GameManager`, `PlayerController`, `TownBuilder` all work
  while the game runs.
- No build step: Unity recompiles scripts automatically on save/focus.
