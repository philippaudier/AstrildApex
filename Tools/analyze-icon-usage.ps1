# Analyze icon usage in the codebase and find mismatches
# This script finds all IconManager calls and checks if the icons exist

param(
    [string]$SourcePath = "..\Editor",
    [string]$IconsPath = "..\export\icons"
)

Write-Host "`n=== ICON USAGE ANALYSIS ===" -ForegroundColor Cyan

# Get all available icons (base PNG files only, no variants)
$availableIcons = Get-ChildItem -Path $IconsPath -Filter "*.png" |
    Where-Object { $_.Name -notmatch "_active\.png$" -and $_.Name -notmatch "_disabled\.png$" } |
    ForEach-Object { $_.BaseName } |
    Sort-Object

Write-Host "Available icons: $($availableIcons.Count)" -ForegroundColor Green

# Find all icon usage in C# files
$usagePattern = 'IconManager\.(RenderIcon|IconButton|GetIconTexture)\("([^"]+)"'
$csFiles = Get-ChildItem -Path $SourcePath -Recurse -Filter "*.cs"

$usedIcons = @{}
$missingIcons = @()
$fileUsages = @{}

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    $matches = [regex]::Matches($content, $usagePattern)

    foreach ($match in $matches) {
        $iconName = $match.Groups[2].Value
        $method = $match.Groups[1].Value

        # Track usage
        if (-not $usedIcons.ContainsKey($iconName)) {
            $usedIcons[$iconName] = 0
        }
        $usedIcons[$iconName]++

        # Track per-file usage
        $relPath = $file.FullName.Replace((Get-Location).Path, "").TrimStart("\")
        if (-not $fileUsages.ContainsKey($relPath)) {
            $fileUsages[$relPath] = @()
        }
        $fileUsages[$relPath] += "$method(`"$iconName`")"

        # Check if icon exists
        if ($availableIcons -notcontains $iconName) {
            $missingIcons += [PSCustomObject]@{
                Icon = $iconName
                File = $relPath
                Method = $method
            }
        }
    }
}

Write-Host "`n=== USAGE STATISTICS ===" -ForegroundColor Yellow
Write-Host "Total unique icons used: $($usedIcons.Count)" -ForegroundColor White
Write-Host "Total icon calls: $(($usedIcons.Values | Measure-Object -Sum).Sum)" -ForegroundColor White

Write-Host "`n=== USED ICONS ===" -ForegroundColor Green
$usedIcons.GetEnumerator() | Sort-Object Key | ForEach-Object {
    $status = if ($availableIcons -contains $_.Key) { "[OK]" } else { "[MISSING]" }
    $color = if ($availableIcons -contains $_.Key) { "Green" } else { "Red" }
    Write-Host "$status $($_.Key) (used $($_.Value) times)" -ForegroundColor $color
}

if ($missingIcons.Count -gt 0) {
    Write-Host "`n=== MISSING ICONS ===" -ForegroundColor Red
    Write-Host "The following icons are used but do NOT exist as PNG files:`n" -ForegroundColor Yellow

    $missingIcons | Group-Object Icon | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor Red
        $_.Group | ForEach-Object {
            Write-Host "      in $($_.File) via $($_.Method)" -ForegroundColor Gray
        }
    }

    Write-Host "`n=== SUGGESTED ACTIONS ===" -ForegroundColor Yellow
    Write-Host "1. Check if these icons have different names in export/icons/" -ForegroundColor White
    Write-Host "2. Create missing PNG files or rename existing ones" -ForegroundColor White
    Write-Host "3. Update the code to use correct icon names" -ForegroundColor White
}
else {
    Write-Host "`n✓ All used icons exist!" -ForegroundColor Green
}

Write-Host "`n=== UNUSED ICONS ===" -ForegroundColor Cyan
$unusedIcons = $availableIcons | Where-Object { -not $usedIcons.ContainsKey($_) }
if ($unusedIcons.Count -gt 0) {
    Write-Host "The following icons are available but NOT used in code:`n" -ForegroundColor Gray
    $unusedIcons | ForEach-Object {
        Write-Host "  - $_" -ForegroundColor DarkGray
    }
    Write-Host "`nTotal unused: $($unusedIcons.Count)/$($availableIcons.Count)" -ForegroundColor DarkGray
}
else {
    Write-Host "All available icons are being used!" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== FILE-BY-FILE USAGE ===" -ForegroundColor Cyan
$fileUsages.GetEnumerator() | Sort-Object Key | ForEach-Object {
    $uniqueIcons = $_.Value | Select-Object -Unique
    $iconCount = $uniqueIcons.Count
    $fileName = $_.Key
    Write-Host ""
    Write-Host "$fileName - $iconCount icons" -ForegroundColor White
    $uniqueIcons | Sort-Object | ForEach-Object {
        Write-Host "  - $_" -ForegroundColor Gray
    }
}

# Export report to CSV
$reportPath = Join-Path $PSScriptRoot "icon_usage_report.csv"
Write-Host ""
Write-Host "=== EXPORTING REPORT ===" -ForegroundColor Cyan
$report = @()
foreach ($kv in $usedIcons.GetEnumerator()) {
    $exists = $availableIcons -contains $kv.Key
    $report += [PSCustomObject]@{
        IconName = $kv.Key
        UsageCount = $kv.Value
        Exists = $exists
        Status = if ($exists) { "OK" } else { "MISSING" }
    }
}
$report | Export-Csv -Path $reportPath -NoTypeInformation
Write-Host "Report saved to: $reportPath" -ForegroundColor Green
