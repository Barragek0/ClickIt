<#
Runs the standard local build-and-test loop for ClickIt and copies the built DLL to the
configured plugin directory.

Fast path notes:
- A single `dotnet build Tests/ClickIt.Tests.csproj --no-incremental -m` builds the WHOLE graph
  (product ClickIt.csproj + test harness GameOffsetsShim.csproj + tests) in one MSBuild invocation
  with parallel nodes — the old flow built each project in a separate serial `dotnet build`.
- Third-party decompile is owned by THIS script: it runs ONCE, in PARALLEL with the build, and the
  build always gets SkipThirdPartyDecompile=true so the test project's own decompile target does
  not re-run it (the decompiled sources are excluded from compilation anyway).
- Tests run with --no-build --no-restore (everything was just built; restore already ran).
#>

[CmdletBinding()]
param(
    [string] $SolutionPath = 'ClickIt.sln',
    [string] $ProductProjectPath = 'ClickIt.csproj',
    [string] $TestProjectPath = 'Tests/ClickIt.Tests.csproj',
    [string] $TestHarnessProjectPath = 'Tests/Harness/GameOffsetsShim/GameOffsetsShim.csproj',
    [string] $Configuration = 'Debug',
    [string] $ExapiPackagePath = '',
    [string] $PluginOutputPath = '',
    [string] $BuildTool = 'dotnet',
    [string] $DecompileScriptPath = '',
    [switch] $IncludeIntegrationTests,
    [switch] $SkipThirdPartyDecompile,
    [switch] $NoIncrementalBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

function Resolve-FullPath([string] $path) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        return ''
    }

    if ([System.IO.Path]::IsPathRooted($path)) {
        return $path
    }

    return Join-Path $repoRoot $path
}

function Get-MsBuildPath {
    $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
    $vswhere = $null

    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
    }

    if ($vswhere -and (Test-Path $vswhere)) {
        $resolved = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($resolved)) {
            return $resolved
        }
    }

    $msbuildCommand = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($null -ne $msbuildCommand) {
        return $msbuildCommand.Source
    }

    throw 'MSBuild.exe could not be resolved. Install Visual Studio Build Tools or ensure MSBuild is on PATH.'
}

function Invoke-Build {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectPath,
        [switch] $BuildProjectReferences
    )

    if ($BuildTool -eq 'msbuild-auto') {
        $msbuildPath = Get-MsBuildPath
        $buildArgs = @(
            $ProjectPath,
            "/p:Configuration=$Configuration"
        )

        if ($NoIncrementalBuild) {
            $buildArgs += '/t:Rebuild'
        }

        if ($IncludeIntegrationTests) {
            $buildArgs += '/p:IncludeIntegrationTests=true'
        }

        if (-not [string]::IsNullOrWhiteSpace($ExapiPackagePath)) {
            $buildArgs += "/p:exapiPackage=$ExapiPackagePath"
        }

        # Decompile is owned by THIS script (single parallel run below); always stop the test
        # project's RefreshThirdPartyDecompiledSources target from re-running it.
        $buildArgs += '/p:SkipThirdPartyDecompile=true'

        if (-not $BuildProjectReferences) {
            $buildArgs += '/p:BuildProjectReferences=false'
        }

        & $msbuildPath @buildArgs
        return
    }

    $buildArgs = @(
        'build',
        $ProjectPath,
        '-c',
        $Configuration
    )

    if ($NoIncrementalBuild) {
        $buildArgs += '--no-incremental'
    }

    if ($IncludeIntegrationTests) {
        $buildArgs += '/p:IncludeIntegrationTests=true'
    }

    if (-not [string]::IsNullOrWhiteSpace($ExapiPackagePath)) {
        $buildArgs += "/p:exapiPackage=$ExapiPackagePath"
    }

    # Decompile is owned by THIS script (single parallel run below); always stop the test
    # project's RefreshThirdPartyDecompiledSources target from re-running it.
    $buildArgs += '/p:SkipThirdPartyDecompile=true'

    if (-not $BuildProjectReferences) {
        $buildArgs += '/p:BuildProjectReferences=false'
    }

    # Parallel MSBuild nodes: the test project pulls the product + harness into one graph, and
    # MSBuild schedules the independent projects across nodes instead of building serially.
    $buildArgs += '-m'

    & dotnet @buildArgs
}

