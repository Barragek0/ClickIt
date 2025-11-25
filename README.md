# ClickIt

[![CI](https://github.com/Barragek0/ClickIt/actions/workflows/ci.yml/badge.svg?branch=main&t=1730808000)](https://github.com/Barragek0/ClickIt/actions/workflows/ci.yml)

## What is ClickIt?

ClickIt is a Path of Exile automation plugin that makes grinding way less tedious. Built for ExileAPI, it automatically clicks stuff for you - items, chests, shrines, essences, and even makes smart decisions on Eater/Exarch altars.

### What it does
- **Picks up items** automatically (can ignore uniques if you want)
- **Opens chests** - basic ones and all the league-specific ones
- **Clicks shrines** so you don't have to
- **Corrupts essences** and spam clicks to awaken them
- **Smart altar clicking** - analyzes Eater/Exarch modifiers and picks the best ones based on weights you provide
- **Lazy mode** - hands-free automation with safety restrictions
- **Lots of settings** - 200+ options to tweak everything

### League-specific stuff it handles
- Alva temple doors
- Betrayal encounters
- Blight pumps
- Breach nodes
- Legion pillars
- Sanctum
- Settlers ore deposits (Crimson Iron, Petrified Wood, etc.)

## Quick Start

1. **Get ExileAPI** - https://www.ownedcore.com/forums/mmo/path-of-exile/poe-bots-programs/1000594-exileapi-3-23-beyond-plugin-framework.html
2. **Download latest release** - https://github.com/Barragek0/ClickIt/releases/latest
3. **Copy DLL** to your ExileAPI plugins folder
4. **Launch ExileAPI**
5. **Press F1** (default hotkey) to start clicking

## Settings Overview

### Basic Stuff
- **Hotkey**: F1 by default, hold to click
- **Range**: How far it looks for stuff (100 for 1080p)
- **Click frequency target**: The amount of time the plugin aims to maintain between clicks

### What to Click
- Items (with unique filtering)
- Basic chests vs League chests
- Shrines, area transitions, crafting recipes
- All individual strongbox types
- Essences with corruption settings
- Harvest nodes

### Advanced Features

**Lazy Mode** (use carefully):
- Runs automatically without holding hotkey
- Won't click dangerous stuff (strongboxes, league chests, settlers trees)
- But if dangerous stuff is on screen, it temporarily allows clicking other things

**Altar AI**:
- Analyzes 300+ modifier combinations
- Lets you weight modifiers based on how good or bad they are
- Highlights or auto-clicks the best choice
- Works for both Eater and Exarch altars

## How it Works (Tech Stuff)

### Architecture
- **Service-based**: Clean separation of concerns
- **Coroutine magic**: Non-blocking async operations
- **Caching**: Updates labels every 50ms, altars every 100ms
- **Thread-safe**: Uses ThreadLocal for multi-threading
- **Performance focused**: Minimal CPU impact

### Debug Tools
- **Debug overlay**: FPS, timing stats, cache hit rates
- **Visual debugging**: Shows clickable areas on screen

## Development

### Building
```bash
# Tests only (no ExileCore needed)
dotnet test Tests\ClickIt.Tests.csproj

# Full build (needs ExileCore path)
msbuild ClickIt.sln /p:Configuration=Debug /p:exapiPackage="C:\Path\To\ExileCore"
```

### Testing
- **A lot of tests** covering almost everything
- **CI** runs on every commit
- Tests game logic, performance, edge cases

### Code Quality
- Modern C# with .NET 8.0
- Comprehensive error handling
- Performance monitoring built-in
- Thread-safe everywhere

## Troubleshooting

**Plugin not working?**
- Make sure ExileAPI is running as admin
- Enable debug mode and additional debug information - render
- Check debug overlay for errors
- Adjust search radius if it's not clicking things that are on-screen

**Missing clicks?**
- Tweak chest height offset
- Check if UI panels are blocking

**Performance issues?**
- Debug overlay shows timing stats
- Reduce search radius / click frequency target, higher radius and higher click frequency means higher performance impact

## Contributing

1. Fork it
2. Make your changes
3. PR it
4. I'll add tests for new stuff where appropriate

## Credits

- **Arecurius0** - Original pickit improvements that started this
- **cheatingeagle / exapitools** - For keeping ExileAPI up to date and functional

---
