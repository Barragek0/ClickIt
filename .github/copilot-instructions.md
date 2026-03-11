# ClickIt AI Agent Instructions

## Mission and Scope

**Plugin Purpose**: Path of Exile automation (item pickup, chest opening, essence corruption, altar decision-making) built on ExileCore.

**Agent Goal**: Deliver safe, minimal, high-confidence changes quickly, with strong regression protection.

## Autonomy Rules (Strict)

1. Execute user task lists in order; complete each step before moving to the next.
2. Do not ask the user to choose between implementation options unless truly blocked.
3. Choose the simplest safe approach that is fast to implement and easy to maintain.
4. Continue independently until all requested work is complete.
5. Ask for help only when required (missing credentials, destructive-risk approval, ambiguous policy).
6. If a step fails, stop, report the exact failure, and propose a concrete fix plan.

## Non-Negotiable Quality Gates

1. **Update tests whenever behavior changes.**
2. **Run the workspace default build task after code changes** so the project is built, tests are run, and the compiled DLL copy/reload flow runs automatically.
3. **Preserve safety-first click behavior**: clickable area checks, element validity checks, and conservative fallbacks.
4. **Prefer merge-first edits**: extend existing services/helpers before creating new paths.

## Core Architecture Snapshot

### Essential patterns

1. Service-based design with constructor injection.
2. Caching policy: 50ms for frequent label work, 100ms for expensive altar validation/weight work.
3. Thread safety: thread-local or thread-static lists for element processing.
4. Safety order: UI zone validation -> element validity -> clickability check.
5. Service dependency order: `TimeCache -> AreaService -> AltarService -> LabelFilterService -> InputHandler -> DebugRenderer -> WeightCalculator -> AltarDisplayRenderer -> ClickService`.

### Core services

- `Services/ClickService.cs`: click orchestration and runtime safety decisions.
- `Services/AltarService.cs`: altar discovery, parsing, and matching.
- `Services/AreaService.cs`: screen regions and safe-click boundaries.
- `Services/LabelFilterService.cs`: label filtering and prioritization.
- `Services/EssenceService.cs`: essence corruption strategy.
- `Services/ShrineService.cs`: shrine detection and click rules.
- `Utils/WeightCalculator.cs`: altar weight calculations.

## Development Workflow

### Merge-first change policy

Before adding code, inspect existing services/helpers and integrate there if possible.

- Reuse existing constants, helpers, and settings nodes.
- Avoid parallel code paths that duplicate behavior.
- Add new classes/methods only when integration would hurt clarity or safety.
- Keep new code small and colocated with the owning feature.

### Implementation checklist

1. Identify the owning service/file.
2. Verify whether caching or invalidation is required.
3. Apply thread-safe element access patterns where needed.
4. Add or update tests in the matching `Tests/` area.
5. Run the workspace default build task (not just direct build/test commands) so build + tests execute and `Copy Compiled DLL` runs.

### Definition of done

- Behavior implemented and aligned with existing patterns.
- Relevant tests added/updated.
- Workspace default build task has run successfully so build + tests complete and the latest DLL is copied/reloaded.
- No avoidable duplication introduced.
- Safety behavior preserved (especially click/input safety).

## Commands and Validation

```powershell
# Preferred final validation/run path (ensures build + tests run and post-build DLL copy task runs)
# VS Code: Run Build Task -> default workspace build task

# Full test suite (primary gate)
dotnet test Tests\ClickIt.Tests.csproj --configuration Debug --logger "console;verbosity=minimal"

# Build plugin (requires ExileCore dependencies)
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ClickIt.sln /p:Configuration=Debug

# Combined validation
dotnet test Tests\ClickIt.Tests.csproj --configuration Debug; & "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ClickIt.sln /p:Configuration=Debug
```

After code changes are complete, always run the workspace default build task as the final step so the project is built, tests are run, and the compiled DLL is copied to the plugin directory.

## Runtime and Environment Notes

- Project targets `.NET 10` (`net10.0-windows`) and x64 runtime.
- Tests can run independently of ExileCore binaries.
- Full plugin build requires local ExileCore dependencies from `..\..\PoeHelper\net48\`.

## Safety and Performance Rules

### Safety mechanisms

- Always respect `PointIsInClickableArea()` checks.
- Always validate element state (`IsValid` / null checks) before use.
- Keep global exception handlers lightweight: logging and minimal cleanup only.

### Performance rules

- Follow the existing cache windows (50ms/100ms).
- Implement/maintain cache invalidation (`InvalidateCache()` patterns).
- Avoid expensive memory access in tight render loops.
- Use `DebugRenderer` timing/caches to detect regressions.

## Regression Protection Guidance

When refactoring for deduplication or merge-first cleanup:

1. Add equivalence tests for critical logic paths before/alongside refactor.
2. Keep behavior-preserving helpers small and easy to verify.
3. Validate edge cases explicitly (null/invalid elements, empty lists, cooldown/threshold boundaries).
4. Prefer pure helper extraction over logic rewrites unless behavior change is intentional.

## Troubleshooting Quick Guide

### Input and click issues

- Re-check clickable-area gating and label eligibility filters.
- Validate lazy-mode blockers/limiters before suspecting input APIs.
- If input becomes stuck: stop plugin, restart plugin, then inspect recent input-handler changes.

### Performance issues

- Verify cache hit rates and invalidation points.
- Inspect `DebugRenderer` timing outputs for render/click spikes.
- Remove repeated expensive work from per-frame paths.

### Altar decision issues

- Confirm unknown mod fallback behavior (default tier/weight rules).
- Check threshold logic ignores zero-value slots where intended.
- Compare parser and matcher normalized text paths when matching regresses.

## Repository Navigation

### High-value locations

- `Core/ClickIt.cs`: orchestration and service wiring.
- `Core/ClickItSettings.cs`: setting nodes and menus.
- `Services/ClickService.cs`: central click selection/execution logic.
- `Services/LabelFilterService.cs`: label eligibility and prioritization.
- `Services/AltarService.cs`, `Services/AltarParser.cs`: altar processing.
- `Utils/InputHandler.cs`: click gating and lazy mode timing.
- `Rendering/DebugRenderer.cs`, `Rendering/AltarDisplayRenderer.cs`: debug/perf visibility.
- `Tests/`: unit/integration coverage by feature area.

### Placement rules for new work

- Feature logic: `Services/`.
- UI/debug rendering: `Rendering/` or `Components/`.
- Settings: `Core/ClickItSettings.cs`.
- Utility-only shared logic: `Utils/`.
- Tests: matching category under `Tests/`.

### Settings UI ordering rule

- For ClickIt settings menus, on-screen order follows declaration order in `Core/ClickItSettings.cs` (and related partial settings files), not menu numeric id alone.
- When a setting must render above/below another control, move the property declaration accordingly.

## Success Criteria Before Hand-Off

1. Workspace default build task completes successfully (including build, tests, and DLL copy step).
2. Safety checks remain intact (clickable-area + element validation).
3. No obvious performance regression in debug metrics.
4. Changes follow merge-first principles with minimal duplication.

**Final principle**: This is a safety-critical automation plugin. Favor stability, safety, and clear maintainability over feature volume.
