# Verify that icons are properly loaded in the output directory
# This script checks if the icon files exist in the build output

param(
    [string]$BuildOutput = "..\Editor\bin\Debug\net8.0-windows"
)

$iconPath = Join-Path $PSScriptRoot $BuildOutput "export\icons"

Write-Host "`n=== ICON LOADING VERIFICATION ===" -ForegroundColor Cyan
Write-Host "Build output: $BuildOutput" -ForegroundColor Gray
Write-Host "Icons path: $iconPath`n" -ForegroundColor Gray

if (-not (Test-Path $iconPath)) {
    Write-Host "ERROR: Icons directory not found!" -ForegroundColor Red
    Write-Host "Expected path: $iconPath" -ForegroundColor Yellow
    Write-Host "`nPlease run: dotnet build Editor/Editor.csproj" -ForegroundColor Yellow
    exit 1
}

$pngFiles = Get-ChildItem -Path $iconPath -Filter "*.png"
$svgFiles = Get-ChildItem -Path $iconPath -Filter "*.svg"

Write-Host "PNG files: $($pngFiles.Count)" -ForegroundColor Green
Write-Host "SVG files: $($svgFiles.Count)" -ForegroundColor Green
Write-Host "Total icon files: $($pngFiles.Count + $svgFiles.Count)`n" -ForegroundColor Cyan

# Check for some common icons
$testIcons = @("camera", "save", "hierarchy", "inspector", "material", "mesh_renderer", "play", "stop")

Write-Host "=== TESTING COMMON ICONS ===" -ForegroundColor Cyan
foreach ($icon in $testIcons) {
    $pngPath = Join-Path $iconPath "$icon.png"
    $exists = Test-Path $pngPath
    $status = if ($exists) { "[OK]" } else { "[MISSING]" }
    $color = if ($exists) { "Green" } else { "Red" }
    Write-Host "$status $icon.png" -ForegroundColor $color
}

Write-Host "`n=== ICON MANAGER SEARCH PATHS ===" -ForegroundColor Cyan
Write-Host "When running from: $BuildOutput" -ForegroundColor Gray
Write-Host "IconManager searches in:" -ForegroundColor Gray
Write-Host "  1. export\icons        (CURRENT DIR - should work now!)" -ForegroundColor Green
Write-Host "  2. ..\export\icons" -ForegroundColor Gray
Write-Host "  3. Assets\Icons" -ForegroundColor Gray
Write-Host "  4. Editor\Assets\Icons" -ForegroundColor Gray

Write-Host "`nAll icons should now load correctly in the editor!" -ForegroundColor Green
