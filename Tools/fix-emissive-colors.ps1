# Fix EmissiveColor in existing materials
# This script updates all .material files that have EmissiveTexture but EmissiveColor = [0,0,0]

$materialsPath = "Materials"
$assetsPath = "Assets\Materials"

$paths = @($materialsPath, $assetsPath)
$fixedCount = 0
$totalCount = 0

foreach ($basePath in $paths) {
    if (-not (Test-Path $basePath)) {
        Write-Host "Path not found: $basePath" -ForegroundColor Yellow
        continue
    }

    $materialFiles = Get-ChildItem -Path $basePath -Filter "*.material" -Recurse

    foreach ($file in $materialFiles) {
        $totalCount++
        $content = Get-Content $file.FullName -Raw
        
        # Check if material has EmissiveTexture and EmissiveColor = [0,0,0]
        if ($content -match '"EmissiveTexture":\s*"[0-9a-fA-F-]+"' -and 
            $content -match '"EmissiveColor":\s*\[\s*0(?:\.0)?f?\s*,\s*0(?:\.0)?f?\s*,\s*0(?:\.0)?f?\s*\]') {
            
            Write-Host "Fixing: $($file.FullName)" -ForegroundColor Cyan
            
            # Replace EmissiveColor [0,0,0] with [1,1,1]
            $content = $content -replace '"EmissiveColor":\s*\[\s*0(?:\.0)?f?\s*,\s*0(?:\.0)?f?\s*,\s*0(?:\.0)?f?\s*\]', '"EmissiveColor":[1.0,1.0,1.0]'
            
            # Write back
            Set-Content -Path $file.FullName -Value $content -NoNewline
            $fixedCount++
            
            Write-Host "  Fixed EmissiveColor to [1,1,1]" -ForegroundColor Green
        }
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Total materials scanned: $totalCount" -ForegroundColor White
Write-Host "Fixed materials: $fixedCount" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

if ($fixedCount -gt 0) {
    Write-Host "`nEmissive colors fixed! Reimport your models or restart the editor to see changes." -ForegroundColor Green
} else {
    Write-Host "`nNo materials needed fixing." -ForegroundColor Green
}
