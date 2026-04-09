<#
Runs as a hidden sidecar monitor that watches the combined memory usage of all `testhost*`
processes. If the threshold is exceeded, it writes a trip file, kills the test hosts, and exits.

Usage:
    pwsh -NoProfile -ExecutionPolicy Bypass -File ./Tests/Scripts/memory-guard.ps1 \
        -MarkerPath 'Tests/TestResults/memory-guard/trip.json' \
        -StopFilePath 'Tests/TestResults/memory-guard/stop.signal'
#>

[CmdletBinding()]
param(
    [string] $MarkerPath = '',
    [string] $StopFilePath = '',
    [int] $ThresholdMB = 0,
    [int] $PollIntervalMs = 200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($ThresholdMB -le 0) {
    $parsedThreshold = 0
    if ([int]::TryParse($env:CLICKIT_TEST_MEMORY_THRESHOLD_MB, [ref]$parsedThreshold)) {
        $ThresholdMB = $parsedThreshold
    }
    else {
        $ThresholdMB = 2048
    }
}

if ($PollIntervalMs -lt 50) {
    throw 'PollIntervalMs must be at least 50ms.'
}

$thresholdBytes = [int64]$ThresholdMB * 1MB
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$artifactsDir = Join-Path $repoRoot 'Tests\TestResults\memory-guard'
New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($MarkerPath)) {
    $MarkerPath = Join-Path $artifactsDir 'trip.json'
}

if ([string]::IsNullOrWhiteSpace($StopFilePath)) {
    $StopFilePath = Join-Path $artifactsDir 'stop.signal'
}

function Resolve-FullPath([string] $path) {
    if ([System.IO.Path]::IsPathRooted($path)) {
        return $path
    }

    return Join-Path $repoRoot $path
}

$MarkerPath = Resolve-FullPath $MarkerPath
$StopFilePath = Resolve-FullPath $StopFilePath

$markerDirectory = Split-Path $MarkerPath -Parent
$stopDirectory = Split-Path $StopFilePath -Parent
if (-not [string]::IsNullOrWhiteSpace($markerDirectory)) {
    New-Item -ItemType Directory -Path $markerDirectory -Force | Out-Null
}
if (-not [string]::IsNullOrWhiteSpace($stopDirectory)) {
    New-Item -ItemType Directory -Path $stopDirectory -Force | Out-Null
}

Remove-Item $MarkerPath, $StopFilePath -Force -ErrorAction SilentlyContinue

function Get-TestHostProcesses {
    @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'testhost*' })
}

$maxPrivateBytes = 0L
$maxWorkingBytes = 0L
$tripWritten = $false

while (-not (Test-Path $StopFilePath)) {
    [System.Diagnostics.Process[]]$testHosts = @(Get-TestHostProcesses)
    $combinedPrivateBytes = 0L
    $combinedWorkingBytes = 0L

    if ($testHosts.Length -gt 0) {
        $privateStats = $testHosts | Measure-Object -Property PrivateMemorySize64 -Sum
        $workingStats = $testHosts | Measure-Object -Property WorkingSet64 -Sum

        if ($null -ne $privateStats -and $null -ne $privateStats.Sum) {
            $combinedPrivateBytes = [long]$privateStats.Sum
        }

        if ($null -ne $workingStats -and $null -ne $workingStats.Sum) {
            $combinedWorkingBytes = [long]$workingStats.Sum
        }
    }

    if ($null -eq $combinedPrivateBytes) {
        $combinedPrivateBytes = 0L
    }

    if ($null -eq $combinedWorkingBytes) {
        $combinedWorkingBytes = 0L
    }

    if ($combinedPrivateBytes -gt $maxPrivateBytes) {
        $maxPrivateBytes = $combinedPrivateBytes
    }

    if ($combinedWorkingBytes -gt $maxWorkingBytes) {
        $maxWorkingBytes = $combinedWorkingBytes
    }

    if ($combinedPrivateBytes -gt $thresholdBytes -or $combinedWorkingBytes -gt $thresholdBytes) {
        $thresholdCause = if ($combinedPrivateBytes -gt $thresholdBytes) {
            'combined private memory exceeded threshold'
        }
        else {
            'combined working set exceeded threshold'
        }

        $offenderSummaries = @($testHosts | Sort-Object PrivateMemorySize64 -Descending | ForEach-Object {
                [PSCustomObject]@{
                    ProcessName     = $_.ProcessName
                    Id              = $_.Id
                    PrivateMemoryMB = [math]::Round(($_.PrivateMemorySize64 / 1MB), 1)
                    WorkingSetMB    = [math]::Round(($_.WorkingSet64 / 1MB), 1)
                }
            })

        $payload = [PSCustomObject]@{
            ThresholdMB          = $ThresholdMB
            ThresholdCause       = $thresholdCause
            MaxCombinedPrivateMB = [math]::Round(($maxPrivateBytes / 1MB), 1)
            MaxCombinedWorkingMB = [math]::Round(($maxWorkingBytes / 1MB), 1)
            TriggeredAtUtc       = [DateTime]::UtcNow.ToString('o')
            Offenders            = $offenderSummaries
        }

        $payload | ConvertTo-Json -Depth 5 | Set-Content -Path $MarkerPath -Encoding UTF8
        $tripWritten = $true

        foreach ($testHost in @($testHosts | Sort-Object PrivateMemorySize64 -Descending)) {
            try {
                Stop-Process -Id $testHost.Id -Force -ErrorAction SilentlyContinue
            }
            catch {
            }
        }

        break
    }

    Start-Sleep -Milliseconds $PollIntervalMs
}

if ($tripWritten) {
    exit 86
}

exit 0