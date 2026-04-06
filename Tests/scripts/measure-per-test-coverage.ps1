<#
Measures per-test coverage for the ClickIt MSTest suite by running filtered XPlat
coverage collection for each discovered test method and parsing the resulting
Cobertura report.

Why method-level discovery instead of display-name discovery?
- Standard [TestMethod] cases can be isolated directly by FullyQualifiedName.
- Under the current .NET 10 VSTest filter behavior in this repo, MSTest
  [DataTestMethod] display names with row arguments are not reliably filterable
  one-by-one, so those are scored at method granularity and marked accordingly.

Outputs:
- Tests/TestResults/per-test-coverage/per-test-coverage.csv
- Tests/TestResults/per-test-coverage/per-test-coverage.json
- Tests/TestResults/per-test-coverage/per-test-coverage-summary.md
#>

param(
    [string] $Configuration = 'Debug',
    [string] $Project = 'Tests/ClickIt.Tests.csproj',
    [string] $RunSettings = 'runsettings.xml',
    [string] $OutputDir = 'Tests/TestResults/per-test-coverage',
    [switch] $IncludeIntegrationTests,
    [switch] $NoBuild,
    [int] $MaxTests = 0,
    [string] $NameContains = '',
    [int] $ProgressEvery = 1,
    [int] $ShardCount = 1,
    [int] $ShardIndex = 0,
    [switch] $KeepRawResults,
    [bool] $ShowPerTestResults = $true,
    [string] $CoverageFormat = 'cobertura',
    [bool] $SingleHit = $true,
    [int] $MaxCpuCount = 1,
    [int] $MSTestWorkers = 1,
    [string] $DotNetVerbosity = 'quiet',
    [int] $ParallelWorkers = 1,
    [switch] $WorkerMode,
    [string] $TestAssemblyPath = '',
    [string] $WorkerLabel = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$includeIntegrationTestsEnabled = if ($PSBoundParameters.ContainsKey('IncludeIntegrationTests')) { $IncludeIntegrationTests.IsPresent } else { $true }
$noBuildEnabled = if ($PSBoundParameters.ContainsKey('NoBuild')) { $NoBuild.IsPresent } else { $true }

$scriptPath = $MyInvocation.MyCommand.Path
$scriptDirectory = if (-not [string]::IsNullOrWhiteSpace($scriptPath)) { Split-Path -Path $scriptPath -Parent } else { (Get-Location).Path }
$repoRoot = Split-Path -Path (Split-Path -Path $scriptDirectory -Parent) -Parent

if (-not [System.IO.Path]::IsPathRooted($Project)) {
    $Project = Join-Path $repoRoot $Project
}

if (-not [System.IO.Path]::IsPathRooted($RunSettings)) {
    $RunSettings = Join-Path $repoRoot $RunSettings
}

if (-not [System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = Join-Path $repoRoot $OutputDir
}

Set-Location -Path $repoRoot

function Get-TestMethods {
    param([string] $Root)

    $testFiles = Get-ChildItem -Path $Root -Recurse -Filter '*Tests.cs' | Sort-Object FullName
    $entries = New-Object System.Collections.Generic.List[object]

    foreach ($file in $testFiles) {
        $lines = Get-Content -Path $file.FullName
        $namespaceName = $null
        $className = $null
        $sawTestClass = $false
        $pendingKind = $null

        foreach ($line in $lines) {
            if (-not $namespaceName -and $line -match '^\s*namespace\s+([^\s{]+)') {
                $namespaceName = $Matches[1]
            }

            if ($line -match '^\s*\[TestClass\]') {
                $sawTestClass = $true
                continue
            }

            if ($sawTestClass -and $line -match '^\s*public\s+(?:sealed\s+|partial\s+|abstract\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)') {
                $className = $Matches[1]
                $sawTestClass = $false
                continue
            }

            if ($line -match '^\s*\[(TestMethod|DataTestMethod)\]') {
                $pendingKind = $Matches[1]
                continue
            }

            if ($pendingKind -and $line -match '^\s*public\s+(?:async\s+)?(?:void|Task(?:<[^>]+>)?)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(') {
                if ($namespaceName -and $className) {
                    $methodName = $Matches[1]
                    $entries.Add([PSCustomObject]@{
                            Namespace          = $namespaceName
                            ClassName          = $className
                            MethodName         = $methodName
                            FullyQualifiedName = "$namespaceName.$className.$methodName"
                            Kind               = if ($pendingKind -eq 'DataTestMethod') { 'DataTestMethod' } else { 'TestMethod' }
                            SourceFile         = $file.FullName
                        })
                }

                $pendingKind = $null
            }
        }
    }

    return $entries
}

function Get-CoverageSummary {
    param([string] $CoverageFile)

    [xml] $xml = Get-Content -Path $CoverageFile

    $packageRoot = $xml.SelectSingleNode('/coverage/packages')
    if ($null -eq $packageRoot) {
        return [PSCustomObject]@{
            ProjectLinePct       = 0
            ProjectBranchPct     = 0
            ProjectScorePct      = 0
            LinePct              = 0
            BranchPct            = 0
            ScorePct             = 0
            LinesCovered         = 0
            LinesValid           = 0
            BranchesCovered      = 0
            BranchesValid        = 0
            LocalLinesCovered    = 0
            LocalLinesValid      = 0
            LocalBranchesCovered = 0
            LocalBranchesValid   = 0
            TouchedClassCount    = 0
            TouchedFileCount     = 0
            TopTouchedFiles      = ''
        }
    }

    $classes = @($xml.SelectNodes('/coverage/packages/package/classes/class'))
    if ($classes.Count -eq 0) {
        return [PSCustomObject]@{
            ProjectLinePct       = [math]::Round(([double] $xml.coverage.'line-rate') * 100.0, 2)
            ProjectBranchPct     = [math]::Round(([double] $xml.coverage.'branch-rate') * 100.0, 2)
            ProjectScorePct      = 0
            LinePct              = 0
            BranchPct            = 0
            ScorePct             = 0
            LinesCovered         = [int] $xml.coverage.'lines-covered'
            LinesValid           = [int] $xml.coverage.'lines-valid'
            BranchesCovered      = [int] $xml.coverage.'branches-covered'
            BranchesValid        = [int] $xml.coverage.'branches-valid'
            LocalLinesCovered    = 0
            LocalLinesValid      = 0
            LocalBranchesCovered = 0
            LocalBranchesValid   = 0
            TouchedClassCount    = 0
            TouchedFileCount     = 0
            TopTouchedFiles      = ''
        }
    }

    $touchedClasses = @()
    $fileCoverage = @{}
    $localLinesCovered = 0
    $localLinesValid = 0
    $localBranchesCovered = 0
    $localBranchesValid = 0

    foreach ($class in $classes) {
        $classLines = @($class.lines.line)
        $coveredLines = @($classLines | Where-Object { [int] $_.hits -gt 0 }).Count
        if ($coveredLines -le 0) {
            continue
        }

        $touchedClasses += $class
        $localLinesCovered += $coveredLines
        $localLinesValid += $classLines.Count

        $fileName = [string] $class.filename
        if (-not $fileCoverage.ContainsKey($fileName)) {
            $fileCoverage[$fileName] = 0
        }

        $fileCoverage[$fileName] += $coveredLines

        foreach ($line in $classLines) {
            $coverageProperty = $line.PSObject.Properties['condition-coverage']
            $coverageText = if ($null -ne $coverageProperty) { [string] $coverageProperty.Value } else { '' }
            if ([string]::IsNullOrWhiteSpace($coverageText)) {
                continue
            }

            if ($coverageText -match '\((\d+)\/(\d+)\)') {
                $localBranchesCovered += [int] $Matches[1]
                $localBranchesValid += [int] $Matches[2]
            }
        }
    }

    $topFiles = $fileCoverage.GetEnumerator() |
    Sort-Object Value -Descending |
    Select-Object -First 3 |
    ForEach-Object { '{0} ({1})' -f $_.Key, $_.Value }

    $lineRate = [double] $xml.coverage.'line-rate'
    $branchRate = [double] $xml.coverage.'branch-rate'
    $linesCovered = [int] $xml.coverage.'lines-covered'
    $linesValid = [int] $xml.coverage.'lines-valid'
    $branchesCovered = [int] $xml.coverage.'branches-covered'
    $branchesValid = [int] $xml.coverage.'branches-valid'

    $projectScore = if ($branchesValid -gt 0) {
        [math]::Round((($lineRate + $branchRate) / 2.0) * 100.0, 2)
    }
    else {
        [math]::Round($lineRate * 100.0, 2)
    }

    $localLinePct = if ($localLinesValid -gt 0) {
        [math]::Round(($localLinesCovered / [double] $localLinesValid) * 100.0, 2)
    }
    else {
        0
    }

    $localBranchPct = if ($localBranchesValid -gt 0) {
        [math]::Round(($localBranchesCovered / [double] $localBranchesValid) * 100.0, 2)
    }
    else {
        0
    }

    $localScore = if ($localBranchesValid -gt 0) {
        [math]::Round((($localLinePct + $localBranchPct) / 2.0), 2)
    }
    else {
        $localLinePct
    }

    return [PSCustomObject]@{
        ProjectLinePct       = [math]::Round($lineRate * 100.0, 2)
        ProjectBranchPct     = [math]::Round($branchRate * 100.0, 2)
        ProjectScorePct      = $projectScore
        LinePct              = $localLinePct
        BranchPct            = $localBranchPct
        ScorePct             = $localScore
        LinesCovered         = $linesCovered
        LinesValid           = $linesValid
        BranchesCovered      = $branchesCovered
        BranchesValid        = $branchesValid
        LocalLinesCovered    = $localLinesCovered
        LocalLinesValid      = $localLinesValid
        LocalBranchesCovered = $localBranchesCovered
        LocalBranchesValid   = $localBranchesValid
        TouchedClassCount    = $touchedClasses.Count
        TouchedFileCount     = $fileCoverage.Count
        TopTouchedFiles      = ($topFiles -join '; ')
    }
}

function Remove-DirectoryIfPresent {
    param([string] $Path)

    if (Test-Path -Path $Path) {
        Remove-Item -Path $Path -Recurse -Force
    }
}

function Copy-DirectoryContents {
    param(
        [string] $Source,
        [string] $Destination
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $Source '*') -Destination $Destination -Recurse -Force
}

function Copy-DirectoryContentsWithRetry {
    param(
        [string] $Source,
        [string] $Destination,
        [int] $MaxAttempts = 20,
        [int] $DelayMilliseconds = 250
    )

    Remove-DirectoryIfPresent -Path $Destination
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null

    $sourceRoot = (Resolve-Path -Path $Source).Path
    $sourceItems = Get-ChildItem -Path $sourceRoot -Recurse -Force | Sort-Object FullName

    foreach ($item in $sourceItems) {
        $relativePath = $item.FullName.Substring($sourceRoot.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $targetPath = Join-Path $Destination $relativePath

        if ($item.PSIsContainer) {
            New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
            continue
        }

        $targetDirectory = Split-Path -Path $targetPath -Parent
        if (-not [string]::IsNullOrWhiteSpace($targetDirectory)) {
            New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        }

        $copied = $false
        $lastError = $null
        for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
            try {
                Copy-Item -Path $item.FullName -Destination $targetPath -Force
                $copied = $true
                break
            }
            catch {
                $lastError = $_
                if ($attempt -lt $MaxAttempts) {
                    [System.Threading.Thread]::Sleep($DelayMilliseconds)
                }
            }
        }

        if (-not $copied) {
            throw ('Failed to copy "{0}" to "{1}" after {2} attempts. {3}' -f $item.FullName, $targetPath, $MaxAttempts, $lastError.Exception.Message)
        }
    }
}

function Get-LogSnapshot {
    param(
        [string] $Path,
        [int] $LineCount
    )

    if (-not (Test-Path -Path $Path)) {
        return [PSCustomObject]@{
            Lines     = @()
            LineCount = $LineCount
        }
    }

    $lines = @(Get-Content -Path $Path)
    $startIndex = $LineCount
    if ($startIndex -ge $lines.Count) {
        return [PSCustomObject]@{
            Lines     = @()
            LineCount = $lines.Count
        }
    }

    return [PSCustomObject]@{
        Lines     = @($lines[$startIndex..($lines.Count - 1)])
        LineCount = $lines.Count
    }
}

function ConvertTo-NativeArgumentString {
    param([object[]] $Arguments)

    $escapedArguments = foreach ($argument in $Arguments) {
        $text = [string] $argument
        if ($text -match '[\s"]') {
            '"{0}"' -f ($text -replace '"', '\"')
        }
        else {
            $text
        }
    }

    return ($escapedArguments -join ' ')
}

function Test-ExclusiveFileAccess {
    param([string] $Path)

    if (-not (Test-Path -Path $Path)) {
        return $true
    }

    try {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $stream.Close()
        $stream.Dispose()
        return $true
    }
    catch {
        return $false
    }
}

function Format-RemainingTime {
    param([TimeSpan] $TimeSpan)

    if ($TimeSpan.TotalHours -ge 1) {
        return ('{0:hh\:mm\:ss}' -f $TimeSpan)
    }

    return ('{0:mm\:ss}' -f $TimeSpan)
}

function Write-PerTestProgress {
    param(
        [int] $Index,
        [int] $Total,
        [object] $Record,
        [TimeSpan] $Eta
    )

    $etaText = Format-RemainingTime -TimeSpan $Eta
    $durationText = '{0} ms' -f $Record.DurationMs

    if ($Record.Status -eq 'Passed') {
        $message = ('[{0}/{1}] {2} | score {3}% | line {4}% | branch {5}% | project {6}% | touched {7} files | {8} | ETA {9}' -f
            $Index, $Total, $Record.FullyQualifiedName, $Record.ScorePct, $Record.LinePct, $Record.BranchPct, $Record.ProjectScorePct, $Record.TouchedFileCount, $durationText, $etaText)
        Write-Output $message
        return
    }

    $notes = $Record.Notes
    if (-not [string]::IsNullOrWhiteSpace($notes) -and $notes.Length -gt 180) {
        $notes = $notes.Substring(0, 180) + '...'
    }

    $message = ('[{0}/{1}] {2} | {3} | {4} | ETA {5} | {6}' -f
        $Index, $Total, $Record.FullyQualifiedName, $Record.Status, $durationText, $etaText, $notes)
    Write-Output $message
}

function Write-ProgressMessage {
    param([string] $Message)

    if ([string]::IsNullOrWhiteSpace($WorkerLabel)) {
        Write-Output $Message
        return
    }

    Write-Output ('[{0}] {1}' -f $WorkerLabel, $Message)
}

function Get-CoverletCollectorPath {
    $packageRoot = Join-Path $env:USERPROFILE '.nuget\packages\coverlet.collector\6.0.1\build\net6.0'
    if (Test-Path -Path (Join-Path $packageRoot 'coverlet.collector.dll')) {
        return $packageRoot
    }

    $fallbackRoot = Join-Path $env:USERPROFILE '.nuget\packages\coverlet.collector\6.0.1\build\netstandard2.0'
    if (Test-Path -Path (Join-Path $fallbackRoot 'coverlet.collector.dll')) {
        return $fallbackRoot
    }

    throw 'coverlet.collector.dll was not found in the local NuGet cache.'
}

function Invoke-CoverageRun {
    param(
        [object] $Test,
        [string] $ResultDir
    )

    $filter = 'FullyQualifiedName={0}' -f $Test.FullyQualifiedName

    if (-not [string]::IsNullOrWhiteSpace($TestAssemblyPath)) {
        $collectorPath = Get-CoverletCollectorPath
        $testAdapterPath = Split-Path -Path $TestAssemblyPath -Parent
        $commandParts = @(
            'vstest', $TestAssemblyPath,
            '--collect:XPlat Code Coverage',
            ('--Settings:{0}' -f $RunSettings),
            ('--ResultsDirectory:{0}' -f $ResultDir),
            ('--TestCaseFilter:{0}' -f $filter),
            ('--TestAdapterPath:{0}' -f $testAdapterPath),
            ('--TestAdapterPath:{0}' -f $collectorPath),
            '/Parallel',
            '--',
            ('DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format={0}' -f $CoverageFormat),
            ('DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.SingleHit={0}' -f $SingleHit.ToString().ToLowerInvariant()),
            ('RunConfiguration.MaxCpuCount={0}' -f $MaxCpuCount),
            ('MSTest.Parallelize.Workers={0}' -f $MSTestWorkers),
            'MSTest.Parallelize.Scope=MethodLevel'
        )

        $outputLines = & dotnet @commandParts 2>&1
        return @($outputLines, $LASTEXITCODE)
    }

    $commandParts = @(
        'test', $Project,
        '-c', $Configuration,
        '--collect:XPlat Code Coverage',
        '--settings', $RunSettings,
        '--results-directory', $ResultDir,
        '--filter', $filter,
        '--nologo',
        '--verbosity', $DotNetVerbosity,
        '--logger:console;verbosity=minimal'
    )

    if ($noBuildEnabled) {
        $commandParts += '--no-build'
    }

    if ($includeIntegrationTestsEnabled) {
        $commandParts += '-p:IncludeIntegrationTests=true'
    }

    $commandParts += '-p:CollectCoverage=false'
    $commandParts += '-p:BuildProjectReferences=false'
    $commandParts += '--'
    $commandParts += ('DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format={0}' -f $CoverageFormat)
    $commandParts += ('DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.SingleHit={0}' -f $SingleHit.ToString().ToLowerInvariant())
    $commandParts += ('RunConfiguration.MaxCpuCount={0}' -f $MaxCpuCount)
    $commandParts += ('MSTest.Parallelize.Workers={0}' -f $MSTestWorkers)
    $commandParts += 'MSTest.Parallelize.Scope=MethodLevel'

    $outputLines = & dotnet @commandParts 2>&1
    return @($outputLines, $LASTEXITCODE)
}

function Write-SummaryFiles {
    param(
        [object[]] $ResultItems,
        [string] $CsvPath,
        [string] $JsonPath,
        [string] $SummaryPath,
        [TimeSpan] $Elapsed,
        [int] $EffectiveShardCount,
        [int] $EffectiveShardIndex
    )

    $ResultItems | Sort-Object ScorePct, LinesCovered, FullyQualifiedName | Export-Csv -Path $CsvPath -NoTypeInformation -Force
    $ResultItems | ConvertTo-Json -Depth 5 | Set-Content -Path $JsonPath

    $bottom10 = @($ResultItems | Sort-Object ScorePct, LinesCovered, FullyQualifiedName | Select-Object -First 10)
    $top10 = @($ResultItems | Sort-Object ScorePct, LinesCovered, FullyQualifiedName -Descending | Select-Object -First 10)
    $failed = @($ResultItems | Where-Object { $_.Status -ne 'Passed' })
    $aggregatedDataMethods = @($ResultItems | Where-Object { $_.Isolation -eq 'MethodAggregated' })
    $avgScore = if ($ResultItems.Count -gt 0) { [math]::Round((($ResultItems | Measure-Object -Property ScorePct -Average).Average), 2) } else { 0 }

    $summary = @()
    $summary += '# Per-Test Coverage Summary'
    $summary += ''
    $summary += ('- Shard: `{0}/{1}`' -f ($EffectiveShardIndex + 1), $EffectiveShardCount)
    $summary += ('- Measured test methods: `{0}`' -f $ResultItems.Count)
    $summary += ('- Average local score: `{0}%`' -f $avgScore)
    $summary += ('- Failed or missing coverage runs: `{0}`' -f $failed.Count)
    $summary += ('- Aggregated data-test methods: `{0}`' -f $aggregatedDataMethods.Count)
    $summary += ('- Total elapsed: `{0}`' -f $Elapsed)
    $summary += ''
    $summary += '## Lowest Scores'
    $summary += ''
    $summary += '| Local score | Local line % | Local branch % | Project contribution % | Test | Kind | Top touched files | Status |'
    $summary += '| ---: | ---: | ---: | ---: | --- | --- | --- | --- |'
    foreach ($item in $bottom10) {
        $summary += ('| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |' -f $item.ScorePct, $item.LinePct, $item.BranchPct, $item.ProjectScorePct, $item.FullyQualifiedName, $item.Isolation, ($item.TopTouchedFiles -replace '\|', '/'), $item.Status)
    }
    $summary += ''
    $summary += '## Highest Scores'
    $summary += ''
    $summary += '| Local score | Local line % | Local branch % | Project contribution % | Test | Kind | Top touched files | Status |'
    $summary += '| ---: | ---: | ---: | ---: | --- | --- | --- | --- |'
    foreach ($item in $top10) {
        $summary += ('| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |' -f $item.ScorePct, $item.LinePct, $item.BranchPct, $item.ProjectScorePct, $item.FullyQualifiedName, $item.Isolation, ($item.TopTouchedFiles -replace '\|', '/'), $item.Status)
    }
    $summary += ''
    $summary += '## Notes'
    $summary += ''
    $summary += '- `ScorePct` is a local score computed only over the lines and branches in the classes that the filtered test actually touched.'
    $summary += '- `ProjectScorePct` is the same filtered run scored against the whole product report, which is useful as a contribution metric but too harsh to use alone when judging focused tests.'
    $summary += '- `DataTestMethod` entries are currently measured at method granularity because the active .NET 10 VSTest filter path in this repo does not reliably isolate individual MSTest row display names.'
    $summary += '- `ScriptError` means the scorer hit a per-test script/parsing problem but continued measuring the rest of the suite.'
    $summary += '- Use the CSV for sorting and the JSON for follow-up automation.'

    $summary -join [Environment]::NewLine | Set-Content -Path $SummaryPath
}

function New-ResultRecord {
    param(
        [object] $Test,
        [double] $DurationMs,
        [string] $Status,
        [string] $Notes,
        [object] $Coverage = $null
    )

    return [PSCustomObject]@{
        Namespace            = $Test.Namespace
        ClassName            = $Test.ClassName
        MethodName           = $Test.MethodName
        FullyQualifiedName   = $Test.FullyQualifiedName
        Kind                 = $Test.Kind
        Isolation            = if ($Test.Kind -eq 'DataTestMethod') { 'MethodAggregated' } else { 'Method' }
        DurationMs           = $DurationMs
        ProjectLinePct       = if ($null -ne $Coverage) { $Coverage.ProjectLinePct } else { 0 }
        ProjectBranchPct     = if ($null -ne $Coverage) { $Coverage.ProjectBranchPct } else { 0 }
        ProjectScorePct      = if ($null -ne $Coverage) { $Coverage.ProjectScorePct } else { 0 }
        LinePct              = if ($null -ne $Coverage) { $Coverage.LinePct } else { 0 }
        BranchPct            = if ($null -ne $Coverage) { $Coverage.BranchPct } else { 0 }
        ScorePct             = if ($null -ne $Coverage) { $Coverage.ScorePct } else { 0 }
        LinesCovered         = if ($null -ne $Coverage) { $Coverage.LinesCovered } else { 0 }
        LinesValid           = if ($null -ne $Coverage) { $Coverage.LinesValid } else { 0 }
        BranchesCovered      = if ($null -ne $Coverage) { $Coverage.BranchesCovered } else { 0 }
        BranchesValid        = if ($null -ne $Coverage) { $Coverage.BranchesValid } else { 0 }
        LocalLinesCovered    = if ($null -ne $Coverage) { $Coverage.LocalLinesCovered } else { 0 }
        LocalLinesValid      = if ($null -ne $Coverage) { $Coverage.LocalLinesValid } else { 0 }
        LocalBranchesCovered = if ($null -ne $Coverage) { $Coverage.LocalBranchesCovered } else { 0 }
        LocalBranchesValid   = if ($null -ne $Coverage) { $Coverage.LocalBranchesValid } else { 0 }
        TouchedClassCount    = if ($null -ne $Coverage) { $Coverage.TouchedClassCount } else { 0 }
        TouchedFileCount     = if ($null -ne $Coverage) { $Coverage.TouchedFileCount } else { 0 }
        TopTouchedFiles      = if ($null -ne $Coverage) { $Coverage.TopTouchedFiles } else { '' }
        Status               = $Status
        Notes                = $Notes
        SourceFile           = $Test.SourceFile
    }
}

$tests = Get-TestMethods -Root (Join-Path $repoRoot 'Tests')

if (-not [string]::IsNullOrWhiteSpace($NameContains)) {
    $tests = @($tests | Where-Object {
            $_.FullyQualifiedName -like "*$NameContains*" -or $_.MethodName -like "*$NameContains*"
        })
}

if ($MaxTests -gt 0) {
    $tests = @($tests | Select-Object -First $MaxTests)
}

if ($ShardCount -lt 1) {
    throw 'ShardCount must be at least 1.'
}

if ($ParallelWorkers -lt 1) {
    throw 'ParallelWorkers must be at least 1.'
}

if ($ProgressEvery -lt 1) {
    $ProgressEvery = 1
}

if ($MaxCpuCount -lt 1) {
    throw 'MaxCpuCount must be at least 1.'
}

if ($MSTestWorkers -lt 1) {
    throw 'MSTestWorkers must be at least 1.'
}

if ($ShardIndex -lt 0 -or $ShardIndex -ge $ShardCount) {
    throw 'ShardIndex must be between 0 and ShardCount - 1.'
}

if ($ShardCount -gt 1) {
    $sharded = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $tests.Count; $i++) {
        if (($i % $ShardCount) -eq $ShardIndex) {
            $sharded.Add($tests[$i])
        }
    }

    $tests = @($sharded.ToArray())
}

if (-not $tests -or $tests.Count -eq 0) {
    throw 'No test methods matched the requested scope.'
}

if ($ParallelWorkers -gt 1 -and -not $WorkerMode) {
    if ([string]::IsNullOrWhiteSpace($scriptPath)) {
        throw 'Unable to resolve the scorer script path for parallel worker execution.'
    }

    $sourceBinDir = Join-Path (Split-Path -Path $Project -Parent) (Join-Path 'bin' (Join-Path $Configuration 'win-x64'))
    $sourceTestAssemblyPath = Join-Path $sourceBinDir 'ClickIt.Tests.dll'
    if (-not (Test-Path -Path $sourceTestAssemblyPath)) {
        throw ('Built test assembly not found: {0}. Run the test build first.' -f $sourceTestAssemblyPath)
    }

    $parallelRoot = Join-Path $OutputDir 'parallel-workers'
    New-Item -ItemType Directory -Path $parallelRoot -Force | Out-Null
    $runToken = '{0:yyyyMMdd-HHmmss}-{1}' -f (Get-Date), $PID
    $workerRoot = Join-Path $parallelRoot ('run-' + $runToken)
    New-Item -ItemType Directory -Path $workerRoot -Force | Out-Null
    $baseBinDir = Join-Path $workerRoot 'base-bin'
    Copy-DirectoryContentsWithRetry -Source $sourceBinDir -Destination $baseBinDir

    $powershellExe = Join-Path $PSHOME 'powershell.exe'
    if (-not (Test-Path -Path $powershellExe)) {
        $powershellExe = 'powershell.exe'
    }

    $workerStates = New-Object System.Collections.Generic.List[object]
    for ($workerIndex = 0; $workerIndex -lt $ParallelWorkers; $workerIndex++) {
        $workerBinDir = Join-Path $workerRoot ('worker-{0}\bin' -f ($workerIndex + 1))
        Copy-DirectoryContents -Source $baseBinDir -Destination $workerBinDir
        $workerAssemblyPath = Join-Path $workerBinDir 'ClickIt.Tests.dll'
        $workerName = 'W{0}' -f ($workerIndex + 1)
        $workerLogPath = Join-Path $workerRoot ('worker-{0}.stdout.log' -f ($workerIndex + 1))
        $workerErrorPath = Join-Path $workerRoot ('worker-{0}.stderr.log' -f ($workerIndex + 1))
        $workerCsvPath = Join-Path $OutputDir ('per-test-coverage.shard{0}-of-{1}.csv' -f ($workerIndex + 1), $ParallelWorkers)

        Remove-DirectoryIfPresent -Path $workerLogPath
        Remove-DirectoryIfPresent -Path $workerErrorPath
        Remove-DirectoryIfPresent -Path $workerCsvPath

        $workerArgs = @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', $scriptPath,
            '-Configuration', $Configuration,
            '-Project', $Project,
            '-RunSettings', $RunSettings,
            '-OutputDir', $OutputDir,
            '-MaxTests', $MaxTests,
            '-ProgressEvery', 1,
            '-ShardCount', $ParallelWorkers,
            '-ShardIndex', $workerIndex,
            '-CoverageFormat', $CoverageFormat,
            '-MaxCpuCount', 1,
            '-MSTestWorkers', 1,
            '-DotNetVerbosity', $DotNetVerbosity,
            '-ParallelWorkers', 1,
            '-WorkerMode',
            '-TestAssemblyPath', $workerAssemblyPath,
            '-WorkerLabel', $workerName
        )

        if (-not [string]::IsNullOrWhiteSpace($NameContains)) {
            $workerArgs += @('-NameContains', $NameContains)
        }

        if ($includeIntegrationTestsEnabled) {
            $workerArgs += '-IncludeIntegrationTests'
        }

        if ($noBuildEnabled) {
            $workerArgs += '-NoBuild'
        }

        $workerArgumentString = ConvertTo-NativeArgumentString -Arguments $workerArgs
        $process = Start-Process -FilePath $powershellExe -ArgumentList $workerArgumentString -RedirectStandardOutput $workerLogPath -RedirectStandardError $workerErrorPath -PassThru -WindowStyle Hidden
        $workerStates.Add([PSCustomObject]@{
                Name            = $workerName
                Process         = $process
                OutputPath      = $workerLogPath
                ErrorPath       = $workerErrorPath
                CsvPath         = $workerCsvPath
                OutputLineCount = 0
                ErrorLineCount  = 0
            })
    }

    $suiteStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    do {
        foreach ($workerState in $workerStates) {
            $outputSnapshot = Get-LogSnapshot -Path $workerState.OutputPath -LineCount $workerState.OutputLineCount
            $workerState.OutputLineCount = $outputSnapshot.LineCount
            foreach ($line in $outputSnapshot.Lines) {
                Write-Output $line
            }

            $errorSnapshot = Get-LogSnapshot -Path $workerState.ErrorPath -LineCount $workerState.ErrorLineCount
            $workerState.ErrorLineCount = $errorSnapshot.LineCount
            foreach ($line in $errorSnapshot.Lines) {
                Write-Output ('[{0}] ERROR {1}' -f $workerState.Name, $line)
            }
        }

        $runningWorkers = @($workerStates | Where-Object { -not $_.Process.HasExited })
        if ($runningWorkers.Count -gt 0) {
            Wait-Process -Id ($runningWorkers | ForEach-Object { $_.Process.Id }) -Timeout 1 -ErrorAction SilentlyContinue
        }
    } while ((@($workerStates | Where-Object { -not $_.Process.HasExited })).Count -gt 0)

    foreach ($workerState in $workerStates) {
        $workerState.Process.WaitForExit()
        $workerState.Process.Refresh()

        $outputSnapshot = Get-LogSnapshot -Path $workerState.OutputPath -LineCount $workerState.OutputLineCount
        $workerState.OutputLineCount = $outputSnapshot.LineCount
        foreach ($line in $outputSnapshot.Lines) {
            Write-Output $line
        }

        $errorSnapshot = Get-LogSnapshot -Path $workerState.ErrorPath -LineCount $workerState.ErrorLineCount
        $workerState.ErrorLineCount = $errorSnapshot.LineCount
        foreach ($line in $errorSnapshot.Lines) {
            Write-Output ('[{0}] ERROR {1}' -f $workerState.Name, $line)
        }
    }

    $failedWorkers = @($workerStates | Where-Object { -not (Test-Path -Path $_.CsvPath) })
    if ($failedWorkers.Count -gt 0) {
        throw ('One or more scorer workers did not complete successfully: {0}' -f (($failedWorkers | ForEach-Object { $_.Name }) -join ', '))
    }

    $suiteStopwatch.Stop()

    $csvParts = for ($workerIndex = 0; $workerIndex -lt $ParallelWorkers; $workerIndex++) {
        Join-Path $OutputDir ('per-test-coverage.shard{0}-of-{1}.csv' -f ($workerIndex + 1), $ParallelWorkers)
    }

    $allResults = @()
    foreach ($csvPart in $csvParts) {
        if (Test-Path -Path $csvPart) {
            $allResults += Import-Csv -Path $csvPart
        }
    }

    $csvPath = Join-Path $OutputDir 'per-test-coverage.csv'
    $jsonPath = Join-Path $OutputDir 'per-test-coverage.json'
    $summaryPath = Join-Path $OutputDir 'per-test-coverage-summary.md'
    Write-SummaryFiles -ResultItems $allResults -CsvPath $csvPath -JsonPath $jsonPath -SummaryPath $summaryPath -Elapsed $suiteStopwatch.Elapsed -EffectiveShardCount 1 -EffectiveShardIndex 0

    Write-Output ('Wrote: {0}' -f $csvPath)
    Write-Output ('Wrote: {0}' -f $jsonPath)
    Write-Output ('Wrote: {0}' -f $summaryPath)
    return
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$rawRoot = Join-Path $OutputDir 'raw'
if ($KeepRawResults) {
    New-Item -ItemType Directory -Path $rawRoot -Force | Out-Null
}
else {
    Remove-DirectoryIfPresent -Path $rawRoot
}

$results = New-Object System.Collections.Generic.List[object]
$suiteStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$counter = 0
$projectDirectory = Split-Path -Path $Project -Parent
$sharedProductAssembly = if (-not [string]::IsNullOrWhiteSpace($TestAssemblyPath)) {
    Join-Path (Split-Path -Path $TestAssemblyPath -Parent) 'ClickIt.dll'
}
else {
    Join-Path $projectDirectory (Join-Path (Join-Path 'bin' $Configuration) (Join-Path 'win-x64' 'ClickIt.dll'))
}

if (-not (Test-ExclusiveFileAccess -Path $sharedProductAssembly)) {
    Write-Warning ("Shared test output assembly is locked: {0}. Per-test coverage runs are likely to be very slow or fail to produce coverage until that process releases the file." -f $sharedProductAssembly)
}

Write-ProgressMessage -Message "Scoring $($tests.Count) test methods with filtered XPlat coverage..."

foreach ($test in $tests) {
    $counter++
    $safeName = ($test.FullyQualifiedName -replace '[^A-Za-z0-9_.-]', '_')
    $resultDir = if ($KeepRawResults) { Join-Path $rawRoot $safeName } else { Join-Path $env:TEMP ("ClickIt.PerTestCoverage." + $safeName) }
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        Remove-DirectoryIfPresent -Path $resultDir
        New-Item -ItemType Directory -Path $resultDir -Force | Out-Null

        $runResult = Invoke-CoverageRun -Test $test -ResultDir $resultDir
        $exitCode = [int] $runResult[-1]
        $outputLines = @($runResult[0..($runResult.Count - 2)])
        $stopwatch.Stop()
        $durationMs = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 2)
        $outputText = ($outputLines | Out-String)

        if ($exitCode -ne 0) {
            $results.Add((New-ResultRecord -Test $test -DurationMs $durationMs -Status 'Failed' -Notes ($outputText.Trim() -replace '\r?\n', ' | ')))
        }
        else {
            $coverageFile = Get-ChildItem -Path $resultDir -Recurse -Filter 'coverage.cobertura.xml' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if (-not $coverageFile) {
                $notes = 'coverage.cobertura.xml was not generated.'
                if ($outputText -match 'CoverletCoverageDataCollector: Failed to instrument modules') {
                    $notes = 'Coverlet failed to instrument or restore modules before writing coverage. A file lock on ClickIt.dll or another test output assembly is the likely cause.'
                }
                elseif ($outputText -match 'Could not find data collector') {
                    $notes = 'The XPlat Code Coverage data collector was not available to this run.'
                }

                $results.Add((New-ResultRecord -Test $test -DurationMs $durationMs -Status 'NoCoverageFile' -Notes $notes))
            }
            else {
                $coverage = Get-CoverageSummary -CoverageFile $coverageFile.FullName
                $notes = if ($test.Kind -eq 'DataTestMethod') { 'MSTest data rows are aggregated at method level under current VSTest filter behavior.' } else { '' }
                $results.Add((New-ResultRecord -Test $test -DurationMs $durationMs -Status 'Passed' -Notes $notes -Coverage $coverage))
            }
        }
    }
    catch {
        $stopwatch.Stop()
        $durationMs = [math]::Round($stopwatch.Elapsed.TotalMilliseconds, 2)
        $results.Add((New-ResultRecord -Test $test -DurationMs $durationMs -Status 'ScriptError' -Notes (($_ | Out-String).Trim() -replace '\r?\n', ' | ')))
    }
    finally {
        if (-not $KeepRawResults) {
            Remove-DirectoryIfPresent -Path $resultDir
        }
    }

    $latestRecord = $results[$results.Count - 1]
    $averageDurationMs = (@($results.ToArray()) | Measure-Object -Property DurationMs -Average).Average
    if ($null -eq $averageDurationMs) {
        $averageDurationMs = 0
    }

    $remainingCount = $tests.Count - $counter
    $remainingMs = [math]::Round($averageDurationMs * $remainingCount, 2)
    $eta = [TimeSpan]::FromMilliseconds([math]::Max(0, $remainingMs))

    if ($ShowPerTestResults) {
        Write-PerTestProgress -Index $counter -Total $tests.Count -Record $latestRecord -Eta $eta
    }

    if (($counter % $ProgressEvery) -eq 0 -or $counter -eq $tests.Count) {
        Write-ProgressMessage -Message ("Processed {0}/{1} test methods..." -f $counter, $tests.Count)
    }
}

$suiteStopwatch.Stop()

$resultItems = @($results.ToArray())
$fileSuffix = if ($ShardCount -gt 1) { '.shard{0}-of-{1}' -f ($ShardIndex + 1), $ShardCount } else { '' }
$csvPath = Join-Path $OutputDir ("per-test-coverage{0}.csv" -f $fileSuffix)
$jsonPath = Join-Path $OutputDir ("per-test-coverage{0}.json" -f $fileSuffix)
$summaryPath = Join-Path $OutputDir ("per-test-coverage-summary{0}.md" -f $fileSuffix)

Write-SummaryFiles -ResultItems $resultItems -CsvPath $csvPath -JsonPath $jsonPath -SummaryPath $summaryPath -Elapsed $suiteStopwatch.Elapsed -EffectiveShardCount $ShardCount -EffectiveShardIndex $ShardIndex

Write-ProgressMessage -Message ("Wrote: {0}" -f $csvPath)
Write-ProgressMessage -Message ("Wrote: {0}" -f $jsonPath)
Write-ProgressMessage -Message ("Wrote: {0}" -f $summaryPath)