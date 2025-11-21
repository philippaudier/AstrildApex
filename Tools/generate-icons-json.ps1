# Generate astrild-apex-icons.json from PNG files in export/icons
# This script scans all base PNG icons (excluding _active and _disabled variants)
# and creates a JSON file with minimal SVG fallbacks

param(
    [string]$IconsPath = "..\export\icons",
    [string]$OutputPath = "..\Editor\Icons\astrild-apex-icons.json"
)

Write-Host "Scanning icons in: $IconsPath" -ForegroundColor Cyan

# Get all PNG files excluding variants
$pngFiles = Get-ChildItem -Path $IconsPath -Filter "*.png" |
    Where-Object { $_.Name -notmatch "_active\.png$" -and $_.Name -notmatch "_disabled\.png$" } |
    Sort-Object Name

Write-Host "Found $($pngFiles.Count) base icons" -ForegroundColor Green

# Build icon entries
$icons = @{}
foreach ($file in $pngFiles) {
    $iconKey = $file.BaseName  # e.g., "camera_component"

    # Create a friendly name from the key
    $words = $iconKey -split '_'
    $friendlyName = ($words | ForEach-Object {
        if ($_.Length -gt 0) {
            $_.Substring(0,1).ToUpper() + $_.Substring(1)
        }
    }) -join ' '

    # Minimal SVG placeholder (will use PNG file instead via IconManager.FindIconFile)
    $svg = "<svg xmlns=`"http://www.w3.org/2000/svg`" viewBox=`"0 0 24 24`"><rect width=`"24`" height=`"24`" fill=`"currentColor`"/></svg>"

    $icons[$iconKey] = @{
        name = $friendlyName
        svg = $svg
        viewBox = "0 0 24 24"
    }
}

# Create the complete JSON structure
$jsonData = @{
    name = "AstrildApex Engine Icons"
    version = "2.0.0"
    description = "Complete icon set for AstrildApex game engine editor (PNG-based)"
    count = $icons.Count
    icons = $icons
}

# Convert to JSON with proper formatting
$jsonContent = $jsonData | ConvertTo-Json -Depth 10

# Write to file
$outputFullPath = Join-Path $PSScriptRoot $OutputPath
Write-Host "Writing JSON to: $outputFullPath" -ForegroundColor Cyan
$jsonContent | Out-File -FilePath $outputFullPath -Encoding UTF8

Write-Host "Successfully generated astrild-apex-icons.json with $($icons.Count) icons!" -ForegroundColor Green

# Display first few icons as sample
Write-Host "`nSample icons:" -ForegroundColor Yellow
$icons.Keys | Select-Object -First 10 | ForEach-Object {
    Write-Host "  - $_" -ForegroundColor Gray
}
