✅ Concrete step-by-step plan to reach 90% (line, branch, method)
This is an actionable plan broken down into phases — where each phase includes objectives, tasks, scripts to measure, and estimated effort.

Overview: I recommend an iterative, measurement-driven approach with frequent coverage checks (incremental PRs, small and safe changes) instead of attempting a single massive PR.

When implementing this plan you must:
- Avoid making changes to the main project where possible. Changes should ideally be isolated to the test project.
- UNDER NO CIRCUMSTANCES, change any functionality of any code in the main project. Every function still needs to work as it originally did before implementing any changes as part of this plan.
- If you do need to make changes to the main project, all new additions should be separated into a .seams.cs file instead of the actual class that tests are being made for. For example, if you were adding tests for LabelService, any code to allow the tests to function must be added to LabelService.Seams.cs and not the base file. You may need to update references in the projects original files to implement the new class, which is fine, as long as functionality remains the same.
- Remember that any tests added should be easy for developers to read and understand, and ideally, not use complicated methods like reflection to function. This is sometimes unavoidable, but it must be avoided whereever possible.
- Remember to update this plan, and update your personal github copilot to-do list after you complete each step, so that you ensure you can keep track of how far you have progressed into the plan.
- Remember to use the generate-coverage.ps1 script when needed.

Phase 0 — Baseline & tooling

Objective: stabilize coverage metric and find exact hotspots.

High level checklist (phase 0):

- [ ] Choose & lock-in a CI coverage pipeline (recommend: XPlat collector + ReportGenerator)
- [ ] Add a reproducible local script to generate per-file missing-lines/branch CSVs
- [ ] Add a CI job (or pipeline step) to upload the coverage summary outputs
- [ ] Run an initial baseline, capture the current coverage numbers and save them in TestResults/baseline.md

Commands (example) to create detailed file report (local):

dotnet test ClickIt.Tests.csproj -c Debug --collect:"XPlat Code Coverage" /p:IncludeIntegrationTests=false

