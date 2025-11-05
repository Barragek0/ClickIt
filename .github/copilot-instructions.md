# ClickIt AI Agent Instructions

This document provides guidance for AI agents working on the ClickIt codebase.

## Architecture and Core Concepts

- **Purpose**: ClickIt is a plugin for the ExileCore framework, designed to automate in-game actions in "Path of Exile" like clicking items, chests, shrines, and altars.
- **Entry Point**: The main logic is in `ClickIt.cs`. The `ClickIt` class inherits from `BaseSettingsPlugin<ClickItSettings>`, making it a settings-driven plugin.
- **Main Loop**: The `Tick()` method is the plugin's heartbeat, running on every game frame. It checks for the `Settings.ClickLabelKey` hotkey and triggers the `clickLabelCoroutine` to perform actions.
- **Coroutines**: The plugin uses coroutines for its main logic (`clickLabelCoroutine` for clicking, `altarCoroutine` for scanning for altars). This enables asynchronous operations without blocking ExileCore's main thread.
- **UI Interaction**: The agent interacts with the game's UI by identifying elements and simulating mouse clicks. The `Render()` method is used to draw overlays and debug information.
- **Settings**: The plugin's behavior is heavily configured through `ClickItSettings.cs`. This class defines all user-configurable options.

## Developer Workflow

- **Building**: The project is a C# library. Build it using MSBuild with the `ClickIt.sln` solution file. The typical build command is:
  ```powershell
  & "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ClickIt.sln /p:Configuration=Debug
  ```
- **Dependencies**: The project relies on the `ExileCore` framework and standard .NET libraries. There are no external NuGet packages specified in `packages.config`.
- **Debugging**: Enable `Settings.DebugMode` for extensive logging. Debugging is typically done by attaching a C# debugger to the running "Path of Exile" process that has ExileCore injected.

## Key Files and Directories

- `ClickIt.cs`: The main plugin class containing core logic for clicking, scanning, and rendering.
- `ClickItSettings.cs`: Defines the settings that control the plugin's behavior. All user-facing options are here.
- `Utils/`: Contains utility classes for mouse and keyboard simulation (`Mouse.cs`, `Keyboard.cs`).
- `AltarButton.cs`, `PrimaryAltarComponent.cs`, `SecondaryAltarComponent.cs`: These files handle the logic for interacting with in-game altars.

## Project-Specific Conventions

- **Performance**: The code prioritizes performance. Note the use of caching (`TimeCache`), avoidance of unnecessary memory allocations in loops, and the use of coroutines for background tasks.
- **UI Avoidance**: The `PointIsInClickableArea()` method in `ClickIt.cs` defines screen regions to *avoid* clicking, such as health/mana globes and buff/debuff bars. This is critical to prevent unintended interactions.
- **Hotkeys**: All actions are gated by the `Settings.ClickLabelKey`. The agent should not perform actions unless this key is pressed.
- **Safety**: Mouse and keyboard inputs are blocked during clicks using `Mouse.blockInput(true/false)` to prevent user interference.
