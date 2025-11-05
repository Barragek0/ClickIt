# ClickIt



## Overview

ClickIt is an automation plugin for Path of Exile that streamlines gameplay by intelligently interacting with various game elements. Built for ExileApi, this plugin helps players navigate and interact with the game world.

### Key Features
- **Interaction**: Picks up items, opens chests, strongboxes, and league mechanic containers. Clicks and corrupts essences. Clicks shrines, area transitions, crafting recipes and more
- **Altar Optimization**: Advanced eater / exarch altar selection system that evaluates and chooses optimal altar modifiers based on configurable weights and gameplay impact
- **Performance Focused**: Optimized for minimal game impact with efficient caching and coroutine-based processing
- **Safety Features**: Includes UI avoidance zones to prevent accidental clicks on health globes, skill bars, and other critical interface elements

### How It Works

The plugin operates through a hotkey-activated system that scans the game world for interactive elements. When activated, it prioritizes actions based on proximity, value, and safety considerations. The plugin uses pathfinding and click simulation to interact with elements while avoiding interference with normal gameplay.

---

*An improved version of pickit that automatically picks up items, interacts with chests (league mechanic chests too), clicks area transitions, clicks shrines and clicks + corrupts essences.*

*Credit to Arecurius0 for the initial improvements to pickit (caching, chest interacting, etc)*

## Development

This project uses automated testing and CI/CD to ensure code quality. The test suite runs automatically on every push and pull request.

### Requirements

- **ExileCore Framework**: This plugin requires ExileCore framework dependencies to build and run
- **.NET Framework 4.8**: The plugin targets .NET Framework 4.8
- **Path of Exile**: Designed to work with the Path of Exile game client

### Building Locally

**Important**: The main ClickIt plugin requires ExileCore framework dependencies that must be available in your build environment. The plugin is designed to be built within the ExileCore ecosystem.

```bash
# Build test project only (works without ExileCore dependencies)
dotnet restore Tests\ClickIt.Tests.csproj
dotnet build Tests\ClickIt.Tests.csproj --configuration Debug
dotnet test Tests\ClickIt.Tests.csproj --configuration Debug

# Full solution build (requires ExileCore dependencies)
nuget restore ClickIt.sln
msbuild ClickIt.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

### CI/CD Notes

The automated CI/CD pipeline only builds and tests the test project since the main plugin requires ExileCore framework dependencies that are not available in GitHub Actions. The test project contains comprehensive validation of game data and utility functions that can be tested independently.