reportgenerator -reports:Tests/TestResults/**coverage.cobertura.xml -targetdir:Tests/TestResults/cov -reporttypes:XmlSummary

PowerShell snippet to list files by missing lines:

powershell -NoProfile -Command "[xml]$s=Get-Content Tests/TestResults/cov/Summary.xml; $s.coverage.packages.package.classes.class | Select @{n='file';e={$.filename}},@{n='missed';e={[int]$.lines.'@missing'}}, @{n='lineRate';e={[double]$_.'@line-rate'}} | Sort-Object missed -Descending | Format-Table -AutoSize"

Why this matters
- Stabilizing a single coverage pipeline makes CI deterministic and allows tracking progress against a consistent baseline.

Next step (Phase 0 split):

- [x] 0.1: Add a reproducible script (dotnet + reportgenerator) that produces the Summary.xml and CSV/JSON outputs. (implemented at `Tests/scripts/generate-coverage.ps1`)
- [x] 0.2: Add a short PowerShell helper to extract top-N files by missing lines and output it to Tests/TestResults/missing-files.csv. (implemented in the script)
- [x] 0.3: Run the pipeline locally, produce baseline coverage, and commit Tests/TestResults/baseline.md with the numbers.

Phase 1 — Quick wins -- deliver 3—5% line boost & improve branches slightly

Objective: Add tests that are quick to write and unblock many lines/branches.

High level checklist (phase 1):

- [ ] Pick the first 3 small utilities to target (suggestion: TextHelpers, LabelUtils, WeightCalculator)
- [ ] Add parameterized [Theory] tests where appropriate
- [ ] Ensure tests are deterministic and rely on TestBuilders/TestUtils where possible
- [ ] Run tests, measure coverage; iterate until we get the expected 3–5% boost

Phase 1 split into concrete steps:

- [x] 1.1: Create a test file for `TextHelpers` covering empty/null inputs, trimming logic, and edge cases
- [x] 1.2: Add parameterized tests for `LabelUtils` including special-case strings and normalization
- [x] 1.3: Add tests for `WeightCalculator` edge cases (zero/negative weights, threshold boundaries)
- [x] 1.4: Convert a few small existing single-case unit tests to [Theory]s to multiply coverage
- [x] 1.5: Collect coverage and add a short report (Tests/TestResults/phase1.md) describing gains

Phase 2 — Medium-impact — deliver 4–7% branch and line coverage

Objective: Target decision-rich modules with medium difficulty.

High level checklist (phase 2):

- [ ] Expand unit tests to cover LabelFilterService, AreaService, and deeper WeightCalculator cases
- [ ] Create helper builders/stubs for ElementAdapter + UI element generation in Tests/TestUtils
- [ ] Add parameterized permutation tests for common code paths across these services

Phase 2 split into concrete steps:

	- [x] 2.1: Write focused tests for `LabelFilterService` covering chest vs world labels, weight ordering, and special-case rules
	- [x] 2.2: Add boundary & edge tests for `AreaService` (point inclusion, UI zone avoidance, border coordinates)
	- [x] 2.3: Add deeper `WeightCalculator` parameterized tests covering lower/higher thresholds, edge cases and negative/zero weights
	- [x] 2.4: Implement `IElementAdapter` test stub in `Tests/TestUtils` to facilitate tests without changes to the main project
	- [x] 2.5: Run coverage and capture the delta in Tests/TestResults/phase2.md
Phase 3 — Highest-impact, complex modules — deliver the largest branch increase

Objective: Target reason-heavy modules where tests will significantly increase branch coverage.

High level checklist (phase 3):

- [ ] Build full component-level unit/integration tests for `ClickService` and altar parsing/matching services
- [ ] Cover cache invalidation, partial matches, unknown mod handling, and multi-step flows
- [ ] Add integration-style tests that simulate a full decision flow from labels → parser → matcher → click decisions

Phase 3 split into concrete steps:

 - [x] 3.1: Add `ClickService` unit tests covering safety checks and click decision paths (UI avoidance / `PointIsInClickableArea`), emergency/recovery behavior without legacy input-blocking APIs, lazy-mode click limiting and rate-throttling, and interactions with `WeightCalculator` / `LabelFilterService` decision paths
 - [x] 3.2: Add `AltarParser`/`AltarMatcher` tests to exercise full matching logic with upsides/downsides and unknown mods
 - [x] 3.3: Add `AltarService` tests for caching behavior and invalidation paths
 - [x] 3.4: Add 10 integration-style tests that exercise the end-to-end decision pipeline by composing TestBuilders, IElementAdapter stubs and PluginContext test stubs
 - [x] 3.5: Run coverage and capture the delta in Tests/TestResults/phase3.md

Phase 4 — Rendering & edge conditions

Objective: Cover many conditional rendering paths that are currently not covered.

High level checklist (phase 4):

- [ ] Add tests to exercise `AltarDisplayRenderer` with many combinations of input data
- [ ] Add tests for `StrongboxRenderer` and `DebugRenderer` covering early-return conditions and debug toggles

Phase 4 split into concrete steps:

 - [x] 4.1: Create test fixture builders for renderer inputs (AltarComponent stubs, weight objects)
 - [x] 4.2: Add tests for `AltarDisplayRenderer` for upside-only/downside-only/partial/full matches and edge weights
 - [x] 4.3: Add tests for `StrongboxRenderer` early-return and full rendering paths
 - [x] 4.4: Add `DebugRenderer` tests for both debug flag on and off cases
 - [x] 4.5: Run coverage and capture the delta in Tests/TestResults/phase4.md

Phase 5 — Consolidation, cleanup, metrics

Objective: consolidate duplicated tests, remove low-value tests and ensure test quality.

High level checklist (phase 5):

- [ ] Consolidate duplicated tests into parameterized theories
- [ ] Add documentation to Tests/README.md explaining patterns and how to add new tests
- [ ] Add a coverage gate CI job that enforces a minimum acceptable coverage delta per PR

Phase 5 split into concrete steps:

- [x] 5.1: Identify duplicate tests and refactor into [Theory] parameterized suites
- [x] 5.2: Add or update Tests/README.md with contribution & testing patterns
- [x] 5.3: Add a CI coverage gate (or CI job) template and a small verification step to the repo
- [x] 5.4: Run coverage and capture the delta in Tests/TestResults/phase5.md

Phase 6 — Iteration until 90/90/90

Objective: iterate with focused tests for remaining low-coverage codeblocks until all metrics >= 90%.

High level checklist (phase 6):

- [ ] Review the per-file missing-lines report and pick the top 10 offenders
- [ ] Add targeted tests for the uncovered branches & edge cases until coverage rises above 90%
- [ ] Consider mutation testing (spot-check) to raise confidence in test effectiveness

Phase 6 split into concrete steps:

- [ ] 6.1: Generate a prioritized list of top-10 uncovered files and branches
- [ ] 6.2: Add focused tests and fix any test harness gaps needed to reach the branches
- [ ] 6.3: Consolidate all files and review all current tests. Merge / consolidate tests where possible and remove redundant tests that have no value. Validate overall coverage, if its below 90% / 90% / 90%, go back to step 6.1 and repeat
- [ ] 6.4: Validate overall coverage and produce a final summary Tests/TestResults/final-summary.md

Tracking & progress

I will track progress in this file as each checklist item gets completed, and keep a fast-moving Tests/Progress.md for day-to-day notes and numbers.

Next step: start with Phase 0, step 0.1 (add a reproducible coverage script in Tests/)

🧭 Test-writing strategy & practices (recommended)
Always measure before/after with the same coverage pipeline (XPlat + ReportGenerator).
Focus on branch coverage first — branch coverage is harder to increase and yields the biggest returns.
Prefer parameterized tests (xUnit [Theory] + [InlineData]) to cover many separate branches in fewer files.
Use TestBuilders/TestUtils to create standard test objects and stubs (there is already TestUtils/TestBuilders.cs).
Limit integration tests to scenarios that add unique coverage not reachable by unit tests.
Prefer non flakey, deterministic tests — avoid tests depending on system time or external resources. Mock behaviors or use TestStub/CI stubs.