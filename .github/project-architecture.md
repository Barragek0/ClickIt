# ClickIt Project Architecture

This document provides an exhaustive analysis of the ClickIt codebase, including detailed explanations of all components, services, their relationships, data flow, and architectural patterns. This serves as a comprehensive reference for AI assistants and developers working on the project.

---

## Project Directory Structure

```
ClickIt/
│
├── .vscode/
│   ├── project-overview.md
│   └── directory-structure.md
│
├── App.config                     # Assembly binding redirects and runtime configuration
├── ClickIt.csproj                 # Project file with dependencies and build configuration
├── ClickIt.sln                    # Solution file
├── nuget.config                   # NuGet package source configuration
├── packages.config                # Legacy package references
├── README.md                      # Project documentation
├── run-tests.bat                  # Windows test execution script
├── run-tests.sh                   # Unix test execution script
├── runsettings.xml                # Test configuration for unit tests
├── remove-comments.ps1            # PowerShell script for code cleanup
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
│   ├── AssemblyInfo.cs            # Assembly metadata and versioning
│   ├── Settings.Designer.cs       # Auto-generated application settings
│   └── Settings.settings          # Application settings definition
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

## Detailed File Explanations and Architecture

### Core Layer

#### Core/ClickIt.cs - Main Plugin Entry Point
**Role**: Primary plugin class inheriting from `BaseSettingsPlugin<ClickItSettings>`
**Key Responsibilities**:
- **Plugin Lifecycle Management**: `OnLoad()`, `Initialise()`, `OnClose()` methods
- **Main Game Loop**: `Tick()` method runs on every game frame, checking hotkey state
- **Coroutine Orchestration**: Manages three critical coroutines:
  - `MainClickLabelCoroutine()`: Handles item clicking logic
  - `MainScanForAltarsLogic()`: Scans for altar components
  - `InputSafetyCoroutine()`: Monitors input safety and hotkey release
- **Performance Monitoring**: Real-time FPS calculation, timing queues (render, click, altar)
- **Debug Rendering**: Renders debug overlays when `Settings.DebugMode` is enabled
- **Error Management**: Rolling error log with automatic cleanup, comprehensive exception handling
- **Input Safety**: Hotkey detection, input blocking/unblocking with failsafe mechanisms

**Key Methods**:
- `Render()`: Main rendering method with performance optimization
- `ClickLabel()`: Core clicking logic with timing controls
- `ProcessRegularClick()`: Processes regular item clicking operations
- `PerformSafetyChecks()`: Validates plugin state and input safety
- `ForceUnblockInput()`: Emergency input unblocking for safety

#### Core/ClickItSettings.cs - Comprehensive Configuration System
**Role**: ISettings implementation with 200+ configurable options
**Categories**:

**Debug & Accessibility**:
- Debug modes, rendering options, bug reporting
- Left-handed mode, hotkey configuration
- Report bug button with GitHub integration

**General Automation**:
- Hotkey settings (`ClickLabelKey`, `ToggleItemsHotkey`)
- Click distance configuration (0-300 range)
- Item filtering (ignore uniques, basic chests, league chests)
- Safety controls (input blocking, UI state checks)

**League Mechanics Support**:
- Harvest, Delve, Legion, Blight, Sentinel interactions
- Sulphite veins, azurite mining
- Alva temple doors, legion pillars
- Settlers ore deposits (CrimsonIron, Orichalcum, Verisium, etc.)

**Essence Corruption System**:
- Global corruption toggle
- MEDS targeting (Misery, Envy, Dread, Scorn)
- Non-shrieking essence corruption
- Crystal Resonance atlas passive support

**Altar Decision System**:
- Searing Exarch automation with highlighting and clicking
- Eater of Worlds automation with decision trees
- Weight-based mod evaluation
- Comprehensive altar mod weight dictionary

**Critical Properties**:
- `ClickEaterAltars`, `ClickExarchAltars`: Altar automation toggles
- `HighlightExarchAltars`, `HighlightEaterAltars`: Decision visualization
- `BlockUserInput`: Input safety control
- `ChestHeightOffset`: Chest clicking calibration
- Weight dictionaries for all altar mods (Player, Minion, Boss targets)

### Component Layer - Data Models

#### Components/AltarButton.cs
**Role**: Simple wrapper for altar button UI elements
**Purpose**: Represents a clickable altar option button
**Usage**: Used in `PrimaryAltarComponent` for top/bottom altar choices

#### Components/CustomItem.cs
**Role**: Enhanced item representation with metadata
**Properties**:
- `IsTargeted`: Func<bool> for targeted status checking
- `BaseName`, `ClassName`: Item classification
- `Distance`: Distance to player for sorting
- `Path`: Entity path for identification
- `Width`, `Height`: Item dimensions
- `IsValid`: Validation status

#### Components/PrimaryAltarComponent.cs - Performance-Critical Altar Data
**Role**: Complete altar representation with caching optimization
**Key Features**:
- **Caching Strategy**: 100ms time-based cache for validation, weights, and rectangles
- **Performance Optimization**: Prevents expensive game memory access in render loops
- **Validation Caching**: `_isValidCache`, `_lastValidationTime` for altar state
- **Weight Caching**: `_cachedWeights`, `_lastWeightCalculationTime` for decision calculations
- **Rectangle Caching**: `_cachedTopModsRect`, `_cachedBottomModsRect` for UI positioning
- **Cache Invalidation**: `InvalidateCache()` for data freshness

**Architecture**:
```csharp
public bool IsValidCached() // Thread-safe validation with cache
public Utils.AltarWeights? GetCachedWeights(Func<PrimaryAltarComponent, Utils.AltarWeights> weightCalculator)
public (RectangleF topRect, RectangleF bottomRect) GetCachedRects()
```

#### Components/SecondaryAltarComponent.cs
**Role**: Represents altar option (top/bottom) with parsed mods
**Properties**:
- `Upsides`, `Downsides`: Parsed mod lists
- `FirstUpside`, `SecondUpside`, etc.: Individual mod accessors
- `HasUnmatchedMods`: Validation flag for unknown mods
- `Element`: UI element reference for positioning

### Service Layer - Business Logic

#### Services/AltarService.cs - Altar Detection and Management
**Role**: Central altar detection, parsing, and management system
**Key Features**:
- **Debug Information**: Comprehensive statistics tracking (`AltarServiceDebugInfo`)
- **Performance Caching**: Mod matching cache and text cleaning cache
- **Regex Optimization**: Pre-compiled RGB markup removal regex
- **Mod Processing**: Advanced mod parsing with target type detection (Player/Minion/Boss)
- **Component Management**: Maintains list of active altar components

**Data Flow**:
1. **Detection**: `GetAltarLabels()` finds altar labels by type (Exarch/Eater)
2. **Parsing**: `ExtractModsFromElement()` processes altar UI text
3. **Classification**: `ProcessMods()` categorizes upsides/downsides
4. **Validation**: `IsValidCached()` ensures altar state
5. **Management**: `AddAltarComponent()` maintains component list

**Critical Methods**:
- `ProcessAltarScanningLogic()`: Main altar detection coroutine
- `ExtractModsFromElement()`: Text processing and mod extraction
- `BuildAltarKey()`: Unique identification for duplicate prevention
- Debug info properties for performance monitoring

#### Services/ClickService.cs - Click Orchestration
**Role**: Centralized clicking logic with safety and coordination
**Responsibilities**:
- **Altar Clicking**: Automated altar decision clicking
- **Safety Validation**: Pre-click validation and state checks
- **Input Coordination**: Safe input blocking/unblocking
- **Error Handling**: Comprehensive exception handling

**Key Methods**:
- `ProcessAltarClicking()`: Main altar clicking coroutine
- `ShouldClickAltar()`: Validates altar for clicking
- `GetAltarElementToClick()`: Determines which altar element to click
- `ClickAltarElement()`: Performs the actual click operation

#### Services/AreaService.cs - Screen Region Management
**Role**: Manages screen areas and clickable region calculations
**Responsibilities**:
- **Screen Area Tracking**: Full screen, health/mana areas, buffs/debuffs
- **Clickable Zone Calculation**: Determines safe clicking areas
- **Dynamic Updates**: Updates areas when window size/position changes
- **UI Avoidance**: Prevents clicks on game UI elements

**Key Areas**:
- `FullScreenRectangle`: Complete game window
- `HealthAndFlaskRectangle`: Bottom-left health/mana area
- `ManaAndSkillsRectangle`: Bottom-right mana/skills area  
- `BuffsAndDebuffsRectangle`: Top buff/debuff area

#### Services/LabelFilterService.cs - Item Label Filtering
**Role**: Filters and prioritizes ground item labels for clicking
**Key Features**:
- **Path Matching**: Entity path validation for different item types
- **Distance Filtering**: Player distance validation
- **Special Handling**: Verisium detection, harvest filtering
- **Priority Sorting**: Distance-based sorting for optimal clicking

**Key Methods**:
- `GetNextLabelToClick()`: Determines next item to interact with
- `HasVerisiumOnScreen()`: Special Verisium detection logic
- `FilterHarvestLabels()`: Harvest-specific filtering

#### Services/ElementService.cs - Thread-Safe Element Operations
**Role**: Thread-safe element traversal and text extraction
**Key Features**:
- **Thread Local Storage**: Prevents race conditions with multi-threading
- **Text Extraction**: Safe text retrieval from UI elements
- **String Matching**: Pattern matching for element identification
- **Recursive Traversal**: Safe child element discovery

**Critical Implementation**:
```csharp
[ThreadStatic]
private static List<Element>? _threadLocalList;
```

#### Services/EssenceService.cs - Essence Corruption Logic
**Role**: Advanced essence corruption decision system
**Strategies**:
- **Global Corruption**: `CorruptAllEssences` override
- **MEDS Targeting**: Specific essences (Misery, Envy, Dread, Scorn)
- **Non-Shrieking**: Avoids shrieking essences for Crystal Resonance
- **Shrieking Detection**: Comprehensive shrieking essence identification

**Key Methods**:
- `ShouldCorruptEssence()`: Main corruption decision logic
- `GetCorruptionClickPosition()`: Calculates safe corruption click position

#### Services/AltarWeightCalculator.cs - Decision Calculation
**Role**: Calculates weighted decisions for altar choices
**Features**:
- **Mod Weight Lookup**: Settings-based weight retrieval
- **Upside/Downside Calculation**: Separate weight calculations
- **Individual Mod Tracking**: Per-mod weight calculation
- **Ratio Calculation**: Top/Bottom choice ratios

### Rendering Layer - Visualization and Debug

#### Rendering/AltarDisplayRenderer.cs - Altar Decision Visualization
**Role**: Renders altar decisions and weights on screen
**Features**:
- **Choice Highlighting**: Visual indication of recommended choice
- **Weight Display**: Shows calculated weights for each option
- **Error Visualization**: Invalid altar detection display
- **Rectangle Validation**: Safe rectangle checking before rendering

#### Rendering/AltarRenderer.cs - General Altar Rendering
**Role**: Renders altar components and their metadata
**Usage**: Called by `ClickIt.Render()` for altar overlay display

#### Rendering/DebugRenderer.cs - Comprehensive Debug Overlays
**Role**: Renders extensive debug information
**Sections**:
- **Plugin Status**: General plugin state and timing
- **Performance Metrics**: FPS, timing averages, cache hits
- **Input State**: Hotkey status, input blocking state
- **Game State**: Current game state and UI status
- **Altar Service**: Altar detection statistics
- **Label Information**: Ground item label status
- **Error Log**: Recent errors with timestamps

### Utility Layer - System Integration

#### Utils/InputHandler.cs - High-Level Input Management
**Role**: Safe input simulation with randomization
**Features**:
- **Click Positioning**: Random offset calculation for natural clicking
- **Input Blocking**: Safe user input control
- **Chest Special Handling**: Height offset adjustment for chests
- **Toggle Item Integration**: Automatic item view toggling

#### Utils/Mouse.cs - Low-Level Mouse Operations
**Role**: Direct Windows API mouse interaction
**Features**:
- **Cursor Positioning**: `SetCursorPos()` with window coordinate conversion
- **Click Simulation**: Left/right/middle click events
- **Input Blocking**: Windows API input blocking
- **Position Tracking**: Current cursor position retrieval

#### Utils/Keyboard.cs - Low-Level Keyboard Operations
**Role**: Windows API keyboard simulation
**Features**:
- **Key Press/Release**: Direct key event simulation
- **Key State Detection**: Real-time key state checking
- **Press Duration Control**: Configurable press timing

#### Utils/WeightCalculator.cs - Altar Weight Calculations
**Role**: Core altar decision calculation engine
**Features**:
- **Mod Weight Lookup**: Settings-based weight retrieval
- **Upside Weight Sum**: Additive upside calculation
- **Downside Weight Product**: Multiplicative downside calculation
- **Individual Mod Tracking**: Per-mod weight calculation

#### Utils/Input.cs - Input Abstraction
**Role**: Input interface abstractions for testing and flexibility

### Constants and Configuration

#### Constants/AltarModsConstants.cs - Mod Data Definitions
**Role**: Comprehensive altar mod definitions and mappings
**Structure**:
- **Target Type Dictionaries**: Player, Minion, Boss classification
- **Downside Mods List**: 50+ downside mods with weights
- **Upside Mods List**: 100+ upside mods with weights
- **Default Weight Values**: Balance values for all mods

**Key Dictionaries**:
- `FilterTargetDict`: General target type classification
- `AltarTargetDict`: Altar-specific target classification
- `DownsideMods`: Player/Minion/Boss downside definitions
- `UpsideMods`: Player/Minion/Boss upside definitions

#### Constants/Constants.cs - Centralized Constants
**Role**: String and numeric constants used throughout the plugin
**Categories**:
- **Entity Path Strings**: Game entity path identifiers
- **Target Type Strings**: Player, Minion, Boss classifications
- **UI Text Constants**: Text used for element identification
- **Timing Constants**: Delays, timeouts, cache durations
- **Mouse Event Constants**: Windows API mouse event codes

### Configuration Files

#### App.config - Runtime Configuration
**Role**: Assembly binding redirects and runtime configuration
**Critical Redirects**:
- System.Runtime, System.Numerics.Vectors
- System.Windows.Forms, System.Memory
- Newtonsoft.Json, SharpDX assemblies
- Forces newer versions for compatibility

#### ClickIt.csproj - Project Configuration
**Role**: Build configuration and dependencies
**Key Settings**:
- .NET Framework 4.8 target
- x64 platform configuration
- ExileCore dependency reference
- Debug/Release configurations

### Test Suite Architecture

The test suite provides comprehensive coverage across multiple categories:

#### Core Functionality Tests
- **PluginLifecycleTests.cs**: Service initialization, dependency handling
- **ClickExecutionTests.cs**: Click simulation, coordinate transformation
- **InputSafetyAndValidationTests.cs**: Input safety mechanisms
- **PerformanceTimingTests.cs**: Performance optimization validation

#### Business Logic Tests
- **AltarDecisionIntegrationTests.cs**: Complete altar decision flow
- **AdvancedDecisionLogicTests.cs**: Complex decision scenarios
- **AltarModParsingTests.cs**: Mod text parsing and classification
- **ServiceIntegrationTests.cs**: Cross-service functionality

#### Data and Configuration Tests
- **SettingsValidationTests.cs**: Settings validation and defaults
- **ConstantsTests.cs**: Constant value validation
- **AltarModsConstantsTests.cs**: Mod data integrity
- **ExtendedConstantsTests.cs**: Mod data completeness

#### Edge Case and Error Handling
- **AdvancedEdgeCaseTests.cs**: Memory pressure, large datasets
- **ErrorHandlingTests.cs**: Exception handling and recovery
- **WeightCalculationEdgeCaseTests.cs**: Weight calculation edge cases
- **UtilityFunctionTests.cs**: Utility function validation

#### UI and Rendering Tests
- **RenderingAndUILogicTests.cs**: Visual overlay testing
- **ScreenAreaCalculationTests.cs**: UI area calculation
- **PathMatchingAndClassificationTests.cs**: Entity classification

---

## Data Flow and Architecture Patterns

### Main Plugin Flow
1. **Initialization**: `Initialise()` creates all services and sets up coroutines
2. **Game Loop**: `Tick()` checks hotkey and resumes coroutines
3. **Background Processing**: Coroutines handle scanning, clicking, safety monitoring
4. **Rendering**: `Render()` displays overlays and debug information

### Altar Decision Flow
1. **Detection**: `AltarService.GetAltarLabels()` finds altar components
2. **Parsing**: Mod text extraction and classification
3. **Caching**: `PrimaryAltarComponent` caches validation and calculations
4. **Weight Calculation**: `WeightCalculator` determines choice weights
5. **Decision**: `ClickService` makes clicking decisions
6. **Execution**: `InputHandler` performs safe clicking

### Item Clicking Flow
1. **Filtering**: `LabelFilterService` filters and sorts ground items
2. **Validation**: `AreaService` validates clickable areas
3. **Positioning**: `InputHandler` calculates click position
4. **Execution**: Safe input blocking and click simulation
5. **Cleanup**: Input unblocking and state reset

This architecture emphasizes performance, safety, and maintainability through clear separation of concerns, comprehensive caching, and extensive error handling.