$resolvedSolutionPath = Resolve-FullPath $SolutionPath
$resolvedProductProjectPath = Resolve-FullPath $ProductProjectPath
$resolvedTestProjectPath = Resolve-FullPath $TestProjectPath
$resolvedTestHarnessProjectPath = Resolve-FullPath $TestHarnessProjectPath
$resolvedPluginOutputPath = Resolve-FullPath $PluginOutputPath
$resolvedDecompileScriptPath = Resolve-FullPath $DecompileScriptPath

# The decompiled sources under ThirdParty/Decompiled/ are excluded from both csproj compile sets,
# so decompiling is NOT a build input — start it in parallel with the build and join it afterwards.
# Start-Job is used (not Start-Process) so the script path with spaces needs no command-line quoting.
$decompileJob = $null
if (-not $SkipThirdPartyDecompile -and -not [string]::IsNullOrWhiteSpace($resolvedDecompileScriptPath)) {
    Write-Output 'Starting third-party decompile in parallel with the build...'
    $decompileJob = Start-Job -ScriptBlock {
        param($scriptPath)
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath 2>&1
        [pscustomobject]@{ Output = $output; ExitCode = $LASTEXITCODE }
    } -ArgumentList $resolvedDecompileScriptPath
}

# ONE graph build: the test project references the product (ClickIt.csproj) and the test harness
# (GameOffsetsShim.csproj), so a single build of it fully builds ALL three projects in one MSBuild
# graph with parallel nodes — no redundant second/third build of the same code.
Invoke-Build -ProjectPath $resolvedTestProjectPath -BuildProjectReferences

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if ($null -ne $decompileJob) {
    Write-Output 'Waiting for third-party decompile to finish...'
    Wait-Job $decompileJob | Out-Null
    $result = Receive-Job $decompileJob -Keep
    $decompileOutput = @($result.Output)
    if ($decompileJob.State -eq 'Failed' -or $result.ExitCode -ne 0) {
        $decompileOutput | Write-Output
        Remove-Job $decompileJob -Force
        if ($result.ExitCode -ne 0) {
            exit $result.ExitCode
        }
        exit 1
    }
    $decompileOutput | Write-Output
    Remove-Job $decompileJob -Force
    Write-Output 'Third-party decompile complete.'
}

$testArgs = @('--no-build', '--no-restore')

if ($IncludeIntegrationTests) {
    $testArgs += '-p:IncludeIntegrationTests=true'
}

if ($SkipThirdPartyDecompile) {
    $testArgs += '/p:SkipThirdPartyDecompile=true'
}

& (Join-Path $PSScriptRoot 'invoke-dotnet-test-with-memory-guard.ps1') -ProjectPath $resolvedTestProjectPath -Configuration $Configuration -AdditionalArgs $testArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$builtDllPath = Join-Path $repoRoot (Join-Path 'bin\Debug\net10.0-windows\win-x64' 'ClickIt.dll')
if (-not (Test-Path $builtDllPath)) {
    Write-Error "Compiled DLL not found at $builtDllPath"
    exit 1
}

if ([string]::IsNullOrWhiteSpace($resolvedPluginOutputPath)) {
    Write-Error 'PluginOutputPath is required.'
    exit 1
}

if (-not (Test-Path $resolvedPluginOutputPath)) {
    New-Item -ItemType Directory -Path $resolvedPluginOutputPath -Force | Out-Null
}

# Use atomic rename to replace the DLL even if it's locked.
# 1. Copy to a temp name (new file, no lock)
# 2. Wait until the temp file is accessible (antivirus scanning window)
# 3. Atomic rename via MoveFileEx replaces the directory entry
$tmpName = Join-Path $resolvedPluginOutputPath 'ClickIt.dll.new'
Copy-Item -Path $builtDllPath -Destination $tmpName -Force

# Wait until the temp file is fully released (avoids antivirus scanning race).
$maxWaitMs = 3000
$elapsed = 0
$step = 100
do {
    Start-Sleep -Milliseconds $step
    $elapsed += $step
    try {
        [System.IO.File]::OpenRead($tmpName).Dispose()
        break
    }
    catch {
        # File still locked by scanner — retry
    }
} while ($elapsed -lt $maxWaitMs)

Move-Item -Path $tmpName -Destination (Join-Path $resolvedPluginOutputPath 'ClickIt.dll') -Force
Write-Output "Copied $builtDllPath to $resolvedPluginOutputPath (atomic replace)"