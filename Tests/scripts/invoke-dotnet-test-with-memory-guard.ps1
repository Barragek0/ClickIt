<#
Runs `dotnet test` normally while a hidden memory-guard sidecar watches `testhost*`
processes and trips if memory exceeds the configured threshold.
#>

[CmdletBinding()]
param(
    [string] $ProjectPath = 'Tests/ClickIt.Tests.csproj',
    [string] $Configuration = 'Debug',
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $AdditionalArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$guardDir = Join-Path $repoRoot 'Tests\TestResults\memory-guard'
New-Item -ItemType Directory -Path $guardDir -Force | Out-Null

$sessionId = [guid]::NewGuid().ToString('N')
$tripPath = Join-Path $guardDir ("trip-$sessionId.json")
$stopPath = Join-Path $guardDir ("stop-$sessionId.signal")

$monitor = $null
$testExitCode = 1

try {
    $monitor = Start-Process powershell -ArgumentList @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        (Join-Path $PSScriptRoot 'memory-guard.ps1'),
        '-MarkerPath',
        $tripPath,
        '-StopFilePath',
        $stopPath
    ) -WindowStyle Hidden -PassThru

    & dotnet test $ProjectPath -c $Configuration @AdditionalArgs
    $testExitCode = $LASTEXITCODE
}
finally {
    New-Item -ItemType File -Path $stopPath -Force | Out-Null

    if ($null -ne $monitor) {
        try {
            $null = $monitor.WaitForExit(1500)
            if (-not $monitor.HasExited) {
                Stop-Process -Id $monitor.Id -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
        }
    }
}

if (Test-Path $tripPath) {
    $trip = Get-Content $tripPath | ConvertFrom-Json
    Write-Error (
        'Memory guard killed testhost because {0} crossed the {1} MB threshold. Max private={2} MB; max working={3} MB.' -f
        $trip.ThresholdCause,
        $trip.ThresholdMB,
        $trip.MaxCombinedPrivateMB,
        $trip.MaxCombinedWorkingMB)
    exit 86
}

exit $testExitCode