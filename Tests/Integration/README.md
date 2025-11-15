# Integration tests

This folder contains slower integration and end-to-end tests that are intentionally excluded from the default, lightweight test run.

Guidelines
- These tests may require more mocks or external dependencies; keep them deterministic where possible.
- They are excluded by default from `dotnet test` via entries in `Tests/ClickIt.Tests.csproj`.
- To run integration tests locally, either:
  - temporarily remove the corresponding `<Compile Remove=.../>` entries in `Tests/ClickIt.Tests.csproj`, or
  - run a targeted test command using the test filter (e.g., by trait or full name).

CI
- Integration tests are intended to be gated behind a separate CI job that provisions any heavy runtimes (ExileCore) or runs on a self-hosted Windows runner.

Files moved here:
- `ServiceIntegrationTests.cs` - multi-service integration checks
- `PerformanceTimingTests.cs` - timing and performance checks (opt-in)
- `ConfigurationIntegrationTests.cs` - settings and persistence integration
- `EndToEndScenarioTests.cs` - end-to-end workflow scenarios
- `AltarDecisionIntegrationTests.cs` - full altar decision validations
