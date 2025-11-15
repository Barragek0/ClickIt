# Test Suite Audit (automatically generated)

Generated: 2025-11-14 — focused audit pass

Scope and approach
- I scanned the active `Tests/` tree and reviewed representative files in high-density areas (Decision/WeightCalculator, Constants, LabelFilter/ClickService, AltarService). The goal was to identify overlapping tests, low-value tests, and consolidation opportunities while avoiding behavior regressions.

Executive summary
- Strengths: broad coverage across core domains (decision/weights, label filtering, altar parsing). Test helpers and stubs under `Tests/Shared/` are well used.
- Opportunities: significant duplication exists in the Decision/WeightCalculator area and the Constants area; the LabelFilter/ClickService surface area also contains many similar shallow tests that can be consolidated.
- Strategy: consolidate into canonical test files (parameterized where appropriate), move slow/heavy tests into an `Tests/Integration/` area (or tag them), and archive highly-redundant or heavy tests.

Recommendations (file groups and actions)

1) Decision / WeightCalculator — Consolidate (HIGH ROI)
- Keep canonical: `Tests/Decision/WeightCalculatorTests.cs` (make this the single source of truth).
- Merge into canonical: `WeightCalculatorMoreTests.cs`, `WeightCalculatorExtraTests.cs`, `WeightCalculatorCompositeTests.cs`, `WeightCalculatorAltarWeightsMixedTests.cs`, `WeightCalculatorAltarWeightsEdgeTests.cs`.
- Action: migrate cases using `DataTestMethod` + `DataRow` and shared fixtures from `Tests/Shared/TestUtilities.cs`.

2) Constants — Consolidate and de-duplicate
- Keep canonical: `Tests/Constants/AltarModsConstantsIntegrityTests.cs`.
- Merge: `Tests/Constants/ConstantsTests.cs` and any `ExtendedConstantsTests.cs` content into the canonical file.
- Remove redundant single-assert tests that duplicate checks in the canonical integrity test.

3) LabelFilter / ClickService — Reduce overlap
- Consolidate shallow tests: merge `LabelFilterServiceTests.cs`, `LabelFilterServiceEdgeTests.cs`, `LabelFilterServiceDeepTests.cs` into two classes: `LabelFilterServiceTests` (regular behavior) and `LabelFilterServiceEdgeTests` (rare/complex cases).
- Consolidate ClickService surface-level tests: keep `ClickServiceSurfaceTests.cs` as primary; move deep runtime/Heavy tests to `Tests/ARCHIVE/` or `Tests/Integration/`.

4) AltarService — keep but tidy
- `AltarServiceUtilsTests.cs` (private helper coverage via reflection) and `AltarServicePublicTests.cs` (public API behaviors) complement each other — keep both but move shared mocks/helpers to `Tests/Shared/`.

5) Locking / LockManager
- Consolidate `Utils/LockManagerTests.cs` and `Services/LockingTests.cs` into one canonical locking test suite under `Tests/Services/LockingTests.cs` if they duplicate scenarios.

6) Integration / Slow tests
- Move slow/integration tests into `Tests/Integration/` and update CI to exclude by default. Candidates: `ServiceIntegrationTests.cs`, `AltarDecisionIntegrationTests.cs`, `EndToEndScenarioTests.cs`, `PerformanceTimingTests.cs`.

7) Low-value / Redundant tests (candidates for removal)
- Remove or archive tests that do only trivial one-line asserts already covered by canonical tests (e.g., "collection is not empty" checks repeated across constants tests).
- `AdvancedEdgeCaseTests.cs` often contains many small historical scenarios; split or remove low-value cases and keep high-value simulations only.

Concrete action plan (safe, small batches)
1. Merge Decision/WeightCalculator files into `Decision/WeightCalculatorTests.cs`. Run test suite. (Recommended first step.)
2. Merge Constants checks into `Constants/AltarModsConstantsIntegrityTests.cs`. Run test suite.
3. Consolidate LabelFilter/ClickService shallow tests and archive heavy runtime tests.
4. Create `Tests/Integration/` and move slow tests there; add CI exclusion.

Immediate options I can implement now (pick one or more)
- Consolidate `Decision/WeightCalculatorMoreTests.cs` into `Decision/WeightCalculatorTests.cs` (parameterize and run tests). — recommended.
- Merge `Tests/Constants/ConstantsTests.cs` into `Tests/Constants/AltarModsConstantsIntegrityTests.cs` and remove duplicates.
- Move `Tests/Services/ClickServiceHeavyTests.cs` to `Tests/ARCHIVE/`.

