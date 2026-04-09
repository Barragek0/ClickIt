<#
Runs the ClickIt tests with XPlat code coverage then produces a report using ReportGenerator.
It also extracts a simple CSV listing of files with the most missing lines.

Usage:
  pwsh -NoProfile -ExecutionPolicy Bypass -File ./Tests/Scripts/generate-coverage.ps1

Outputs:
    - Tests/TestResults/lcov.info
  - Tests/TestResults/coverage.cobertura.xml
  - Tests/TestResults/cov/Summary.xml
    - Tests/TestResults/covstats-xplat/index.html
  - Tests/TestResults/missing-files.csv
    - Tests/TestResults/uncovered-lines.csv
#>

param(
    [string] $configuration = 'Debug',
    [string] $resultsDir = "Tests/TestResults",
    [int] $topN = 50
)

function Get-ReportGeneratorLicense {
    $processValue = [Environment]::GetEnvironmentVariable('REPORTGENERATOR_LICENSE', [EnvironmentVariableTarget]::Process)
    if (-not [string]::IsNullOrWhiteSpace($processValue)) {
        return @{ Value = $processValue; Source = 'process' }
    }

    $userValue = [Environment]::GetEnvironmentVariable('REPORTGENERATOR_LICENSE', [EnvironmentVariableTarget]::User)
    if (-not [string]::IsNullOrWhiteSpace($userValue)) {
        return @{ Value = $userValue; Source = 'user' }
    }

    $machineValue = [Environment]::GetEnvironmentVariable('REPORTGENERATOR_LICENSE', [EnvironmentVariableTarget]::Machine)
    if (-not [string]::IsNullOrWhiteSpace($machineValue)) {
        return @{ Value = $machineValue; Source = 'machine' }
    }

    return $null
}

function Resolve-RepoRelativeCoveragePath {
    param(
        [string] $Path,
        [string] $RepoRoot
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $Path
    }

    try {
        $fullPath = [System.IO.Path]::GetFullPath($Path)
        $fullRepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
        $normalizedRepoRoot = $fullRepoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $repoPrefix = $normalizedRepoRoot + [System.IO.Path]::DirectorySeparatorChar

        if ($fullPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $fullPath.Substring($repoPrefix.Length)
        }

        return $fullPath
    }
    catch {
        return $Path
    }
}

Set-StrictMode -Version Latest

Write-Host "Running tests and collecting XPlat coverage (configuration=$configuration)"

& (Join-Path $PSScriptRoot 'invoke-dotnet-test-with-memory-guard.ps1') -ProjectPath 'Tests/ClickIt.Tests.csproj' -Configuration $configuration -AdditionalArgs @('--collect:XPlat Code Coverage', '--settings', 'runsettings.xml', '/p:CollectCoverage=false', '/p:BuildProjectReferences=false')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$coverageFiles = Get-ChildItem -Path Tests/TestResults -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue
if (-not $coverageFiles) { Write-Error "No coverage.cobertura.xml files found in Tests/TestResults"; exit 1 }

$coverageFiles = $coverageFiles | Sort-Object LastWriteTime -Descending
$chosen = $coverageFiles | Select-Object -First 1
if (-not $chosen) { Write-Error "No cobertura coverage file found to parse"; exit 1 }

$lcovFiles = Get-ChildItem -Path $resultsDir -Recurse -Filter 'lcov.info' -ErrorAction SilentlyContinue
if (-not $lcovFiles) { Write-Error "No lcov.info files found in $resultsDir"; exit 1 }

$lcovFiles = $lcovFiles | Sort-Object LastWriteTime -Descending
$chosenLcov = $lcovFiles | Select-Object -First 1
if (-not $chosenLcov) { Write-Error "No lcov.info file found to parse"; exit 1 }

$canonicalCoveragePath = Join-Path $resultsDir 'coverage.cobertura.xml'
if ($chosen.FullName -ne (Resolve-Path $canonicalCoveragePath -ErrorAction SilentlyContinue | ForEach-Object { $_.Path })) {
    Copy-Item $chosen.FullName $canonicalCoveragePath -Force
}

$canonicalLcovPath = Join-Path $resultsDir 'lcov.info'
if ($chosenLcov.FullName -ne (Resolve-Path $canonicalLcovPath -ErrorAction SilentlyContinue | ForEach-Object { $_.Path })) {
    Copy-Item $chosenLcov.FullName $canonicalLcovPath -Force
}

