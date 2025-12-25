# Mass UI Migration Script
# Replace all hardcoded Vector4 colors with ThemeManager.UI equivalents

# Common color replacements:
# new Vector4(0.2f, 1.0f, 0.2f, 1.0f) | new Vector4(0.3f, 1.0f, 0.3f, 1.0f) | new Vector4(0.4f, 1f, 0.4f, 1f) → UI.Success (green)
# new Vector4(1.0f, 0.8f, 0.2f, 1.0f) | new Vector4(1f, 0.8f, 0.2f, 1f) → UI.Warning (yellow/orange)
# new Vector4(1.0f, 0.3f, 0.3f, 1.0f) | new Vector4(1f, 0.3f, 0.3f, 1f) → UI.Error (red)
# new Vector4(0.4f, 0.7f, 1.0f, 1.0f) | new Vector4(0.5f, 0.8f, 1f, 1f) → UI.Info / UI.Primary (blue)
# new Vector4(0.7f, 0.7f, 0.7f, 1.0f) | new Vector4(0.5f, 0.5f, 0.5f, 1.0f) → UI.TextDisabled (gray)
# new Vector4(0.4f, 0.4f, 0.4f, 1.0f) → UI.Background (dark gray)

$inspectorFiles = Get-ChildItem -Path "Editor/Inspector" -Filter "*.cs" -Recurse

Write-Host "Found $($inspectorFiles.Count) inspector files"
Write-Host "Files that need 'using Editor.Themes;' added:"

foreach ($file in $inspectorFiles) {
    $content = Get-Content $file.FullName -Raw
    
    # Check if file has hardcoded colors
    $hasHardcodedColors = $content -match 'new Vector4\([\d\.f,\s]+\)'
    
    # Check if file already has using Editor.Themes
    $hasThemesUsing = $content -match 'using Editor\.Themes;'
    
    if ($hasHardcodedColors -and -not $hasThemesUsing) {
        Write-Host "  - $($file.Name)"
    }
}

Write-Host "`nTo add using statements and UITheme UI field to all inspector files:`n"
Write-Host "Run manual migration for remaining files."
