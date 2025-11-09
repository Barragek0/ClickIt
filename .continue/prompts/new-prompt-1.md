# ClickIt Codebase Documentation

This document provides detailed technical documentation for the ClickIt project, including its architecture, core concepts, developer workflow, file structure, and project-specific conventions. Use this as a reference for development, debugging, and maintenance.

---

## Architecture and Core Concepts

- **Purpose**: ClickIt is a plugin for the ExileCore framework, automating in-game actions in "Path of Exile" such as clicking items, chests, shrines, and altars.
- **Entry Point**: The main logic resides in `Core/ClickIt.cs`, where the `ClickIt` class inherits from `BaseSettingsPlugin<ClickItSettings>`. This makes the plugin settings-driven and integrates with ExileCore's plugin system.
- **Main Loop**: The `Tick()` method is the plugin's heartbeat, running every game frame. It checks for the `Settings.ClickLabelKey` hotkey and triggers the `clickLabelCoroutine` to perform actions.
- **Coroutines**: The plugin uses coroutines for its main logic (`clickLabelCoroutine` for clicking, `altarCoroutine` for scanning for altars), enabling asynchronous operations without blocking ExileCore's main thread.
- **UI Interaction**: The agent interacts with the game's UI by identifying elements and simulating mouse clicks. The `Render()` method is used to draw overlays and debug information.
- **Settings**: All user-configurable options are defined in `Core/ClickItSettings.cs`.

---

## Developer Workflow

- **Building**: The project is a C# library. Build it using MSBuild with the `ClickIt.sln` solution file. Example command:
  ```powershell
  & "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ClickIt.sln /p:Configuration=Debug
  ```
- **Dependencies**: The project relies on the `ExileCore` framework and standard .NET libraries. No external NuGet packages are specified in `packages.config`.
- **Debugging**: Enable `Settings.DebugMode` for extensive logging. Debugging is typically done by attaching a C# debugger to the running "Path of Exile" process with ExileCore injected.

---

## File and Directory Overview

- **Core/**
  - `ClickIt.cs`: Main plugin class, entry point, main loop, and core logic.
  - `ClickItSettings.cs`: All user-facing and internal settings.
- **Components/**
  - `AltarButton.cs`, `PrimaryAltarComponent.cs`, `SecondaryAltarComponent.cs`: Logic for interacting with in-game altars.
  - `CustomItem.cs`: Wraps item label and entity data for items on the ground.
- **Constants/**
  - `AltarModsConstants.cs`: Dictionaries and lists of altar mod definitions and mappings.
  - `Constants.cs`: Centralizes string and numeric constants.
- **Rendering/**
  - `AltarDisplayRenderer.cs`, `AltarRenderer.cs`: Draw overlays and altar choice highlights.
  - `DebugRenderer.cs`: Draws debug overlays, including status, errors, and performance info.
- **Services/**
  - `AltarService.cs`: Detects, parses, and manages altars in the game world.
  - `AltarWeightCalculator.cs`: Calculates weights for altar choices.
  - `AreaService.cs`: Manages screen regions and clickable areas.
  - `ClickService.cs`: Orchestrates clicking logic for items, chests, and altars.
  - `ElementService.cs`: Utility for traversing and filtering UI elements.
  - `EssenceService.cs`: Handles logic for essence corruption interactions.
  - `LabelFilterService.cs`: Filters item/altar labels based on settings and context.
- **Utils/**
  - `Input.cs`, `InputHandler.cs`, `Keyboard.cs`, `Mouse.cs`: Abstractions and low-level utilities for mouse/keyboard input.
  - `WeightCalculator.cs`: Calculates weights for altar mods and choices.
- **Properties/**
  - `AssemblyInfo.cs`: Assembly metadata and versioning.
  - `Settings.Designer.cs`, `Settings.settings`: Application settings.
- **Tests/**
  - See `AI-instructions.md` for a full list and explanation of all test classes.

---

## Project-Specific Conventions

- **Performance**: The code prioritizes performance. Caching (`TimeCache`), minimal memory allocations in loops, and coroutines for background tasks are used throughout.
- **UI Avoidance**: The `PointIsInClickableArea()` method in `AreaService.cs` defines screen regions to *avoid* clicking, such as health/mana globes and buff/debuff bars, to prevent unintended interactions.
- **Hotkeys**: All actions are gated by the `Settings.ClickLabelKey`. The agent should not perform actions unless this key is pressed.
- **Safety**: Mouse and keyboard inputs are blocked during clicks using `Mouse.blockInput(true/false)` to prevent user interference.

---
