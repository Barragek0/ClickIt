# ClickIt AI Agent Instructions

## Quick Start for AI Agents (Essential Knowledge)

**Plugin Purpose**: Path of Exile automation (item pickup, chest opening, essence corruption, altar decision-making) built on ExileCore framework.

**Autonomous Operation**:
You are expected to operate autonomously and execute tasks the user gives you. Treat any list of tasks in a user's message as authoritative — execute each task sequentially and complete it before moving on. Do not interrupt execution to ask for high-level guidance, approval, or to weigh options.

**Core rules for autonomous execution (kept intentionally strict)**
1) Do NOT ask the user which approach to take. If you need to choose, pick the simplest safe option.
2) Do NOT offer choices between actions in user-facing messages — perform the chosen action.
3) Do NOT ask what to do next unless the task list is empty.
4) If a task contains multiple steps, proceed through them in order and complete all steps.
5) When a technical decision is required, prefer the alternative that (in priority order):
    a) is fastest to implement,
    b) is easiest to maintain,
    c) is least likely to cause runtime or safety errors.
6) If uncertainty remains after applying the rules above, make the best confident choice and continue; only ask the user when absolutely required (for example: missing credentials, ambiguous repository-wide policy, or dangerous state that needs human approval).

Extra guidance: work methodically, commit small changes, and validate with tests when available. If a step fails (broken tests, build errors), stop, report the failure clearly, and propose a remediation plan before continuing.

**You must update tests whenever you change functionality.** 
After you change any functionality in the code, you must update any appropriate tests to cover the new or changed behavior. Always run the full test suite after making changes to ensure nothing is broken.

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
- **LabelFilterService**: Filters/prioritizes ground items
- **EssenceService**: Corruption logic with MEDS/non-shrieking strategies
- **ShrineService**: Shrine detection and clicking logic
- **WeightCalculator**: Calculates weights for altar decisions

### Critical Commands
```powershell
# Test (run this early and often)
dotnet test Tests\ClickIt.Tests.csproj --logger "console;verbosity=normal"

# Build main plugin (requires ExileCore dependencies)  
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ClickIt.sln /p:Configuration=Debug

# Full validation
dotnet test Tests\ClickIt.Tests.csproj --configuration Debug; & "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ClickIt.sln /p:Configuration=Debug

# Debug: Enable Settings.DebugMode + Settings.RenderDebug
```

### Key Safety Mechanisms
- **UI Avoidance**: `PointIsInClickableArea()` geometric zone checks
- **Element Validation**: `IsValid` verification + null checks

### Global Exception Handling (new)
- The plugin now registers global handlers to improve crash visibility and safe cleanup:
    - `AppDomain.CurrentDomain.UnhandledException` — logs unhandled exceptions and whether the runtime is terminating; attempts a minimal safe cleanup to help keep the plugin in a safe state.
    - `TaskScheduler.UnobservedTaskException` — marks unobserved task exceptions as observed and logs them; also attempts minimal cleanup.
- Note: These handlers are for logging and light cleanup only. They don't guarantee the process will continue after fatal CLR-level crashes (e.g., StackOverflowException, severe OutOfMemory, or native crashes).
- ExileCore itself also has its own error-handling paths and will log many errors originating from game memory access — the plugin-level handlers complement but do not replace ExileCore's internal logging.

    - Quick note: ExileCore already provides built-in error handling and logging; the plugin-level global handlers are intentionally small and complementary — they add plugin-specific context and attempt minimal, safety-critical cleanup (for example, resetting transient runtime state).

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

- ### Safety Validation Checklist
- [ ] UI safety zones respected (`PointIsInClickableArea()`)
- [ ] Element state validated (`IsValid` checks)
- [ ] Cache invalidation implemented
- [ ] ThreadLocal used for element lists
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
- **UI Interference**: Validate `AreaService.PointIsInClickableArea()` calculations
- **Hotkey Problems**: Check `Settings.ClickLabelKey` (default: F1) and `Settings.Enable`

