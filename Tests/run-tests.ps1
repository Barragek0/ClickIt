<#
Simple PowerShell wrapper to run the test project with clear output.
Usage:
  .\run-tests.ps1
#>
param()

Write-Host "Running ClickIt test suite..." -ForegroundColor Cyan

$testProj = Join-Path -Path $PSScriptRoot -ChildPath "ClickIt.Tests.csproj"
if (-not (Test-Path $testProj)) {
    Write-Error "Test project not found at $testProj"
    exit 2
}

& dotnet test $testProj --configuration Debug --no-build
$exitCode = $LASTEXITCODE
if ($exitCode -eq 0) {
    Write-Host "All tests passed." -ForegroundColor Green
} else {
    Write-Host "Tests failed with exit code $exitCode" -ForegroundColor Red
}
exit $exitCode
param(
    [string]$Filter = ''
)

# Simple helper to run the ClickIt test project with an optional filter.
Write-Host "Running ClickIt tests (Debug)" -ForegroundColor Cyan

$proj = "Tests/ClickIt.Tests.csproj"
if ([string]::IsNullOrEmpty($Filter)) {
    dotnet test $proj --configuration Debug
} else {
    dotnet test $proj --configuration Debug --filter "$Filter"
}
