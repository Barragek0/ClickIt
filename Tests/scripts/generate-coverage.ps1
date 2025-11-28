<#
Runs the ClickIt tests with XPlat code coverage then produces a report using ReportGenerator.
It also extracts a simple CSV listing of files with the most missing lines.

Usage:
  pwsh -NoProfile -ExecutionPolicy Bypass -File ./Tests/scripts/generate-coverage.ps1

Outputs:
  - Tests/TestResults/coverage.cobertura.xml
  - Tests/TestResults/cov/Summary.xml
  - Tests/TestResults/missing-files.csv
#>

param(
    [string] $configuration = 'Debug',
    [string] $resultsDir = "Tests/TestResults",
    [int] $topN = 50
)

Set-StrictMode -Version Latest

Write-Host "Running tests and collecting XPlat coverage (configuration=$configuration)"

dotnet test Tests/ClickIt.Tests.csproj --collect:"XPlat Code Coverage" --settings runsettings.xml -c $configuration /p:BuildProjectReferences=false
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet test failed"; exit $LASTEXITCODE }

$coverageFiles = Get-ChildItem -Path Tests/TestResults -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue
if (-not $coverageFiles) { Write-Error "No coverage.cobertura.xml files found in Tests/TestResults"; exit 1 }

$targetDir = Join-Path $resultsDir 'cov'
if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }

Write-Host "Generating reports using ReportGenerator into $targetDir"
$coverageList = ($coverageFiles | ForEach-Object { $_.FullName }) -join ';'
if (-not $coverageList) { Write-Error "No coverage files found to pass to ReportGenerator"; exit 1 }
reportgenerator -reports:"$coverageList" -targetdir:"$targetDir" -reporttypes:XmlSummary
if ($LASTEXITCODE -ne 0) { Write-Error "reportgenerator failed"; exit $LASTEXITCODE }

$summaryPath = Join-Path $targetDir 'Summary.xml'
if (-not (Test-Path $summaryPath)) { Write-Error "Summary.xml not found at $summaryPath"; exit 1 }

Write-Host "Extracting top $topN files by missing lines to missing-files.csv"


# Parse cobertura files produced by dotnet/xplat collector and compute per-file missing line counts
$stats = @{}
# Prefer the most recent cobertura report (avoid duplicating / merging multiple dated copies)
$coverageFiles = $coverageFiles | Sort-Object LastWriteTime -Descending
$chosen = $coverageFiles | Select-Object -First 1
if (-not $chosen) { Write-Error "No cobertura coverage file found to parse"; exit 1 }

Write-Host "Parsing coverage file: $($chosen.FullName)"
foreach ($f in @($chosen)) {
  try {
    [xml]$c = Get-Content $f.FullName
  } catch {
    Write-Warning "Failed reading $($f.FullName): $_"
    continue
  }

  $classes = $c.coverage.packages.package.classes.class -as [System.Collections.IEnumerable]
  if (-not $classes) { continue }

  foreach ($cl in $classes) {
    $filePathAttr = $cl.filename
    if (-not $filePathAttr) { continue }

    $lines = @($cl.lines.line)
    $totalLines = @($lines).Count
    $missing = @($lines | Where-Object { $_.hits -eq '0' }).Count

    if ($stats.ContainsKey($filePathAttr)) {
      $stats[$filePathAttr].Total += $totalLines
      $stats[$filePathAttr].Missing += $missing
    } else {
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

Write-Host "Generated: $summaryPath and $csvPath"

# Optionally, output the top results to console
$out | Sort-Object -Property MissingLines -Descending | Select-Object -First 10 | Format-Table -AutoSize

Write-Host "Done."
