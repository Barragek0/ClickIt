# ClickIt AI Agent Instructions

This document provides comprehensive guidance for AI agents working on the ClickIt codebase, a sophisticated Path of Exile automation plugin built on the ExileCore framework.

## Project Overview

**ClickIt** is a high-performance automation plugin for Path of Exile that intelligently automates various in-game interactions including item pickup, chest opening, essence corruption, and complex altar decision-making. The plugin features an advanced decision-making system, performance optimizations, and comprehensive safety mechanisms.

## Architecture and Core Concepts

### Plugin Architecture
- **Main Plugin**: `Core/ClickIt.cs` - Inherits from `BaseSettingsPlugin<ClickItSettings>` with coroutine-based async operations
- **Settings-Driven**: `Core/ClickItSettings.cs` - Comprehensive configuration system with 200+ settings across multiple categories
- **Service Layer**: `Services/` - Separation of concerns with specialized services for different functionality areas
- **Component Model**: `Components/` - Data models for altar components and custom items
- **Utility Classes**: `Utils/` - Low-level system interaction, input handling, and calculations
- **Rendering System**: `Rendering/` - Debug overlays, altar decision visualization, and performance monitoring

### Core Functionality Areas
1. **General Item Automation**: Items, chests (basic and league-specific), shrines, area transitions, sulphite veins
2. **Essence Corruption System**: Sophisticated logic with multiple corruption strategies (MEDS, non-shrieking, global)
3. **Altar Decision System**: Advanced weighted decision trees for Searing Exarch and Eater of Worlds altars
4. **UI Safety System**: Prevents clicks on game UI elements (health/mana bars, buffs/debuffs)
5. **Input Safety System**: Configurable input blocking and safety checks
6. **Performance Monitoring**: Built-in FPS tracking, timing metrics, and error tracking

### Technical Implementation
- **Coroutine-Based Async**: Non-blocking operations using ExileCore's coroutine system
- **Time-Based Caching**: 100ms cache for altar validation, weight calculations, and rectangle data
- **Thread-Safe Element Traversal**: ThreadLocal storage for multi-threaded safety
- **Performance Metrics**: Queue-based timing tracking for render, click, and altar scanning operations
- **Memory Optimization**: Careful memory management to prevent game freezing

## Development Environment

### Build System
- **Framework**: .NET Framework 4.8, x64 platform target
- **Build Command**: 
  ```powershell
  & "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ClickIt.sln /p:Configuration=Debug
  ```
- **Dependencies**: ExileCore framework (Path of Exile automation), SharpDX (graphics), ImGuiNET (UI)
- **Configuration**: `App.config` contains critical assembly binding redirects for compatibility

### Project Structure
```
ClickIt/
├── Core/                 # Main plugin logic and settings
├── Services/             # Business logic services
├── Components/           # Data models and components
├── Utils/               # Low-level utilities and system interaction
├── Rendering/           # Graphics and debug rendering
├── Constants/           # Mod definitions and constants
├── Tests/              # Comprehensive test suite
└── Properties/         # Assembly and settings configuration
```

## Key Services and Systems

### Service Layer Architecture
1. **AltarService**: Detects, parses, and manages altar components with caching
2. **ClickService**: Orchestrates clicking logic with safety checks
3. **AreaService**: Manages screen regions and clickable area calculations
4. **LabelFilterService**: Filters and prioritizes ground items
5. **ElementService**: Thread-safe element traversal and text extraction
6. **EssenceService**: Essence corruption logic with multiple strategies
7. **AltarWeightCalculator**: Calculates weights for altar decisions

### Performance-Critical Components
- **PrimaryAltarComponent**: Cached altar validation, weight calculation, and rectangle data
- **InputHandler**: Safe input simulation with randomization
- **WeightCalculator**: Fast mod weight lookup and calculation
- **DebugRenderer**: Comprehensive debug overlay system

## Settings and Configuration

### Settings Categories
- **Debug & Testing**: Debug mode, rendering options, bug reporting
- **Accessibility**: Left-handed mode, hotkey configuration
- **General Clicking**: Item categories, distance settings, chest options
- **League Mechanics**: Harvest, Delve, Legion, Blight, Sentinel interactions
- **Essence System**: Corruption strategies, MEDS targeting, shrieking detection
- **Altar Automation**: Exarch/Eater decision trees, highlighting, clicking

### Critical Safety Settings
- `BlockUserInput`: Prevents user interference during automation
- `BlockOnOpenLeftRightPanel`: Safety check for UI state
- `ClickDistance`: Range limitation for interactions
- `ChestHeightOffset`: Calibration for chest clicking accuracy

