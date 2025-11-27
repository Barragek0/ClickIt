# Tests — Guide for contributors ✅

This document explains testing conventions used in the repository and how to run the test-suite and coverage tools locally.

Keep tests small, deterministic, and readable. Prefer test seams to avoid invoking native game APIs or assembly conflicts.

Patterns & conventions
- Use `Tests/TestUtils/TestBuilders.cs` and small stubs in `Tests/TestUtils` to construct test data.
- Prefer parameterized tests (`[DataTestMethod]` + `[DataRow(...)]`) for small logic branches — they are easier to read and increase coverage with minimal code.
- Avoid calling ExileCore native methods in tests. Where production code requires complex runtime objects, prefer seams (e.g., `TryGetVisibleLabelRect_ForTests`) or uninitialized placeholders where explicitly safe.
- When a test needs to call a private implementation for edge-case coverage, prefer the `_ForTests` seam method if available. If you must reflect into private members, keep tests clearly documented.

Running tests locally
- Run unit+integration tests (Debug):
```powershell
dotnet test Tests/ClickIt.Tests.csproj -c Debug -p:IncludeIntegrationTests=true
```

Collect coverage & generate reports
- There is a helper script used throughout the repo at `Tests/scripts/generate-coverage.ps1` which runs the tests with XPlat coverage and produces `Tests/TestResults/cov/Summary.xml` and `missing-files.csv`.

Guidelines for new tests
- Keep each test focused on a single behavior.
- Use parameterized tests for similar cases (good for Phase 5 consolidation).
- Update `Tests/TestSuitePlan.md` when you add tests so we can monitor coverage targets.

CI notes
- This project ships a coverage gate workflow template `.github/workflows/coverage-gate.yml` which runs on pull requests and fails the check if the measured line coverage is lower than the saved baseline in `Tests/TestResults/baseline.md`.

Thanks — maintainers: keep tests readable and small — great tests are better than many brittle ones.
