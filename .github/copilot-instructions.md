# ClickIt AI Agent Instructions

## Quick Start for AI Agents (Essential Knowledge)

**Plugin Purpose**: Path of Exile automation (item pickup, chest opening, essence corruption, altar decision-making) built on ExileCore framework.

### 5 Essential Patterns Every AI Must Know
1. **Service Architecture**: 7 core services with constructor injection (Services/*)
2. **Performance Caching**: 50ms for labels, 100ms for altar validation, ThreadLocal for elements
3. **Thread Safety**: `[ThreadStatic]` lists in ElementService for multi-threading
4. **Safety-First**: UI zone validation → Element state → Clickability check
5. **Dependency Order**: TimeCache → AreaService → AltarService → LabelFilter → InputHandler → DebugRenderer → WeightCalculator → AltarDisplay → ClickService

### Core Services (1 line each)
- **ClickService**: Orchestrates all clicking logic with safety checks
- **AltarService**: Detects/parses altars with mod matching caches  
- **AreaService**: Manages screen regions and clickable area calculations
- **ElementService**: Thread-safe element traversal (ThreadLocal pattern)
- **LabelFilterService**: Filters/prioritizes ground items
- **EssenceService**: Corruption logic with MEDS/non-shrieking strategies
- **WeightCalculator**: Calculates weights for altar decisions

### Critical Commands
```powershell
# Test (always run this first)
dotnet test Tests\ClickIt.Tests.csproj --configuration Debug

# Build main plugin (requires ExileCore dependencies)  
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ClickIt.sln /p:Configuration=Debug

# Debug: Enable Settings.DebugMode + Settings.RenderDebug
```

### Key Safety Mechanisms
- **Input Blocking**: `SafeBlockInput()` with 5000ms failsafe
- **UI Avoidance**: `PointIsInClickableArea()` geometric zone checks
- **Element Validation**: `IsValid` verification + null checks
- **Emergency**: `ForceUnblockInput()` for stuck input

---

## Development Workflow for AI Agents

### Feature Development Process
1. **Identify Service Boundary**: Which service owns the logic? (Services/ directory)
2. **Check Caching Rules**: Use 50ms for frequent operations, 100ms for expensive ones
3. **Validate Thread Safety**: Use ThreadLocal pattern if accessing elements
4. **Add Tests**: Add to appropriate test file in Tests/ directory  
5. **Run Validation**: Execute test suite before/after changes

### Performance-First Development
- **Caching Rules**: 
  - 50ms: `TimeCache<List<LabelOnGround>>` for label updates
  - 100ms: `PrimaryAltarComponent` validation/weight calculation
  - ThreadLocal: ElementService for multi-threading safety
- **Memory Management**: Always implement cache invalidation with `InvalidateCache()`
- **Performance Testing**: Monitor `DebugRenderer` timing queues (60-sample render, 10-sample click/altar)

### Safety Validation Checklist
- [ ] Input blocking implemented (`SafeBlockInput()`)
- [ ] UI safety zones respected (`PointIsInClickableArea()`)
- [ ] Element state validated (`IsValid` checks)
- [ ] Emergency unblock available (`ForceUnblockInput()`)
- [ ] Cache invalidation implemented
- [ ] ThreadLocal used for element lists

---

## Essential Troubleshooting Guide

### Debug Mode Workflow
1. **Enable**: Set `Settings.DebugMode = true` and `Settings.RenderDebug = true`
2. **Monitor**: Check `DebugRenderer` output for real-time metrics:
   - FPS calculation (1000ms intervals)
   - Timing queues (render/click/altar)
   - Cache hit rates (`_modMatchCache`, `_textCleanCache`)
3. **Debug Altar Issues**: Check `AltarService.DebugInfo` for scan counts, mod matching stats
4. **Performance**: Monitor 60-sample render queue for frame timing issues

### Common Error Patterns & Solutions

#### Input Safety Issues
- **Stuck Input**: Call `ForceUnblockInput("Emergency unblock")` immediately
- **UI Interference**: Validate `AreaService.PointIsInClickableArea()` calculations
- **Hotkey Problems**: Check `Settings.ClickLabelKey` (default: F1) and `Settings.Enable`

#### Performance Issues  
- **Game Freezing**: Eliminate direct memory access in render loops
- **High CPU Usage**: Verify cache invalidation in altar service
- **Memory Pressure**: Ensure `InvalidateCache()` called on state changes

#### Element Access Issues
- **Null Reference**: Always check `element.IsValid` before access
- **Race Conditions**: Use `ThreadLocal` lists in ElementService style
- **Thread Safety**: Set `CanUseMultiThreading = true` with proper synchronization

---

## Environment Setup for AI Development

### ExileCore Integration Requirements
- **Framework Path**: `..\..\PoeHelper\net48\` (ExileCore.dll, GameOffsets.dll, ProcessMemoryUtilities.dll)
- **Test Project**: Standalone - can build/test without ExileCore
- **Main Plugin**: Requires ExileCore dependencies for full build

### Build Validation Workflow
```powershell
# Step 1: Always run tests first (independent validation)
dotnet test Tests\ClickIt.Tests.csproj --configuration Debug

# Step 2: If tests pass, attempt main build
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ClickIt.sln /p:Configuration=Debug

# Step 3: Success criteria
# - All tests pass
# - Main build succeeds (if ExileCore available)
# - No new compiler warnings
```

### Development Environment Validation
- **.NET Framework 4.8** with x64 target
- **AutoGenerateBindingRedirects**: true
- **Platform Target**: x64 (RuntimeIdentifier: win-x64)

---

## Code Patterns Reference

### Template 1: New Service Pattern
```csharp
public class NewService
{
    private readonly ClickItSettings _settings;
    private readonly ExistingService _existingService;
    
    public NewService(ClickItSettings settings, ExistingService existingService)
    {
        _settings = settings;
        _existingService = existingService;
    }
    
    // Pattern: Cache validation with 100ms expiry
    public bool IsValidCached()
    {
        long currentTime = _cacheTimer.ElapsedMilliseconds;
        if (_isValidCache.HasValue && (currentTime - _lastValidationTime) < CACHE_DURATION_MS)
        {
            return _isValidCache.Value;
        }
        
        // Calculate and cache result
        bool isValid = /* validation logic */;
        _isValidCache = isValid;
        _lastValidationTime = currentTime;
        return isValid;
    }
}
```

### Template 2: Thread-Safe Element Processing
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

public List<Element> GetElements(Element? label, string searchText)
{
    var elementsList = GetThreadLocalList();
    elementsList.Clear();
    
    // Element processing logic
    return elementsList;
}
```

