# ClickIt

[![CI](https://github.com/Barragek0/ClickIt/actions/workflows/ci.yml/badge.svg?branch=main&t=1730808000)](https://github.com/Barragek0/ClickIt/actions/workflows/ci.yml)

Path of Exile automation plugin for the [ExileCore / ExileAPI](https://www.ownedcore.com/forums/mmo/path-of-exile/poe-bots-programs/1000594-exileapi-3-23-beyond-plugin-framework.html) ecosystem. It clicks visible in-game labels — items, chests, shrines, strongboxes, essences, blight towers, and other supported mechanics — makes altar decisions from your configured settings and weights, and includes a debug UI to inspect exactly what it's doing.

## Features

- Click automation for supported in-game labels and mechanics
- Altar decision-making driven by configurable weights and settings
- Blight tower planning and building
- Safety-first: blocked-area checks, clickable-area validation, conservative fallbacks

## Quick Start

1. Install ExileAPI (see link above).
2. Download the [latest ClickIt release](https://github.com/Barragek0/ClickIt/releases/latest).
3. Copy `ClickIt.dll` into `ExileAPI/Plugins/Compiled/ClickIt`.
4. Launch the game and hold `F1` (default) to enable clicking.

Recommended first settings: **Search Radius** `100` for 1080p, **Click Frequency Target** ~`80`, and **Lazy Mode** off until the basics look right.

## How It Works

ClickIt reads the game state, collects the labels and interactions available around you, filters out anything unsafe or not worth clicking, ranks the rest, and clicks only when the target passes the final safety checks.

## Troubleshooting

- **Not clicking** — verify the plugin is loaded in ExileAPI, you're holding the correct hotkey, and the targets are visible/clickable in game. Turn on the debug overlay to confirm what it sees. `Left-handed` should only be enabled when using a left-handed mouse.
- **Chest clicks feel off** — adjust `Chest Height Offset`.
- **Poor performance** — lower `Search Radius`, if that doesn't help, reduce the `Click Frequency Target`.

## For Developers

### Project layout

- `Core/` — plugin surfaces, lifecycle, composition, runtime hosts, settings composition
- `Features/` — domain behavior (`Click`, `Labels`, `Altars`, `Blight`, `Mechanics`, `Pathfinding`, ...)
- `Shared/` — cross-domain helpers (`Diagnostics`, `Game`, `Input`, `Math`)
- `UI/` — overlays, debug UI, introspection, settings panels
- `Tests/` — tests mirroring the runtime ownership structure

### Building and testing

On Windows, install the .NET 10 SDK and restore NuGet packages. The checked-in runtime references under `ThirdParty/` let a fresh checkout build without a separate ExileAPI installation. Generated decompiled sources are not required.

For VS Code, copy the tracked `.vscode/tasks.sample.json` into the workspace task configuration and run **Build and Test**. It performs a non-incremental build, runs the full suite through the memory-guarded runner, verifies whitespace formatting, and stages the plugin DLL under the ignored `artifacts/Plugins/Compiled/ClickIt/` directory. To build against and deploy to a local ExileAPI installation, set `-ExapiPackagePath` to its assembly directory and `-PluginOutputPath` to its `Plugins/Compiled/ClickIt` directory.

The same workflow is available from PowerShell at the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/Scripts/invoke-build-and-test.ps1 `
  -SolutionPath ClickIt.sln `
  -TestProjectPath Tests/ClickIt.Tests.csproj `
  -Configuration Debug `
  -PluginOutputPath artifacts/Plugins/Compiled/ClickIt `
  -BuildTool dotnet `
  -NoIncrementalBuild
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet format ClickIt.sln whitespace --verify-no-changes --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

The test memory guard defaults to 2048 MB and can be changed with `CLICKIT_TEST_MEMORY_THRESHOLD_MB`. Do not invoke `dotnet test` directly; use the guarded runner, the VS Code task, or the command above.

### Testing & coverage

- The sample **Review Coverage** task runs `Tests/Scripts/generate-coverage.ps1`; it restores the pinned ReportGenerator local tool and writes output under `Tests/TestResults/`.
- `Tests/README.md` documents the repo's testing conventions.

### Contributing

- Prefer merge-first changes inside the existing owner; no wrappers just to preserve an old shape.
- Feature logic in `Features/`, shared helpers in `Shared/`, UI in `UI/`.
- Update tests when behavior changes; keep test-only helpers inside `Tests/` (no `*ForTests` methods or test-only branches in the main project).
- If you move folders or namespaces, keep the project layout and ownership guidance in this README accurate.
- Follow the current structure.

## Credits

- **Arecurius0** — original pickit improvements that started this project
- **cheatingeagle / exapitools** — keeping ExileAPI up to date and functional
- **instantsc** — the Radar plugin that influenced the terrain/pathfinding structure
- **AI** — used for documentation, bug fixing (if I couldn't find a solution myself), refactoring / improving codebase structure, and all tests in the `Tests/` project.