$summaryTargetDir = Join-Path $resultsDir 'cov'
$htmlTargetDir = Join-Path $resultsDir 'covstats-xplat'
if (-not (Test-Path $summaryTargetDir)) { New-Item -ItemType Directory -Path $summaryTargetDir -Force | Out-Null }
if (Test-Path $htmlTargetDir) {
    Remove-Item $htmlTargetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $htmlTargetDir -Force | Out-Null

Write-Host "Generating summary report using ReportGenerator into $summaryTargetDir"
$coverageList = $chosen.FullName
if (-not $coverageList) { Write-Error "No coverage files found to pass to ReportGenerator"; exit 1 }

$reportGeneratorLicense = Get-ReportGeneratorLicense
$licenseArg = $null
if ($null -ne $reportGeneratorLicense) {
    Write-Host "Using ReportGenerator license from $($reportGeneratorLicense.Source) environment scope"
    $licenseArg = "-license:$($reportGeneratorLicense.Value)"
}
else {
    Write-Warning "REPORTGENERATOR_LICENSE was not found in process, user, or machine environment scope. Premium metrics such as method coverage may be unavailable."
}

$summaryReportArgs = @(
    "-reports:$coverageList",
    "-targetdir:$summaryTargetDir",
    '-reporttypes:XmlSummary'
)
if ($null -ne $licenseArg) {
    $summaryReportArgs += $licenseArg
}

& reportgenerator @summaryReportArgs
if ($LASTEXITCODE -ne 0) { Write-Error "reportgenerator failed while generating XML summary"; exit $LASTEXITCODE }

Write-Host "Generating HTML report using ReportGenerator into $htmlTargetDir"
$htmlReportArgs = @(
    "-reports:$coverageList",
    "-targetdir:$htmlTargetDir",
    '-reporttypes:Html'
)
if ($null -ne $licenseArg) {
    $htmlReportArgs += $licenseArg
}

& reportgenerator @htmlReportArgs
if ($LASTEXITCODE -ne 0) { Write-Error "reportgenerator failed"; exit $LASTEXITCODE }

$summaryPath = Join-Path $summaryTargetDir 'Summary.xml'
if (-not (Test-Path $summaryPath)) { Write-Error "Summary.xml not found at $summaryPath"; exit 1 }

$htmlIndexPath = Join-Path $htmlTargetDir 'index.html'
if (-not (Test-Path $htmlIndexPath)) { Write-Error "index.html not found at $htmlIndexPath"; exit 1 }

$uncoveredLinesPath = Join-Path $resultsDir 'uncovered-lines.csv'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

Write-Host "Extracting uncovered LCOV lines to $uncoveredLinesPath"

$uncoveredLines = [System.Collections.Generic.List[object]]::new()
$currentSourceFile = $null

foreach ($rawLine in Get-Content $canonicalLcovPath) {
    if ($rawLine.StartsWith('SF:')) {
        $currentSourceFile = $rawLine.Substring(3)
        continue
    }

    if ($rawLine -eq 'end_of_record') {
        $currentSourceFile = $null
        continue
    }

    if (-not $rawLine.StartsWith('DA:') -or [string]::IsNullOrWhiteSpace($currentSourceFile)) {
        continue
    }

    $parts = $rawLine.Substring(3).Split(',')
    if ($parts.Count -lt 2) {
        continue
    }

    $lineNumber = 0
    $hitCount = 0
    if (-not [int]::TryParse($parts[0], [ref]$lineNumber)) {
        continue
    }

    if (-not [int]::TryParse($parts[1], [ref]$hitCount)) {
        continue
    }

    if ($hitCount -ne 0) {
        continue
    }

    $uncoveredLines.Add([PSCustomObject]@{
            Owner    = 'coverage'
            Severity = 'Information'
            Message  = '[code-coverage] line not covered'
            File     = Resolve-RepoRelativeCoveragePath -Path $currentSourceFile -RepoRoot $repoRoot
            Line     = $lineNumber
        }) | Out-Null
}

$uncoveredLines |
Sort-Object -Property File, Line |
Export-Csv -NoTypeInformation -Path $uncoveredLinesPath -Force

Write-Host "Extracting top $topN files by missing lines to missing-files.csv"


# Parse cobertura files produced by dotnet/xplat collector and compute per-file missing line counts
$stats = @{}
Write-Host "Parsing coverage file: $($chosen.FullName)"
foreach ($f in @($chosen)) {
    try {
        [xml]$c = Get-Content $f.FullName
    }
    catch {
        Write-Warning "Failed reading $($f.FullName): $_"
        continue
    }

    $classNodes = @($c.SelectNodes('//class[@filename]'))
    if (-not $classNodes -or $classNodes.Count -eq 0) { continue }

    foreach ($cl in $classNodes) {
        $filePathAttr = $cl.GetAttribute('filename')
        if (-not $filePathAttr) { continue }

        $lines = @($cl.SelectNodes('./lines/line'))
        $totalLines = @($lines).Count
        $missing = @($lines | Where-Object { $_.GetAttribute('hits') -eq '0' }).Count

        if ($stats.ContainsKey($filePathAttr)) {
            $stats[$filePathAttr].Total += $totalLines
            $stats[$filePathAttr].Missing += $missing
        }
        else {
            $stats[$filePathAttr] = [PSCustomObject]@{ File = $filePathAttr; Total = $totalLines; Missing = $missing }
        }
    }
}

$out = $stats.GetEnumerator() | ForEach-Object {
    $o = $_.Value
    [PSCustomObject]@{ File = $o.File; MissingLines = $o.Missing; TotalLines = $o.Total; MissingPct = [math]::Round((($o.Missing / [double]$o.Total) * 100), 2) }
}

$csvPath = Join-Path $resultsDir 'missing-files.csv'
$out | Sort-Object -Property MissingLines -Descending | Select-Object -First $topN | Export-Csv -NoTypeInformation -Path $csvPath -Force

Write-Host "Generated: $canonicalLcovPath, $summaryPath, $htmlIndexPath, $csvPath, and $uncoveredLinesPath"

# Optionally, output the top results to console
$out | Sort-Object -Property MissingLines -Descending | Select-Object -First 10 | Format-Table -AutoSize

Write-Host "Done."