### Template 3: Service Integration Pattern
```csharp
// Constructor with dependency injection
public ClickService(
    ClickItSettings settings,
    GameController gameController,
    Action<string, int> logMessage,
    Action<string, int> logError,
    AltarService altarService,
    WeightCalculator weightCalculator,
    Func<Vector2, string?, bool> pointIsInClickableArea,
    Action<bool> safeBlockInput)
{
    // Null checks and assignment
    this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    this.logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
    // ... other dependencies
}
```

---

## Extension Patterns for AI Agents

### Adding New Altar Mod Types
1. **Location**: `Constants/AltarModsConstants.cs`
2. **Pattern**: Add to `DownsideMods` or `UpsideMods` lists
3. **Format**: `("mod_id", "Display Name", "Target", Weight)`
4. **Validation**: Weight must be 1-100 range

### Extending Item Filtering
1. **Location**: `Services/LabelFilterService.cs`
2. **Pattern**: Add new filtering method following existing patterns
3. **Integration**: Update `ClickService` to use new filters

### Modifying Decision Logic
1. **Location**: `Services/AltarWeightCalculator.cs`
2. **Pattern**: Update `CalculateUpsideWeight` or `CalculateDownsideWeight` methods
3. **Settings**: May need to add to `ModTiers` dictionary in settings

---

## AI Safety Guidelines

### Validation Before Changes
- [ ] **Architecture Check**: Confirm service boundary respected (Services/ separation)
- [ ] **Performance Check**: Validate caching/optimization patterns implemented
- [ ] **Safety Check**: Ensure UI/input safety mechanisms maintained
- [ ] **Test Check**: Run relevant test suite (all tests in Tests/ directory)

### Rollback Procedures
- **Breaking Changes**: Identify via test failures or build errors
- **Safe Rollback**: Revert to last known-good commit
- **Emergency Input**: Use `ForceUnblockInput()` if input becomes stuck

### Performance Impact Assessment
- **Before**: Run test suite, note performance metrics in debug mode
- **After**: Re-run tests, compare debug metrics for regression
- **Acceptable**: <10% performance degradation in critical paths

---

## Quick Reference

### Key Files for Immediate Reference
- **Core Orchestration**: `Core/ClickIt.cs:87` (service creation order in `Initialise()`)
- **Thread Safety**: `Services/ElementService.cs:9` (ThreadLocal pattern)
- **Performance Caching**: `Components/PrimaryAltarComponent.cs:36` (100ms cache pattern)
- **Safety Mechanisms**: `Core/ClickIt.cs:120` (UI zone validation)
- **Service Integration**: `Services/ClickService.cs:32` (constructor injection pattern)

### Settings Architecture (200+ settings)
- **Location**: `Core/ClickItSettings.cs`
- **Pattern**: `[Menu("Category", priority)]` attributes
- **Types**: `ToggleNode`, `RangeNode`, `HotkeyNode`
- **Access**: `Settings.PropertyName.Value`

### Emergency Procedures
- **Stuck Input**: `ForceUnblockInput("Emergency")` 
- **Game Freeze**: Restart plugin, check render loop for memory access
- **High Memory**: Invalidate caches, check for memory leaks in altar service

### Test Categories
- **Weight Calculations**: `WeightCalculationTests.cs`
- **Settings Validation**: `SettingsValidationTests.cs` 
- **Altar Logic**: `AltarDecisionLogicTests.cs`
- **Performance**: `PerformanceTimingTests.cs`
- **Integration**: `ServiceIntegrationTests.cs`

---

## Success Validation

### Before Submitting Changes
1. All tests pass: `dotnet test Tests/ClickIt.Tests.csproj`
2. No performance regression in debug metrics
3. Input safety mechanisms preserved
4. Cache invalidation patterns followed
5. ThreadLocal pattern used where appropriate

### After Implementation
1. Enable debug mode and verify functionality
2. Check debug renderer for performance metrics
3. Test in Path of Exile (if available) 
4. Monitor error log for exceptions

**Remember**: This is a safety-critical automation plugin. Always prioritize user safety and game stability over feature completeness.
