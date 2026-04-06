# ClickIt

[![CI](https://github.com/Barragek0/ClickIt/actions/workflows/ci.yml/badge.svg?branch=main&t=1730808000)](https://github.com/Barragek0/ClickIt/actions/workflows/ci.yml)

ClickIt is a Path of Exile automation plugin for the ExileCore / ExileAPI ecosystem.

It can automatically click visible labels such as items, chests, shrines, strongboxes, essences, and other supported mechanics.

It can also make altar decisions based on your configured settings and weights, and includes debug overlays and diagnostics to help inspect its behavior.

## Quick Start

1. Install ExileAPI: https://www.ownedcore.com/forums/mmo/path-of-exile/poe-bots-programs/1000594-exileapi-3-23-beyond-plugin-framework.html
2. Download the latest ClickIt release: https://github.com/Barragek0/ClickIt/releases/latest
3. Copy `ClickIt.dll` to `ExileAPI/Plugins/Compiled/ClickIt`.
4. Launch ExileAPI.
5. Hold `F1` (default) to enable clicking.

Recommended first settings:
- Search Radius: `100` for 1080p (adjust for your resolution)
- Click Frequency Target: start around `80`
- Lazy Mode: leave it off until the basic behavior looks right

## What The Plugin Is Doing

At a high level, ClickIt works like this:

1. Read the current game and UI state.
2. Collect visible labels and mechanic context.
3. Filter out things that are unsafe, invalid, or not worth clicking.
4. Rank the remaining candidates.
5. Click only when the target passes the final safety checks.

A few design rules show up all over the codebase:
- safety first
- cached snapshots for hot paths
- domain logic lives in feature owners, not random helpers
- debug tooling stays separate from runtime decision logic

## Project Layout

The repo structure changed a lot. The current layout is centered around clear owners instead of broad `Services/` and `Utils/` buckets.

### `Core/`

Host-facing plugin surfaces, lifecycle, composition, runtime hosts, and settings composition.

Important entry surfaces:
- `Core/ClickIt.cs`
- `Core/PluginContext.cs`
- `Core/Bootstrap/PluginCompositionBootstrapper.cs`

### `Features/`

Real domain behavior lives here.

Current feature owners include:
- `Features/Click/` for click automation
- `Features/Labels/` for label filtering, classification, selection, and inventory logic
- `Features/Altars/`
- `Features/Area/`
- `Features/Essence/`
- `Features/Mechanics/`
- `Features/Observability/`
- `Features/Pathfinding/`
- `Features/Shrines/`

Two important ports:
- `Features/Click/ClickAutomationPort.cs`
- `Features/Labels/LabelFilterPort.cs`

### `Shared/`

Cross-domain helpers that are not owned by one feature.

Main shared areas:
- `Shared/Diagnostics/`
- `Shared/Game/`
- `Shared/Input/`
- `Shared/Math/`

### `UI/`

Everything visual belongs here: overlays, debug UI, introspection, and settings panels.

Main UI areas:
- `UI/Debug/`
- `UI/Overlays/`
- `UI/Settings/`

### `Tests/`

Tests mirror the runtime ownership as closely as practical.

That means tests generally live under the matching subtree instead of broad buckets.

## Building

### Fast path in VS Code

- Rename `.vscode/tasks.sample.json` to `.vscode/tasks.json` and modify the file appropriately for your environment.

Use the default build task:

- `Build and Test`

That runs the repo's normal loop in order:
- build the solution
- run the test project
- copy the compiled DLL into the local plugin folder

### Command line

Run tests:

```powershell
dotnet test Tests\ClickIt.Tests.csproj -c Debug -p:IncludeIntegrationTests=true
```

Build the solution with an explicit ExileCore package path:

```powershell
msbuild ClickIt.sln /p:Configuration=Debug /p:exapiPackage="C:\Path\To\PoeHelper\net48\"
```

If `exapiPackage` is not set, the project also has a local fallback path for development.

## Coverage

The workspace coverage flow is:
- `Review Coverage`

Coverage output is written under `Tests/TestResults/`.

If you are working on tests, `Tests/README.md` has the repo's testing conventions and local coverage notes.

## Troubleshooting

### Plugin is not clicking

- Make sure the plugin actually loaded in ExileAPI.
- Confirm you are holding the correct hotkey.
- Check whether the target labels are visible and clickable in game.
- Turn on debug settings and confirm the overlay boxes line up with the UI elements.

If the debug boxes do not line up, try borderless windowed mode or move the game window to the top-left of the monitor. That positioning problem comes from ExileAPI, not ClickIt.

### Chest clicks feel off

- Adjust `Chest Height Offset`.

### Performance feels bad

- Lower `Search Radius`.
- Increase the click frequency target only after the search area looks sane.
- Use the debug overlay before changing lots of settings at once.

## Contributing

Keep it practical:

1. Prefer merge-first changes inside the existing owner.
2. Do not add wrappers just to preserve an old shape.
3. Keep feature logic in `Features/`, shared helpers in `Shared/`, and UI code in `UI/`.
4. Update tests when behavior changes.
5. If you move folders or namespaces, update `.github/project-structure.md` too.

Testing rule:
- keep test helpers, reflection helpers, and test-only setup inside `Tests/`
- do not add `*ForTests` methods, test-only flags, test-only branches, or build steps to the main project just to make tests easier to write

The repo has been moving away from old migration-debt names like `Services/`, `Rendering/`, and `Utils/`. New work should follow the current structure instead of the legacy one.

## Credits

- **Arecurius0** - Original pickit improvements that started this project
- **cheatingeagle / exapitools** - For keeping ExileAPI up to date and functional
- **instantsc** - Author of the Radar plugin that influenced parts of the terrain and pathfinding structure
