Set-StrictMode -Version Latest
$root = Get-Item -Path .
$files = Get-ChildItem -Path $root -Recurse -Include *.cs -File | Where-Object { $_.FullName -notmatch '\\Tests\\' -and $_.FullName -notmatch '\\ThirdParty\\' -and $_.FullName -notmatch '\\\.github\\' -and $_.FullName -notmatch '\\bin\\' -and $_.FullName -notmatch '\\obj\\' }
$regex = [regex]'private\s+(?:static\s+)?[^\s]+\s+([A-Za-z0-9_]+)\s*\('
$methods = @()
foreach ($f in $files) {
  try { $content = Get-Content -Path $f.FullName -Raw -ErrorAction Stop } catch { continue }
  if ([string]::IsNullOrWhiteSpace($content)) { continue };
  foreach ($m in $regex.Matches($content)) { if ($m.Groups.Count -ge 2) { $methods += [PSCustomObject]@{ Name = $m.Groups[1].Value; DeclaredIn = $f.FullName } } }
}
$uniq = $methods | Select-Object -ExpandProperty Name -Unique
$report = @()
foreach ($name in $uniq) {
  $matches = git grep -n --line-number -I -e "\b$([regex]::Escape($name))\b" 2>$null | Out-String
  $lines = @()
  if (-not [string]::IsNullOrWhiteSpace($matches)) { $lines = $matches -split "`n" | Where-Object { $_ -ne '' } }
  $count = $lines.Count
  $declFiles = ($methods | Where-Object { $_.Name -eq $name } | Select-Object -ExpandProperty DeclaredIn -Unique)
  $report += [PSCustomObject]@{Name=$name; DeclaredIn = ($declFiles -join '; '); Occurrences = $count; Sample = ($lines | Select-Object -First 5) -join ' | '}
}
$report | Sort-Object Occurrences, Name | Format-Table -AutoSize

Write-Host "`n-- Methods with Occurrences <= 1 (candidate unused) --`n"
$report | Where-Object { $_.Occurrences -le 1 } | Sort-Object Occurrences, Name | Format-Table -AutoSize