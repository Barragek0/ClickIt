[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-XmlValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$XPath
    )

    if (-not (Test-Path $Path)) {
        throw "File not found: $Path"
    }

    [xml]$xml = Get-Content -Path $Path -Raw
    $node = $xml.SelectSingleNode($XPath)
    if ($null -eq $node) {
        throw "XPath '$XPath' not found in '$Path'"
    }

    return $node.InnerText.Trim()
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$runsettingsPath = Join-Path $repoRoot "runsettings.xml"
$testCsprojPath = Join-Path $repoRoot "Tests\ClickIt.Tests.csproj"
$pluginCsprojPath = Join-Path $repoRoot "ClickIt.csproj"

$runsettingsExclude = Get-XmlValue -Path $runsettingsPath -XPath "//ExcludeByFile"
$testCsprojExclude = Get-XmlValue -Path $testCsprojPath -XPath "//ExcludeByFile"
$pluginCsprojExclude = Get-XmlValue -Path $pluginCsprojPath -XPath "//ExcludeByFile"

$allMatch = ($runsettingsExclude -eq $testCsprojExclude) -and ($runsettingsExclude -eq $pluginCsprojExclude)

if (-not $allMatch) {
    Write-Error "Coverage exclusion mismatch detected across runsettings/csproj files."
    Write-Host "runsettings.xml:"
    Write-Host $runsettingsExclude
    Write-Host "Tests/ClickIt.Tests.csproj:"
    Write-Host $testCsprojExclude
    Write-Host "ClickIt.csproj:"
    Write-Host $pluginCsprojExclude
    exit 1
}

Write-Host "Coverage exclusion configuration is synchronized across runsettings and csproj files."