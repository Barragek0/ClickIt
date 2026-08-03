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

### Building

```powershell
dotnet test Tests\ClickIt.Tests.csproj -c Debug -p:IncludeIntegrationTests=true
```

```powershell
msbuild ClickIt.sln /p:Configuration=Debug /p:exapiPackage="C:\Path\To\PoeHelper\net48\"
```

In VS Code, rename `.vscode/tasks.sample.json` to `.vscode/tasks.json`, adjust for your environment, and run the default `Build and Test` task (build → test → copy the DLL into the plugin folder). A hidden sidecar monitors `testhost*` memory (default 2048 MB; override with `CLICKIT_TEST_MEMORY_THRESHOLD_MB`).

`ThirdParty/Decompiled/` and `.scripts/ThirdPartyDecompiler/` are local-only decompile artifacts and are not required to build.

### Testing & coverage

- Use the `Review Coverage` task; output is written under `Tests/TestResults/`.
- `Tests/README.md` documents the repo's testing conventions.

### Contributing

- Prefer merge-first changes inside the existing owner; no wrappers just to preserve an old shape.
- Feature logic in `Features/`, shared helpers in `Shared/`, UI in `UI/`.
- Update tests when behavior changes; keep test-only helpers inside `Tests/` (no `*ForTests` methods or test-only branches in the main project).
- If you move folders or namespaces, update `.github/project-structure.md` too.
- Follow the current structure.

## Credits

- **Arecurius0** — original pickit improvements that started this project
- **cheatingeagle / exapitools** — keeping ExileAPI up to date and functional
- **instantsc** — the Radar plugin that influenced the terrain/pathfinding structure
- **AI** — used for documentation, bug fixing (if I couldn't find a solution myself), refactoring / improving codebase structure, and all tests in the `Tests/` project