Notes
- I purposely recommend archiving rather than immediate deletion to preserve history and make reversions easy.
- If you want, I can implement the first recommended change (Decision consolidation) in this branch and run the tests to show the result.

---

*End of audit*

- `Tests/Shared/ClickItStubs.cs` — Keep.
- `Tests/ServiceIntegrationTests.cs` — Keep (integration); de-duplicate helper usage if repeated across other integration tests.
- `Tests/ScreenAreaCalculationTests.cs` — Keep; ensure duplicate screen-area tests are merged.
- `Tests/RenderingAndUILogicTests.cs` — Keep.
- `Tests/README.md` — Keep.
- `Tests/PluginLifecycleTests.cs` — Keep.
- `Tests/PerformanceTimingTests.cs` — Keep; mark as performance and consider an opt-in slow flag.
- `Tests/PathMatchingAndClassificationTests.cs` — Keep; parameterize where appropriate.
- `Tests/InputSafetyAndValidationTests.cs` — Keep — consolidated with InputSafetyTests; consider merging.
- `Tests/ExtendedConstantsTests.cs` — Merge into `ConstantsTests.cs` if overlap exists.
- `Tests/ErrorHandlingTests.cs` — Keep.
- `Tests/EndToEndScenarioTests.cs` — Keep but categorize as slow/integration.
- `Tests/coverage.cobertura.xml` — Artifact; ignore.
- `Tests/ConstantsTests.cs` — Keep.
- `Tests/ConfigurationIntegrationTests.cs` — Keep (integration).
- `Tests/Services/LockManagerTests.cs` — DISABLED (merged into `Services/LockingTests.cs`).
- `Tests/Services/LockingTests.cs` — Keep (consolidated locking tests).
- `Tests/Services/InputSafetyTests.cs` — Keep (contains force-unblock tests consolidated).
- `Tests/Services/GlobalLockManagerTests.cs` — DISABLED (merged into `Services/LockingTests.cs`).
- `Tests/Services/ForceUnblockInputTests.cs` — DISABLED (merged into `InputSafetyTests.cs`).
- `Tests/Parser/ParserFuzzTests.cs` — Keep.
- `Tests/ClickIt.Tests.csproj` — Project file.
- `Tests/Constants/ConstantsIntegrityTests.cs` — Keep or merge with `ConstantsTests.cs`.
- `Tests/Constants/AltarModsConstantsIntegrityTests.cs` — Keep (specialized constants checks).
- `Tests/Decision/WeightCalculatorTests.cs` — Keep (consolidated with edge cases earlier).
- `Tests/Decision/WeightCalculatorEdgeCasesTests.cs` — (ensure disabled/merged) — (was merged earlier)
- `Tests/Decision/DecisionTests.cs` — Keep (new consolidated decision tests).
- `Tests/Decision/DecisionPropertyTests.cs` — DISABLED (merged into `DecisionTests.cs`).
- `Tests/Decision/DecisionEngineFuzzTests.cs` — DISABLED (merged into `DecisionTests.cs`).
- `Tests/Decision/AltarWeightsTests.cs` — Keep.
- `Tests/Concurrency/AltarProcessingConcurrencyTests.cs` — Keep (concurrency).
- `Tests/Components/SecondaryAltarComponentTests.cs` — Keep.
- `Tests/Configuration/ModTiersSerializationTests.cs` — Keep (serialization-specific).
- `Tests/Configuration/ExportImportEdgeCasesTests.cs` — Keep.
- `Tests/Configuration/DefaultWeightInitializationTests.cs` — Keep (consolidated initialization checks added).
- `Tests/Configuration/DefaultWeightCountsTests.cs` — Keep.
- `Tests/Configuration/ClickItSettings_GetModTierTests.cs` — Keep.

Recommendations / Next steps (safe, small-batch plan):
1. Create `Tests/Shared/Audit.md` (this file) — done.
2. Convert obvious repeats into parameterized MSTest `DataRow` tests (
   e.g., multiple `GetModTier` variants, constants presence checks).
3. Consolidate remaining small files in each domain into one per domain (Configuration, Constants, Components) preserving specialized tests (serialization/integration).
4. Add an opt-in slow test category: tag long/integration tests with a custom category attribute or move them under `Tests/Integration` and update CI to exclude by default.
5. Add `Tests/Shared/TestHelpers.cs` only if additional shared utilities are detected as duplicated (we already have `TestUtilities.cs` — reuse it).

If you want I can now implement step 2 (parameterize a couple of small repetitive tests) and then step 3 (merge small configuration tests) in small batches, running the test suite after each batch.
