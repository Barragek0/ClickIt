# Tests Guide

This file covers the repo's current test conventions, local test commands, and coverage flow.

Keep tests small, deterministic, and readable. Prefer behavior-focused tests around the real owner instead of seam tests around wrappers or compatibility shims.

## Test Isolation Policy

The test project should stay separate from the main project as much as possible.

- Keep test helpers, builders, reflection helpers, and setup code in `Tests/`.
- Do not add `*ForTests` methods, test-only fields, test-only flags, or test-only branches to production code.
- Do not wire test execution into the main project build.
- If tests need access to internal members, configure that from the test/build side instead of hardcoding test hooks into runtime code.
- Prefer testing through real owners and stable runtime boundaries before reaching for reflection.

## Conventions

- Use `Tests/Shared/TestUtils/TestBuilders.cs` and the helpers under `Tests/Shared/TestUtils/` when you need shared setup.
- Prefer this access order when testing behavior:
	1. public API
	2. existing internal runtime or domain contracts
	3. test-side reflection helpers when there is no stable runtime contract
- Prefer parameterized tests such as `[DataTestMethod]` and `[DataRow(...)]` for small branch-heavy logic.
- Avoid calling ExileCore native methods in tests.
- Avoid wrapper-only tests when the same behavior is already covered through a stronger domain or application-level test.
- Keep test namespaces aligned with the runtime owner instead of flattening them into broad buckets.
- Put support fixtures in `Tests/Shared/TestUtils/` unless they are tightly coupled to one feature.

## Running Tests Locally

Run unit and integration tests in Debug:

```powershell
dotnet test Tests\ClickIt.Tests.csproj -c Debug -p:IncludeIntegrationTests=true
```

If you want the same workflow the repo expects in VS Code, use the default `Build and Test` task from `.vscode/tasks.json`.

## Coverage

The repo has one common workspace coverage flow:

- VS Code task: `Review Coverage`
- Script: `Tests/Scripts/generate-coverage.ps1`

The script runs XPlat coverage, generates a ReportGenerator XML summary, and writes the usual outputs under `Tests/TestResults/`:

- `coverage.cobertura.xml`
- `cov/Summary.xml`
- `missing-files.csv`

Run it like this:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File ./Tests/Scripts/generate-coverage.ps1
```

## Writing New Tests

- Prefer one clear Act step per test.
- Use existing builders and stubs before inventing new fixture layers.
- When runtime code moves, move the closest tests with it.
- When replacing a legacy wrapper API, move the test to the real owning domain instead of preserving the wrapper assertion.
- If a test needs special access, prefer test-side reflection or test-project build configuration over adding a production seam.
- If coverage is missing, target the most meaningful uncovered branches first instead of adding lots of narrow tests.

## ExileCore Notes

- Use `Tests/Shared/TestUtils/ExileCoreOpaqueFactory.cs` when you need an ExileCore object only as an opaque reference token in a test.
- Before assuming an ExileCore property is safe on an uninitialized object, inspect the type with `Tests/Shared/TestUtils/ExileCoreMetadataInspector.cs` and check the opaque-usage notes.
- Treat `LabelOnGround`, `Entity`, and `GameController` as unsafe by default when a test branch dereferences runtime-backed members such as `Label`, `ItemOnGround`, `DistancePlayer`, `Path`, `EntityListWrapper`, or nested UI state.
- Prefer testing through delegates, ports, and existing runtime contracts before trying to fabricate deep ExileCore graphs.
- If a branch still needs remote-memory-backed members, record the exact blocker and move sideways to a nearby branch unless a test-side builder can supply the required graph without touching production code.

## CI Notes

CI is currently defined in `.github/workflows/ci.yml`.

That workflow currently:
- restores the test project
- validates coverage exclusion config sync
- builds the CI stubs
- runs tests with coverage
- generates coverage reports
- uploads `Tests/TestResults/` as an artifact

Keep tests readable and small. A few sharp behavior tests are better than a pile of brittle seam tests.
