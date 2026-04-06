param(
    [string]$InputCsv = "Tests/TestResults/per-test-coverage/per-test-coverage.csv",
    [string]$OutputCsv = "Tests/TestResults/per-test-coverage/per-test-coverage-keep-delete-review.csv",
    [string]$OutputMarkdown = "Tests/TestResults/per-test-coverage/per-test-coverage-keep-delete-review.md"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Convert-ToDouble {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return 0.0
    }

    return [double]$Value
}

function Get-Recommendation {
    param(
        [pscustomobject]$Row,
        [pscustomobject]$FileInfo
    )

    $reasons = New-Object System.Collections.Generic.List[string]

    if ($Row.Status -ne "Passed") {
        $reasons.Add("coverage run did not pass cleanly")
        return [pscustomobject]@{
            Recommendation = "Delete"
            Confidence     = "high"
            Reason         = ($reasons -join "; ")
        }
    }

    if ($Row.ProjectScorePct -ge 1.0) {
        $reasons.Add("material whole-project contribution")
        return [pscustomobject]@{
            Recommendation = "Keep"
            Confidence     = "high"
            Reason         = ($reasons -join "; ")
        }
    }

    if ($Row.ScorePct -ge 35.0) {
        $reasons.Add("strong local coverage for a focused owner")
        return [pscustomobject]@{
            Recommendation = "Keep"
            Confidence     = "high"
            Reason         = ($reasons -join "; ")
        }
    }

    if ($Row.TouchedFileCount -ge 4 -and $Row.ScorePct -ge 20.0) {
        $reasons.Add("covers a non-trivial interaction surface")
        return [pscustomobject]@{
            Recommendation = "Keep"
            Confidence     = "medium"
            Reason         = ($reasons -join "; ")
        }
    }

    if ($Row.ProjectScorePct -le 0.03 -and $Row.ScorePct -le 15.0 -and $Row.TouchedFileCount -le 2) {
        $reasons.Add("very low project contribution")
        $reasons.Add("weak local coverage")
        $reasons.Add("tiny touched surface")

        if ($FileInfo.Count -ge 3 -and $FileInfo.LowProjectCount -ge [math]::Ceiling($FileInfo.Count * 0.6)) {
            $reasons.Add("sits in a low-value cluster within the same test file")
        }

        return [pscustomobject]@{
            Recommendation = "Delete"
            Confidence     = "medium"
            Reason         = ($reasons -join "; ")
        }
    }

    if ($Row.ProjectScorePct -le 0.01 -and $Row.TouchedFileCount -eq 1 -and $Row.ScorePct -lt 25.0) {
        $reasons.Add("minimal project contribution")
        $reasons.Add("single-owner narrow assertion")
        return [pscustomobject]@{
            Recommendation = "Delete"
            Confidence     = "medium"
            Reason         = ($reasons -join "; ")
        }
    }

    if ($Row.ProjectScorePct -le 0.05 -and $Row.ScorePct -le 20.0 -and $Row.TouchedFileCount -le 2) {
        $reasons.Add("low contribution and thin local protection")
        return [pscustomobject]@{
            Recommendation = "Delete"
            Confidence     = "low"
            Reason         = ($reasons -join "; ")
        }
    }

    $reasons.Add("focused coverage is still plausible despite low project contribution")
    return [pscustomobject]@{
        Recommendation = "Keep"
        Confidence     = "low"
        Reason         = ($reasons -join "; ")
    }
}

$rows = Import-Csv $InputCsv

$typedRows = $rows | ForEach-Object {
    [pscustomobject]@{
        FullyQualifiedName = $_.FullyQualifiedName
        Kind               = $_.Kind
        SourceFile         = $_.SourceFile
        ProjectScorePct    = Convert-ToDouble $_.ProjectScorePct
        ScorePct           = Convert-ToDouble $_.ScorePct
        LinePct            = Convert-ToDouble $_.LinePct
        BranchPct          = Convert-ToDouble $_.BranchPct
        TouchedFileCount   = [int]$_.TouchedFileCount
        TopTouchedFiles    = $_.TopTouchedFiles
        Status             = $_.Status
    }
}

