# Shared Test Utilities

This folder contains shared mocks, factories and lightweight helpers used by the unit and integration tests.

Key files
- `TestUtilities.cs` - a set of small mocks and factories (MockClickItSettings, MockLabelFilterService, TestFactories, etc.). Reuse these helpers rather than creating new ad-hoc mocks.
- `TestStubs.cs`, `ComponentStubs.cs`, `ClickItStubs.cs` - additional lightweight stubs and fixtures.

Guidance
- Add small, focused helpers here if multiple tests need the same setup. Keep helpers deterministic.
- Avoid introducing heavy dependencies on runtime services (ExileCore) inside unit tests; prefer small test doubles in `TestUtilities.cs`.

Running tests
Use the project's test runner as usual:
```powershell
dotnet test "Tests/ClickIt.Tests.csproj" --configuration Debug
```
