# ClickIt

[![CI/CD](https://github.com/Barragek0/ClickIt/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/Barragek0/ClickIt/actions/workflows/ci-cd.yml)

## Overview

ClickIt is an automation plugin for Path of Exile that streamlines gameplay by intelligently interacting with various game elements. Built for ExileApi, this plugin helps players navigate and interact with the game world.

### Key Features

- **Smart Item Pickup**: Automatically identifies and picks up valuable items based on configurable filters
- **Chest Interaction**: Opens chests, strongboxes, and league mechanic containers automatically
- **Automatic Clicking**: Clicks and corrupts essences, shrines, area transitions, crafting recipes and more
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

### Building Locally

The project uses .NET Framework 4.8 and requires MSBuild:

```bash
# Restore dependencies
nuget restore ClickIt.sln

# Build the solution
msbuild ClickIt.sln /p:Configuration=Debug /p:Platform="Any CPU"

# Run tests
dotnet test Tests\ClickIt.Tests.csproj --configuration Debug
```
