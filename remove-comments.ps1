# Script to remove comments from all C# files
$files = Get-ChildItem -Path "." -Filter "*.cs" -Recurse

foreach ($file in $files) {
    Write-Host "Processing $($file.FullName)"
    $content = Get-Content $file.FullName
    
    $newContent = @()
    foreach ($line in $content) {
        # Remove lines that are purely comments (starting with //)
        if ($line -match '^\s*//') {
            continue
        }
        # Remove inline comments but keep the code part
        $line = $line -replace '\s*//.*$', ''
        # Remove empty lines that are just whitespace
        if ($line.Trim() -ne '') {
            $newContent += $line
        }
    }
    
    # Write back to file
    $newContent | Out-File -FilePath $file.FullName -Encoding utf8
}

Write-Host "Comment removal complete!"