## Performance and Safety Features

### Performance Optimizations
- **Caching Strategy**: 100ms time-based cache for expensive operations
- **Lazy Evaluation**: Conditional processing only when needed
- **Memory Management**: Pre-allocated collections, efficient string operations
- **Async Operations**: Coroutine-based non-blocking workflows
- **Thread Safety**: ThreadLocal storage for multi-threaded environments

### Safety Mechanisms
- **UI Avoidance**: Automatic detection and avoidance of game UI elements
- **Input Blocking**: Configurable user input blocking during automation
- **Hotkey Gating**: All actions require specific hotkey activation
- **Validation Checks**: Comprehensive element and state validation
- **Error Recovery**: Built-in error handling and recovery mechanisms
- **Failsafe Timers**: Automatic input unblocking after timeout periods

## Testing and Quality Assurance

### Comprehensive Test Suite
- **Unit Tests**: Individual component and service testing
- **Integration Tests**: Cross-service functionality validation
- **Performance Tests**: Timing and optimization validation
- **Edge Case Tests**: Error conditions and boundary scenarios
- **Configuration Tests**: Settings validation and consistency
- **Lifecycle Tests**: Plugin initialization and cleanup

### Debug and Monitoring
- **Real-time Debug Overlay**: Performance metrics, error tracking, service status
- **Extensive Logging**: Configurable debug logging with wrapping
- **Error Tracking**: Rolling error log with timestamps
- **Performance Monitoring**: FPS tracking, timing queues, cache hit rates

## Project-Specific Conventions

### Code Quality Standards
- **Nullable Reference Types**: Extensive use of nullable annotations for safety
- **Performance-First**: Optimizations throughout, especially in render loops
- **Exception Safety**: Comprehensive exception handling with specific catch blocks
- **Memory Safety**: Careful resource management to prevent game freezing

### Input and Automation
- **Hotkey-Driven**: All automation gated by `Settings.ClickLabelKey` (F1 by default)
- **UI Safety First**: `PointIsInClickableArea()` defines strict safe clicking zones
- **Input Randomization**: Small random delays and position offsets to avoid detection
- **Safe Input Blocking**: Automatic unblocking with timeout and state checks

### Configuration Management
- **Settings Hierarchy**: Logical grouping of related settings with tooltips
- **Weight-Based Decisions**: All altar decisions based on configurable mod weights
- **Persistence**: Settings automatically saved and restored between sessions
- **Validation**: Runtime validation of settings consistency and completeness

## Common Development Patterns

### Async Coroutine Pattern
```csharp
private IEnumerator MainClickLabelCoroutine()
{
    while (Settings.Enable)
    {
        yield return ClickLabel();
    }
}
```

### Performance Caching Pattern
```csharp
public bool IsValidCached()
{
    long currentTime = _cacheTimer.ElapsedMilliseconds;
    if (_isValidCache.HasValue && (currentTime - _lastValidationTime) < CACHE_DURATION_MS)
    {
        return _isValidCache.Value;
    }
    // ... calculation logic
    return isValid;
}
```

### Thread-Safe Element Traversal
```csharp
[ThreadStatic]
private static List<Element>? _threadLocalList;

private static List<Element> GetThreadLocalList()
{
    if (_threadLocalList == null)
    {
        _threadLocalList = new List<Element>();
    }
    return _threadLocalList;
}
```

## Debugging and Troubleshooting

### Debug Mode Features
- **Comprehensive Logging**: Detailed operation logs when `Settings.DebugMode` is enabled
- **Visual Debug Overlays**: Screen area highlighting, altar information, performance metrics
- **Error Tracking**: Rolling error log with automatic cleanup
- **Performance Metrics**: Real-time FPS, timing averages, cache hit rates

### Common Issues and Solutions
- **Input Blocking**: Use `ForceUnblockInput()` in emergency situations
- **Memory Pressure**: Implement proper cache invalidation in altar service
- **Game Freezing**: Ensure no direct memory access in render loops
- **Element Access**: Always validate element state before access

### Performance Monitoring
- **Render Timings**: 60-sample queue for render performance tracking
- **Click Timings**: 10-sample queue for click operation analysis
- **Altar Scan Timings**: 10-sample queue for scanning performance
- **FPS Calculation**: Real-time frame rate monitoring

This plugin represents a sophisticated automation system with enterprise-level code quality, comprehensive testing, and advanced performance optimizations. The architecture emphasizes safety, performance, and maintainability while providing powerful automation capabilities for Path of Exile gameplay.
