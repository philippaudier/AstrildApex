$jsonPath = 'Editor\Icons\astrild-apex-icons.json'
if (-not (Test-Path $jsonPath)) { Write-Error "icons json not found: $jsonPath"; exit 1 }
$json = Get-Content -Raw -Path $jsonPath | ConvertFrom-Json
$keys = $json.icons.PSObject.Properties.Name
$out = 'icon_report.csv'
"iconKey,found,path" | Out-File $out -Encoding utf8
$search = @('export\icons','Assets\Icons','Editor\Assets\Icons')
foreach ($k in $keys) {
    $found = $null
    foreach ($d in $search) {
        foreach ($ext in @('.png', '.svg')) {
            $p = Join-Path $d ($k + $ext)
            if (Test-Path $p) { $found = $p; break }
        }
        if ($found) { break }
    }
    if ($found) { "$k,true,$found" | Out-File $out -Append -Encoding utf8 }
    else { "$k,false," | Out-File $out -Append -Encoding utf8 }
}
Write-Host "Wrote $out"
Get-Content $out | Select-Object -First 40 | ForEach-Object { Write-Host $_ }
