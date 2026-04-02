# Tests — Guide for contributors ✅

This document explains testing conventions used in the repository and how to run the test suite and coverage tools locally.

Keep tests small, deterministic, and readable. Prefer behavior-focused internal contracts over wrapper-only seam tests, and avoid invoking native game APIs or assembly conflicts.

Patterns and conventions
- Use `Tests/TestUtils/TestBuilders.cs` and small stubs in `Tests/TestUtils` to construct test data.
- Prefer behavior-first access order for implementation details:
- 1) Public API.
- 2) Existing internal runtime/domain contracts.
- 3) Existing `*ForTests` methods when no stable runtime contract exists.
- 4) Reflection only as a last resort, and avoid adding new reflection helpers for runtime behavior.
- Prefer parameterized tests (`[DataTestMethod]` + `[DataRow(...)]`) for small logic branches — they are easier to read and increase coverage with minimal code.
- Avoid calling ExileCore native methods in tests. Where production code requires complex runtime objects, prefer explicit internal helper contracts or safe placeholders where explicitly valid.
- Avoid wrapper-only boundary tests when the same behavior is already covered through a stronger domain or application contract.
- Prefer namespaces that mirror the runtime domain under test instead of flattening new files into `ClickIt.Tests.Unit`.
- Avoid test names and placement that imply seam ownership; prefer behavior/domain naming aligned with runtime modules.

Running tests locally
- Run unit+integration tests (Debug):
```powershell
dotnet test Tests/ClickIt.Tests.csproj -c Debug -p:IncludeIntegrationTests=true
```

Collect coverage & generate reports
There is a helper script used throughout the repo at `Tests/Scripts/generate-coverage.ps1` which runs the tests with XPlat coverage and produces `Tests/TestResults/cov/Summary.xml` and `missing-files.csv`.

Guidelines for new tests
- Update `Tests/TestSuitePlan.md` when you add tests so we can monitor coverage targets.
- Prefer one Act step per test and use helper builders over shared mutable setup.
- When replacing a legacy wrapper API, move the test to the new owning domain instead of preserving the wrapper assertion.

CI notes
- This project ships a coverage gate workflow template `.github/workflows/coverage-gate.yml` which runs on pull requests and fails the check if the measured line coverage is lower than the saved baseline in `Tests/TestResults/baseline.md`.

Thanks — maintainers: keep tests readable and small — great tests are better than many brittle ones.