#### Performance Issues  
- **Game Freezing**: Eliminate direct memory access in render loops
- **High CPU Usage**: Verify cache invalidation in altar service
- **Memory Pressure**: Ensure `InvalidateCache()` called on state changes
- **UIHover Slow**: `InputHandler.PerformClick`: Add timing around `gameController?.IngameState?.UIHoverElement`

#### Altar Weight Issues
- **Low Value False Positives**: `HasAnyWeightAtOrBelowThreshold`: Use `w > 0 && w <= threshold` to ignore empty slots (weight 0)
- **Unknown Mods**: Default weight 1; customize in `ModTiers` dictionary

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
    Func<Vector2, string?, bool> pointIsInClickableArea)
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
1. **Location**: `Utils/WeightCalculator.cs`
2. **Pattern**: Update `CalculateUpsideWeight` or `CalculateDownsideWeight` methods
3. **Settings**: May need to add to `ModTiers` dictionary in settings

### Recent Patterns

#### Lazy Mode Click Limiting (InputHandler.cs)
```csharp
private bool TryConsumeLazyModeLimiter()
{
    if (_settings?.LazyMode != null && _settings.LazyMode.Value)
    {
        int limiterMs = _settings?.LazyModeClickLimiting?.Value ?? 250;
        long now = Environment.TickCount64;
        long elapsed = now - _lastClickTimestampMs;
        if (_lastClickTimestampMs != 0 && elapsed < limiterMs)
        {
            return false;
        }
        _lastClickTimestampMs = now;
    }
    return true;
}
```

#### Debug Categories (DebugRenderer.cs)
- `RenderClickFrequencyTargetDebug()`: Regular click timing breakdown
- `RenderClickFrequencyTargetLazyDebug()`: Lazy mode status + timing
- Column layout with spacing: `yPos += lineHeight;` after each category

#### Weight Overrides (ClickItSettings.cs)
- `ModTiers` dictionary: `GetModTier(modId, type)` composite key lookup
- Default: 1 for unknown mods
- UI: CustomNode with ImGui.TreeNodeEx + DefaultOpen flag

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
- **Emergency Input**: If input becomes stuck, perform safe-recovery steps such as stopping and restarting the plugin and investigating input handlers.

### Performance Impact Assessment
- **Before**: Run test suite, note performance metrics in debug mode
- **After**: Re-run tests, compare debug metrics for regression
- **Acceptable**: <10% performance degradation in critical paths

---

## Quick Reference

### Key Files for Immediate Reference
- **Core Orchestration**: `Core/ClickIt.cs:87` (service creation order in `Initialise()`)
- **Lazy Mode**: `Utils/InputHandler.cs:171` (UIHover timing, PerformClick sleeps)
- **Altar Weights**: `Rendering/AltarDisplayRenderer.cs:94` (low-value logic, HasAnyWeightAtOrBelowThreshold)
- **Performance Caching**: `Components/PrimaryAltarComponent.cs:36` (100ms cache pattern)
- **Safety Mechanisms**: `Core/ClickIt.cs:120` (UI zone validation)
- **Service Integration**: `Services/ClickService.cs:32` (constructor injection pattern)
- **Debug Rendering**: `Rendering/DebugRenderer.cs:70` (column layout, category spacing)

### Settings Architecture (200+ settings)
- **Location**: `Core/ClickItSettings.cs`
- **Pattern**: `[Menu("Category", priority)]` attributes
- **Types**: `ToggleNode`, `RangeNode`, `HotkeyNode`
- **Access**: `Settings.PropertyName.Value`

### Emergency Procedures
- **Stuck Input**: If input is stuck, stop the plugin and restart it to return to a known-good input state; investigate and fix the root cause before resuming.
- **Game Freeze**: Restart plugin, check render loop for memory access
- **High Memory**: Invalidate caches, check for memory leaks in altar service