$fileStats = @{}
foreach ($group in ($typedRows | Group-Object SourceFile)) {
    $fileStats[$group.Name] = [pscustomobject]@{
        Count           = ($group.Group | Measure-Object).Count
        LowProjectCount = (($group.Group | Where-Object { $_.ProjectScorePct -le 0.03 } | Measure-Object).Count)
    }
}

$review = foreach ($row in ($typedRows | Sort-Object @(
            @{ Expression = { $_.ProjectScorePct }; Descending = $true },
            @{ Expression = { $_.ScorePct }; Descending = $true },
            @{ Expression = { $_.FullyQualifiedName }; Descending = $false }
        ))) {
    $decision = Get-Recommendation -Row $row -FileInfo $fileStats[$row.SourceFile]

    [pscustomobject]@{
        FullyQualifiedName = $row.FullyQualifiedName
        Recommendation     = $decision.Recommendation
        Confidence         = $decision.Confidence
        ProjectScorePct    = [math]::Round($row.ProjectScorePct, 2)
        ScorePct           = [math]::Round($row.ScorePct, 2)
        LinePct            = [math]::Round($row.LinePct, 2)
        BranchPct          = [math]::Round($row.BranchPct, 2)
        TouchedFileCount   = $row.TouchedFileCount
        Kind               = $row.Kind
        Reason             = $decision.Reason
        SourceFile         = $row.SourceFile
    }
}

$review | Export-Csv $OutputCsv -NoTypeInformation

$keepCount = (($review | Where-Object Recommendation -eq "Keep" | Measure-Object).Count)
$deleteCount = (($review | Where-Object Recommendation -eq "Delete" | Measure-Object).Count)
$highConfidenceDeleteCount = (($review | Where-Object {
            $_.Recommendation -eq "Delete" -and $_.Confidence -eq "high"
        } | Measure-Object).Count)

$topKeep = $review | Where-Object Recommendation -eq "Keep" | Sort-Object @(
    @{ Expression = { $_.ProjectScorePct }; Descending = $true },
    @{ Expression = { $_.ScorePct }; Descending = $true },
    @{ Expression = { $_.FullyQualifiedName }; Descending = $false }
) | Select-Object -First 20

$topDelete = $review | Where-Object Recommendation -eq "Delete" | Sort-Object @(
    @{ Expression = { $_.ProjectScorePct }; Descending = $false },
    @{ Expression = { $_.ScorePct }; Descending = $false },
    @{ Expression = { $_.FullyQualifiedName }; Descending = $false }
) | Select-Object -First 40

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# Per-Test Keep/Delete Review")
$markdown.Add("")
$markdown.Add("Single-pass review generated from per-test coverage metrics and redundancy-oriented heuristics.")
$markdown.Add("")
$markdown.Add("- Total reviewed: $($review.Count)")
$markdown.Add("- Keep: $keepCount")
$markdown.Add("- Delete: $deleteCount")
$markdown.Add("- High-confidence delete: $highConfidenceDeleteCount")
$markdown.Add("")
$markdown.Add("## Keep Summary")
$markdown.Add("")
$markdown.Add("| Test | Project contribution % | Local score % | Touched files | Confidence | Reason |")
$markdown.Add("| --- | ---: | ---: | ---: | --- | --- |")
foreach ($row in $topKeep) {
    $markdown.Add("| $($row.FullyQualifiedName) | $($row.ProjectScorePct) | $($row.ScorePct) | $($row.TouchedFileCount) | $($row.Confidence) | $($row.Reason) |")
}

$markdown.Add("")
$markdown.Add("## Delete Summary")
$markdown.Add("")
$markdown.Add("| Test | Project contribution % | Local score % | Touched files | Confidence | Reason |")
$markdown.Add("| --- | ---: | ---: | ---: | --- | --- |")
foreach ($row in $topDelete) {
    $markdown.Add("| $($row.FullyQualifiedName) | $($row.ProjectScorePct) | $($row.ScorePct) | $($row.TouchedFileCount) | $($row.Confidence) | $($row.Reason) |")
}

Set-Content $OutputMarkdown $markdown

Write-Output "Reviewed rows: $($review.Count)"
Write-Output "Keep: $keepCount"
Write-Output "Delete: $deleteCount"
Write-Output "High-confidence delete: $highConfidenceDeleteCount"