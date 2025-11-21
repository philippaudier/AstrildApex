param(
    [Parameter(Mandatory=$true)]
    [string]$SourceFolder,
    [switch]$ConvertWithInkscape,
    [string]$InkScapePath,
    [switch]$PersistInkScapePath,
    [switch]$NoClear
)

Write-Host "Importing icons from: $SourceFolder"
if (-not (Test-Path $SourceFolder)) {
    Write-Error "Source folder not found"
    exit 1
}

$projRoot = Get-Location
$targets = @(
    (Join-Path $projRoot 'export\icons'),
    (Join-Path $projRoot 'Assets\Icons'),
    (Join-Path $projRoot 'Editor\Assets\Icons')
)

foreach ($t in $targets) {
    if (-not (Test-Path $t)) { New-Item -ItemType Directory -Path $t | Out-Null }
}

# By default clear the export/icons folder to avoid stale icons unless -NoClear is specified
if (-not $NoClear.IsPresent) {
    $exportDir = $targets[0]
    Write-Host "Clearing target folder: $exportDir"
    Get-ChildItem -Path $exportDir -File -Force | Remove-Item -Force
}

$svgs = Get-ChildItem -Path $SourceFolder -Filter *.svg -File
if ($svgs.Count -eq 0) {
    Write-Warning "No SVG files found in source folder"
    exit 0
}

# If requested, persist the inkscape path once before conversions (affects new shells)
if ($ConvertWithInkscape -and $PersistInkScapePath.IsPresent) {
    if (-not [string]::IsNullOrWhiteSpace($InkScapePath)) {
        Write-Host "Persisting INKSCAPE_PATH to user environment: $InkScapePath"
        & setx INKSCAPE_PATH "$InkScapePath" | Out-Null
        Write-Host "Persisted INKSCAPE_PATH (will be available in new shells)."
        $env:INKSCAPE_PATH = $InkScapePath
    } elseif (-not [string]::IsNullOrWhiteSpace($env:INKSCAPE_PATH)) {
        Write-Host "Persist switch provided; persisting existing INKSCAPE_PATH: $($env:INKSCAPE_PATH)"
        & setx INKSCAPE_PATH "$($env:INKSCAPE_PATH)" | Out-Null
        Write-Host "Persisted INKSCAPE_PATH (will be available in new shells)."
    } else {
        Write-Warning "-PersistInkScapePath was specified but no path was provided and INKSCAPE_PATH is not set in the environment. Provide -InkScapePath to persist."
    }
}

foreach ($svg in $svgs) {
    foreach ($t in $targets) {
        Copy-Item -Path $svg.FullName -Destination (Join-Path $t $svg.Name) -Force
    }

    if ($ConvertWithInkscape) {
        # Try to create PNG at 24px using inkscape if available. Resolve path precedence:
        # 1. Parameter -InkScapePath, 2. env:INKSCAPE_PATH, 3. 'inkscape' (on PATH)
        $out = Join-Path $targets[0] ($svg.BaseName + '.png')
        try {
                $inkPath = $null
                if (-not [string]::IsNullOrWhiteSpace($InkScapePath)) { $inkPath = $InkScapePath }
                elseif (-not [string]::IsNullOrWhiteSpace($env:INKSCAPE_PATH)) { $inkPath = $env:INKSCAPE_PATH }
                else { $inkPath = 'inkscape' }

                # (Persisting of INKSCAPE_PATH is handled once before the conversion loop if requested)

                # Use Start-Process to capture exit status; some inkscape versions return non-zero but still produce output
                $args = @("$($svg.FullName)", "--export-filename=$out", "--export-width=24", "--export-height=24")
                $proc = Start-Process -FilePath $inkPath -ArgumentList $args -NoNewWindow -PassThru -Wait -ErrorAction Stop
                if (-not (Test-Path $out)) {
                    Write-Warning "Inkscape conversion did not produce output for $($svg.Name) (ExitCode=$($proc.ExitCode))"
                } else {
                    if ($proc.ExitCode -ne 0) { Write-Host "Inkscape produced PNG with ExitCode=$($proc.ExitCode) for $($svg.Name)" }
                }
            }
            catch {
                Write-Warning "Inkscape conversion failed for $($svg.Name): $($_.Exception.Message)"
            }
    }
}

Write-Host "Imported $($svgs.Count) icons to: $($targets -join ', ')"
