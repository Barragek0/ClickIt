# Coverage gaps and deferred heavy-runtime tests

This short document lists notable test coverage gaps intentionally left deferred because they require ExileCore / SharpDX runtime assemblies or interactive game state. These were documented and skipped to keep the default test suite dependency-light and stable.

1) Rendering & UI helpers
   - Files: `Rendering/AltarDisplayRenderer.cs`, `Rendering/DebugRenderer.cs`
   - Reason: These constructors and many methods take `ExileCore.Graphics`, `ExileCore.GameController` or `SharpDX` types.
   - Next actions: run gated integration tests on a self-hosted Windows runner with the required DLLs, or extract pure algorithmic parts (weight evaluation) into small, dependency-free helpers and test them separately.

2) Element/Label plumbing that reads actual game elements
   - Files: `Services/AltarService.cs` (methods that call into `TimeCache<List<LabelOnGround>>`, `CollectAltarLabels`, `GetAltarLabels`), `Utils/LabelUtils.cs`
   - Reason: heavy reliance on `ExileCore.PoEMemory.Elements.LabelOnGround` and related types.
   - Next actions: keep unit tests around that exercise private helpers via reflection where possible; for integration tests, provision ExileCore binaries and run gated CI.

3) ClickService integration paths
   - Files: `Services/ClickService.cs`, `Utils/InputHandler.cs`
   - Reason: constructors and many behavior paths require `ExileCore.GameController` and runtime element instances.
   - Next actions: add adapter interfaces in production code to allow dependency injection of a small interface for game state (thin wrappers), or run gated CI with runtime assemblies.

4) LabelFilterService advanced rules
   - Files: `Services/LabelFilterService.cs` (some `ShouldClick*` methods expect `LabelOnGround` + `Entity`)
   - Reason: need real element/Entity types to exercise border cases.
   - Next actions: create small POCO test wrappers for those inputs (if feasible) or use gated runtime.

5) Tests flagged as legacy or heavy
   - Search for these markers:
     - `[Ignore("Requires` — tests intentionally ignored in the default suite.
     - `Assert.Inconclusive("ElementService depends` — tests that skip when ExileCore types are not present.

Guidance
- We deliberately prioritized keeping the default developer experience fast and green. If you want to increase coverage for the above areas, choose one of:
  1. Provision a gated CI runner with ExileCore/SharpDX and enable `Tests/CI-HEAVY.md` flow; or
  2. Refactor production code to separate pure-algorithm logic from runtime-bound code so that algorithmic parts can be unit-tested without ExileCore.

If you'd like, I can draft small adapter interfaces and a follow-up PR to decouple a few hot spots (for example, move `EvaluateAltarWeights` into a pure helper class that accepts data objects rather than `LabelOnGround` instances).
