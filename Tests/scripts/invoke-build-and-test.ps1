<#
Runs the standard local build-and-test loop for ClickIt and copies the built DLL to the
configured plugin directory.
#>

[CmdletBinding()]
param(
    [string] $SolutionPath = 'ClickIt.sln',
    [string] $TestProjectPath = 'Tests/ClickIt.Tests.csproj',
    [string] $Configuration = 'Debug',
    [string] $ExapiPackagePath = '',
    [string] $PluginOutputPath = '',
    [string] $BuildTool = 'dotnet',
    [string] $DecompileScriptPath = '',
    [switch] $IncludeIntegrationTests,
    [switch] $SkipThirdPartyDecompile
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

$resolvedSolutionPath = Resolve-FullPath $SolutionPath
$resolvedTestProjectPath = Resolve-FullPath $TestProjectPath
$resolvedPluginOutputPath = Resolve-FullPath $PluginOutputPath
$resolvedDecompileScriptPath = Resolve-FullPath $DecompileScriptPath

if (-not [string]::IsNullOrWhiteSpace($resolvedDecompileScriptPath)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $resolvedDecompileScriptPath
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if ($BuildTool -eq 'msbuild-auto') {
    $msbuildPath = Get-MsBuildPath
    $buildArgs = @(
        $resolvedSolutionPath,
        "/p:Configuration=$Configuration"
    )

    if ($IncludeIntegrationTests) {
        $buildArgs += '/p:IncludeIntegrationTests=true'
    }

    if (-not [string]::IsNullOrWhiteSpace($ExapiPackagePath)) {
        $buildArgs += "/p:exapiPackage=$ExapiPackagePath"
    }

    & $msbuildPath @buildArgs
}
else {
    $buildArgs = @(
        'build',
        $resolvedSolutionPath,
        '-c',
        $Configuration
    )

    if ($IncludeIntegrationTests) {
        $buildArgs += '/p:IncludeIntegrationTests=true'
    }

    if (-not [string]::IsNullOrWhiteSpace($ExapiPackagePath)) {
        $buildArgs += "/p:exapiPackage=$ExapiPackagePath"
    }

    if ($SkipThirdPartyDecompile) {
        $buildArgs += '/p:SkipThirdPartyDecompile=true'
    }

    & dotnet @buildArgs
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$testArgs = @('--no-build')

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

Copy-Item -Path $builtDllPath -Destination $resolvedPluginOutputPath -Force
Write-Output "Copied $builtDllPath to $resolvedPluginOutputPath"