# Run integration tests (PowerShell helper)
# Usage: .\run-integration-tests.ps1
# This runs the test project and includes the Integration folder via the MSBuild property.

$project = "Tests/ClickIt.Tests.csproj"
Write-Host "Running integration tests (IncludeIntegrationTests=true) against $project"

dotnet test $project --configuration Debug -p:IncludeIntegrationTests=true
