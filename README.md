# ClickIt

[![CI](https://github.com/Barragek0/ClickIt/actions/workflows/ci.yml/badge.svg?branch=main&t=1730808000)](https://github.com/Barragek0/ClickIt/actions/workflows/ci.yml)

## What is ClickIt?

ClickIt is a Path of Exile automation plugin for ExileAPI.

It can automatically click labels such as items, chests, shrines, strongboxes, and essences, and can also make altar decisions based on your configured weights.

## Quick Start

1. Install ExileAPI: https://www.ownedcore.com/forums/mmo/path-of-exile/poe-bots-programs/1000594-exileapi-3-23-beyond-plugin-framework.html
2. Download the latest ClickIt release: https://github.com/Barragek0/ClickIt/releases/latest
3. Copy `ClickIt.dll` to `ExileAPI/Plugins/Compiled/ClickIt`.
4. Launch ExileAPI.
5. Hold `F1` (default) to enable clicking.

Recommended first settings:
- Search Radius: `100` for 1080p (adjust for your resolution)
- Click Frequency Target: start around `80`
- Keep Lazy Mode off until basic behavior looks correct

## How It Works

ClickIt evaluates visible labels and picks what to click using:
- Mechanic priority order
- Distance and clickability checks
- Feature-specific rules (strongboxes, essences, ultimatums, altars)

Core design:
- Service-based architecture
- Cached snapshots for hot paths
- Safety checks before click execution
- Debug overlay for timing and visibility diagnostics

## Building

```powershell
# Run tests only (no ExileCore binaries required)
dotnet test Tests\ClickIt.Tests.csproj -c Debug

# Full solution build (requires local ExileCore package path)
msbuild ClickIt.sln /p:Configuration=Debug /p:exapiPackage="C:\Path\To\ExileAPI"
```

If you are using VS Code tasks, run the default build/test workflow so tests and DLL copy steps run in sequence.

## Troubleshooting

- Plugin not clicking:
	- Confirm ExileAPI is running and plugin is loaded
	- Make sure you're clicking the correct hotkey, this can be changed at the top of the settings `Click Hotkey`
    - Enable `Debug Mode`, `Additional Debug Information` and `Debug Frames`. The boxes should fit nicely around the UI elements in-game. If they are not, swap to `borderless windowed` in-game, or position your window at the top-left of your monitor. This window positioning issue is a bug with ExileAPI itself.
- Missed chest clicks:
	- Adjust Chest Height Offset
- Performance drops:
	- Lower search radius
	- Increase click frequency target

## Contributing

1. Fork the repo.
2. Create a branch for your change.
3. Keep changes merge-first and avoid duplicate logic paths.
4. Add or update tests for behavior changes (optional).
5. Open a PR with a summary of the changes

## Credits

- **Arecurius0** - Original pickit improvements that started this
- **cheatingeagle / exapitools** - For keeping ExileAPI up to date and functional
- **instantsc** - Author of the Radar plugin architecture that inspired terrain/pathfinding structure here

---
