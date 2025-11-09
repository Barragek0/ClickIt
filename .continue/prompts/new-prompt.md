---
name: Project Overview
description: A comprehensive overview of the entire ClickIt codebase
invokable: true
---

# ClickIt Overview

This document provides a comprehensive overview of the ClickIt codebase, including its architecture, directory structure, and detailed explanations of all C# source and test files. Use this as a reference for onboarding, maintenance, or further development.

---

## Project Directory Structure

```
ClickIt/
│
├── Components/
│   ├── AltarButton.cs
│   ├── CustomItem.cs
│   ├── PrimaryAltarComponent.cs
│   └── SecondaryAltarComponent.cs
│
├── Constants/
│   ├── AltarModsConstants.cs
│   └── Constants.cs
│
├── Core/
│   ├── ClickIt.cs
│   └── ClickItSettings.cs
│
├── Properties/
│   ├── AssemblyInfo.cs
│   ├── Settings.Designer.cs
│   └── Settings.settings
│
├── Rendering/
│   ├── AltarDisplayRenderer.cs
│   ├── AltarRenderer.cs
│   └── DebugRenderer.cs
│
├── Services/
│   ├── AltarService.cs
│   ├── AltarWeightCalculator.cs
│   ├── AreaService.cs
│   ├── ClickService.cs
│   ├── ElementService.cs
│   ├── EssenceService.cs
│   └── LabelFilterService.cs
│
├── Tests/
│   ├── AdvancedDecisionLogicTests.cs
│   ├── AdvancedEdgeCaseTests.cs
│   ├── AltarDecisionIntegrationTests.cs
│   ├── AltarModParsingTests.cs
│   ├── AltarModsConstantsTests.cs
│   ├── ClickExecutionTests.cs
│   ├── ConfigurationIntegrationTests.cs
│   ├── ConstantsTests.cs
│   ├── ErrorHandlingTests.cs
│   ├── ExtendedConstantsTests.cs
│   ├── InputSafetyAndValidationTests.cs
│   ├── PathMatchingAndClassificationTests.cs
│   ├── PerformanceTimingTests.cs
│   ├── PluginLifecycleTests.cs
│   ├── RenderingAndUILogicTests.cs
│   ├── ScreenAreaCalculationTests.cs
│   ├── ServiceIntegrationTests.cs
│   ├── SettingsValidationTests.cs
│   ├── UtilityFunctionTests.cs
│   └── WeightCalculationEdgeCaseTests.cs
│
└── Utils/
    ├── Input.cs
    ├── InputHandler.cs
    ├── Keyboard.cs
    ├── Mouse.cs
    └── WeightCalculator.cs
```

---

## File and Folder Explanations

### Components/
- **AltarButton.cs**: Represents a clickable altar button UI element.
- **CustomItem.cs**: Wraps item label and entity data for items on the ground.
- **PrimaryAltarComponent.cs**: Encapsulates a full altar, including its type, mods, and buttons.
- **SecondaryAltarComponent.cs**: Represents the upsides/downsides of an altar option.

### Constants/
- **AltarModsConstants.cs**: Contains dictionaries and lists of altar mod definitions and mappings.
- **Constants.cs**: Centralizes string and numeric constants used throughout the plugin.

### Core/
- **ClickIt.cs**: The main plugin class; entry point, main loop, and core logic.
- **ClickItSettings.cs**: Defines all user-configurable plugin settings.

### Properties/
- **AssemblyInfo.cs**: Assembly metadata and versioning.
- **Settings.Designer.cs**: Auto-generated settings class for application configuration.
- **Settings.settings**: XML definition of application settings.

### Rendering/
- **AltarDisplayRenderer.cs**: Handles drawing altar overlays and choice highlights.
- **AltarRenderer.cs**: Renders altar components and their weights on the UI.
- **DebugRenderer.cs**: Draws debug overlays, including status, errors, and performance info.

### Services/
- **AltarService.cs**: Detects, parses, and manages altars in the game world.
- **AltarWeightCalculator.cs**: Calculates weights for altar choices based on settings.
- **AreaService.cs**: Manages screen regions and clickable areas.
- **ClickService.cs**: Orchestrates clicking logic for items, chests, and altars.
- **ElementService.cs**: Utility for traversing and filtering UI elements.
- **EssenceService.cs**: Handles logic for essence corruption interactions.
- **LabelFilterService.cs**: Filters item/altar labels based on settings and context.

### Utils/
- **Input.cs**: Abstractions for mouse/keyboard input.
- **InputHandler.cs**: Handles input simulation and click positioning.
- **Keyboard.cs**: Low-level keyboard simulation utilities.
- **Mouse.cs**: Low-level mouse simulation utilities.
- **WeightCalculator.cs**: Calculates weights for altar mods and choices.

---

## Test Classes (Tests/)

- **AdvancedDecisionLogicTests.cs**: Tests complex decision logic, especially prioritizing build-critical mods and avoiding dangerous downsides.
- **AdvancedEdgeCaseTests.cs**: Validates plugin behavior under extreme or unusual conditions, such as memory pressure and large data sets.
- **AltarDecisionIntegrationTests.cs**: Ensures altar decision logic integrates correctly with mod priorities and player/boss mod handling.
- **AltarModParsingTests.cs**: Verifies correct parsing and cleaning of altar mod strings, including markup and edge cases.
- **AltarModsConstantsTests.cs**: Checks the integrity and completeness of altar mod constants and lookup dictionaries.
- **ClickExecutionTests.cs**: Tests mouse click simulation, coordinate transformation, and input logic.
- **ConfigurationIntegrationTests.cs**: Validates settings configuration, including detection of invalid or inconsistent settings.
- **ConstantsTests.cs**: Ensures all constants are valid, non-empty, and correctly defined.
- **ErrorHandlingTests.cs**: Verifies that exceptions are caught, logged, and do not crash the plugin.
- **ExtendedConstantsTests.cs**: Checks for consistency and completeness in mod data formats and essential gameplay mods.
- **InputSafetyAndValidationTests.cs**: Validates input safety, including mouse position validation and input edge cases.
- **PathMatchingAndClassificationTests.cs**: Tests entity path matching and classification for altars and other objects.
- **PerformanceTimingTests.cs**: Ensures plugin performance meets timing targets and optimizations are effective.
- **PluginLifecycleTests.cs**: Validates service initialization, dependency handling, and plugin lifecycle events.
- **RenderingAndUILogicTests.cs**: Tests rendering logic, UI overlays, and visualization of plugin decisions.
- **ScreenAreaCalculationTests.cs**: Verifies correct calculation of screen areas and point-in-rectangle logic.
- **ServiceIntegrationTests.cs**: Ensures correct integration and coordination between major services (e.g., altar, label filter).
- **SettingsValidationTests.cs**: Checks that all mods have default weights and settings are validated.
- **UtilityFunctionTests.cs**: Tests utility functions for uniqueness, weight ranges, and data integrity.
- **WeightCalculationEdgeCaseTests.cs**: Validates weight calculation logic for edge cases, such as empty or null lists.

---

This document provides a complete, up-to-date reference for the ClickIt codebase, including all test classes and their purposes. If you need more detail on any file or subsystem, consult the source or request further information.