### Test Categories
- **Weight Calculations**: `WeightCalculationTests.cs`, `WeightCalculationEdgeCaseTests.cs`
- **Settings Validation**: `SettingsValidationTests.cs` 
- **Altar Logic**: `AltarDecisionLogicTests.cs`, `AdvancedDecisionLogicTests.cs`
- **Rendering/UI**: `RenderingAndUILogicTests.cs`
- **Performance**: `PerformanceTimingTests.cs`
- **Integration**: `ServiceIntegrationTests.cs`, `ConfigurationIntegrationTests.cs`
- **Input/Safety**: `InputSafetyAndValidationTests.cs`
- **Full suite**: 292 tests across all categories

---

## Repository Layout & File Descriptions

Below is a guided map of the repository. Use this as the primary orientation for navigation and deciding where to make edits.

- Root-level files
    - `ClickIt.sln` / `ClickIt.csproj` — Solution and main project file.
    - `README.md` — High-level project description for humans.
    - `run-tests.ps1` / `run-tests.bat` / `run-tests.sh` — Convenience scripts to run the test-suite in different shells.
    - `runsettings.xml` — Test run settings used for code-coverage and CI.

- Components/
    - `PrimaryAltarComponent.cs`, `SecondaryAltarComponent.cs` — UI/element parsing and cached validation patterns for altars.
    - `AltarButton.cs` — Clickable altar button element logic.

- Constants/
    - `Constants.cs` — Global constants and shared values used across the codebase.
    - `AltarModsConstants.cs` — Canonical list of altar mod definitions (used by AltarService / parser).

- Core/
    - `ClickIt.cs` — Entry and orchestration (service wiring) for the plugin.
    - `ClickIt.Input.cs`, `ClickIt.Render.cs` — Input and rendering specific separation of concerns.
    - `ClickItSettings.cs` — Settings tree for the plugin (menus, toggles, ranges, hotkeys).
    - `ClickItState.cs` — In-memory runtime state for plugin data.
    - `PluginContext.cs` — Shared context passed to services.

- Rendering/
    - `DebugRenderer.cs` — Central debug output and timing queues for performance measurement.
    - `AltarDisplayRenderer.cs` — Visual overlay showing altar decisions, weights and matching mods.
    - `LazyModeRenderer.cs` — Visual feedback for lazy-mode click limiting.

- Services/
    - `AltarService.cs` / `AltarScanner.cs` / `AltarParser.cs` — End-to-end altar detection, parsing, and matching logic.
    - `ClickService.cs` — Centralized click orchestration and safety logic.
    - `LabelService.cs` / `LabelFilterService.cs` — Ground label detection and filtering logic.
    - `AreaService.cs` — Screen area calculations and UI zone checks.
    - `ShrineService.cs` / `EssenceService.cs` — Ancillary feature services (shrines, essence corruption, etc.)

- Utils/
    - `InputHandler.cs` / `Mouse.cs` / `Keyboard.cs` — Input primitives and safety handling.
    - `WeightCalculator.cs` / `AltarModMatcher.cs` / `IndexConstants.cs` — Decision logic and helper utilities.
    - `ErrorHandler.cs`, `LockManager.cs` — Global helpers for exception handling and concurrency.

- Tests/  (organized into subfolders: Configuration, Constants, Integration, Services, Unit, Utils)
    - `ClickIt.Tests.csproj` — Test project; run it frequently.
    - Representative tests: `WeightCalculationTests.cs`, `AltarModParsingTests.cs`, `InputSafetyAndValidationTests.cs`, `RenderingAndUILogicTests.cs`.

If you add a new feature or change behaviour, put logic in a service (Services/), the UI in Rendering/ or Components/, settings in Core/ClickItSettings.cs and tests in Tests/ in the matching category.


---

## Success Validation

### Before Submitting Changes
1. All tests pass: `dotnet test Tests/ClickIt.Tests.csproj`
2. No performance regression in debug metrics
3. Input safety mechanisms preserved
4. Cache invalidation patterns followed
5. ThreadLocal pattern used where appropriate

**Remember**: This is a safety-critical automation plugin. Always prioritize user safety and game stability over feature completeness.
