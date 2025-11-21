# List all available icons in the system
# Shows base icons, active variants, and disabled variants

param(
    [string]$IconsPath = "..\export\icons"
)

Write-Host "`n=== ASTRILD APEX ICON SYSTEM ===" -ForegroundColor Cyan
Write-Host "Icons directory: $IconsPath`n" -ForegroundColor Gray

# Get all PNG files
$allPngs = Get-ChildItem -Path $IconsPath -Filter "*.png" | Sort-Object Name

# Categorize icons
$baseIcons = $allPngs | Where-Object { $_.Name -notmatch "_active\.png$" -and $_.Name -notmatch "_disabled\.png$" }
$activeIcons = $allPngs | Where-Object { $_.Name -match "_active\.png$" }
$disabledIcons = $allPngs | Where-Object { $_.Name -match "_disabled\.png$" }

Write-Host "BASE ICONS: $($baseIcons.Count)" -ForegroundColor Green
Write-Host "Active variants: $($activeIcons.Count)" -ForegroundColor Yellow
Write-Host "Disabled variants: $($disabledIcons.Count)" -ForegroundColor Yellow
Write-Host "Total PNG files: $($allPngs.Count)`n" -ForegroundColor Cyan

Write-Host "=== ALL BASE ICONS (for use in IconManager) ===" -ForegroundColor Green
$baseIcons | ForEach-Object {
    $baseName = $_.BaseName
    $hasActive = Test-Path (Join-Path $IconsPath "${baseName}_active.png")
    $hasDisabled = Test-Path (Join-Path $IconsPath "${baseName}_disabled.png")

    $variants = @()
    if ($hasActive) { $variants += "active" }
    if ($hasDisabled) { $variants += "disabled" }

    $variantStr = if ($variants.Count -gt 0) { " [" + ($variants -join ", ") + "]" } else { "" }
    Write-Host "  - $baseName$variantStr" -ForegroundColor Gray
}

Write-Host "`n=== USAGE EXAMPLE ===" -ForegroundColor Cyan
Write-Host @"
In your C# code, use:

    IconManager.RenderIcon("camera");           // Renders camera.png
    IconManager.RenderIcon("camera_component"); // Renders camera_component.png
    IconManager.IconButton("save", "Save");     // Icon button with tooltip

The IconManager will:
1. Look for PNG files in export/icons first (camera.png, camera_active.png, etc.)
2. Fall back to SVG rendering if PNG not found

All 72 base icons are now available!
"@ -ForegroundColor Gray
