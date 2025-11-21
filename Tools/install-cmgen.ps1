<#
Downloads Filament release by tag, extracts cmgen.exe and configures Editor settings.
Usage:
  .\install-cmgen.ps1 -Tag v1.67.0 -Destination "C:\tools\filament" -Force
  or from repo root:
  .\Tools\install-cmgen.ps1 -Tag v1.67.0 -Destination "Tools\\cmgen"
#>
param(
    [string]$Tag = 'v1.67.0',
    [string]$Destination = "$PSScriptRoot\cmgen",
    [switch]$Force
)

function Write-Log { param($m) Write-Host "[install-cmgen] $m" }

$apiUrl = "https://api.github.com/repos/google/filament/releases/tags/$Tag"
Write-Log "Querying GitHub release $Tag"
try {
    $headers = @{ 'User-Agent' = 'AstrildApex-Installer' }
    $rel = Invoke-RestMethod -Uri $apiUrl -Headers $headers
} catch {
    Write-Log "Failed to query GitHub API: $_"
    exit 2
}

if (-not $rel.assets -or $rel.assets.Count -eq 0) {
    Write-Log "No assets found for release $Tag"
    exit 3
}

# Prefer asset containing "cmgen" or a windows archive
$asset = $null
foreach ($a in $rel.assets) {
    if ($a.name -match 'cmgen' -or $a.name -match 'CMGen') { $asset = $a; break }
}
if (-not $asset) {
    foreach ($a in $rel.assets) {
        if ($a.name -match 'windows' -and ($a.name -match '\.zip$' -or $a.name -match '\.7z$' -or $a.name -match '\.tar' )) { $asset = $a; break }
    }
}

if (-not $asset) {
    Write-Log "No suitable asset found. Available assets:";
    $rel.assets | ForEach-Object { Write-Host "  $($_.name) -> $($_.browser_download_url)" }
    exit 4
}

Write-Log "Selected asset: $($asset.name)"

# download
$tempFile = Join-Path -Path $env:TEMP -ChildPath ("filament_asset_" + [System.Guid]::NewGuid().ToString() + ".zip")
Write-Log "Downloading $($asset.browser_download_url) to $tempFile"
try {
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tempFile -Headers $headers -UseBasicParsing
} catch {
    Write-Log "Download failed: $_"
    exit 5
}

# extract to temp folder
$extractDir = Join-Path -Path $env:TEMP -ChildPath ("filament_extract_" + [System.Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $extractDir | Out-Null
Write-Log "Extracting to $extractDir"
try {
    Expand-Archive -Path $tempFile -DestinationPath $extractDir -Force
} catch {
    # try 7zip if available
    Write-Log "Expand-Archive failed, trying 7z..."
    $seven = Get-Command 7z -ErrorAction SilentlyContinue
    if ($seven) {
        & $seven.Path x $tempFile "-o$extractDir" -y | Out-Null
    } else {
        Write-Log "No extraction method available"
        exit 6
    }
}

# find cmgen.exe
Write-Log "Searching for cmgen.exe..."
$found = Get-ChildItem -Path $extractDir -Recurse -Filter "cmgen.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $found) {
    Write-Log "cmgen.exe not found inside the archive. Searching for 'cmgen' files"
    $found = Get-ChildItem -Path $extractDir -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'cmgen' } | Select-Object -First 1
}

if (-not $found) {
    Write-Log "cmgen not found. List files in extracted archive for debugging:";
    Get-ChildItem -Path $extractDir -Recurse | ForEach-Object { Write-Host $_.FullName }
    exit 7
}

Write-Log "Found cmgen at: $($found.FullName)"

# copy to destination
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$destExe = Join-Path -Path $Destination -ChildPath $found.Name
Write-Log "Copying cmgen to $destExe"
Copy-Item -Path $found.FullName -Destination $destExe -Force

# set execution permission (no-op on Windows)
try { icacls $destExe /grant "$($env:USERNAME):(RX)" | Out-Null } catch { }

# update ProjectSettings/EditorSettings.json
$settingsPath = Join-Path -Path (Join-Path -Path $PSScriptRoot -ChildPath "..\ProjectSettings") -ChildPath "EditorSettings.json"
# If script called from repo root, adjust
if (-not (Test-Path $settingsPath)) {
    $settingsPath = Join-Path -Path (Get-Location) -ChildPath "ProjectSettings\EditorSettings.json"
}
if (-not (Test-Path $settingsPath)) {
    Write-Log "EditorSettings.json not found at expected locations. Please set CmgenPath manually.";
    Write-Log "Destination cmgen path: $destExe"
    exit 0
}

Write-Log "Updating EditorSettings.json -> setting CmgenPath and enabling AutoGeneratePMREMOnImport"
try {
    $json = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if (-not $json.ExternalTools) { $json | Add-Member -NotePropertyName ExternalTools -NotePropertyValue @{ } }
    $json.ExternalTools.CmgenPath = $destExe
    $json.ExternalTools.AutoGeneratePMREMOnImport = $true
    $json | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8
    Write-Log "EditorSettings.json updated."
} catch {
    Write-Log "Failed to update EditorSettings.json: $_"
    Write-Log "Destination cmgen path: $destExe"
    exit 0
}

Write-Log "Install complete. You can now run the editor and imports will auto-generate PMREM." 
exit 0
