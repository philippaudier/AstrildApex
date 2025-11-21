Install cmgen (Filament) and configure the editor to auto-generate PMREM

Usage (from repository root):

PowerShell (recommended):

```powershell
# Run with default tag v1.67.0 and destination Tools\cmgen
.\Tools\install-cmgen.ps1

# Or specify a different release tag and destination
.\Tools\install-cmgen.ps1 -Tag v1.67.0 -Destination "C:\tools\filament" -Force
```

What it does:
- Queries GitHub Releases for `google/filament` by tag (default `v1.67.0`).
- Picks an asset likely containing the `cmgen` tool (prefers assets with "cmgen" in the name, else a windows archive).
- Downloads and extracts the asset to a temporary folder.
- Searches for `cmgen.exe` inside the extracted files and copies it to the `Destination` folder.
- Updates `ProjectSettings/EditorSettings.json` to set `ExternalTools.CmgenPath` and enable `ExternalTools.AutoGeneratePMREMOnImport`.

Notes & troubleshooting:
- The GitHub API requires a `User-Agent` header; the script sets one.
- If extraction with `Expand-Archive` fails, the script will try to use installed `7z` if available.
- If the script cannot find a suitable asset automatically, it will print the list of available assets for manual download.
- The script attempts to locate `EditorSettings.json` relative to the script path or current working directory; if it cannot find it, it will still copy `cmgen.exe` and print the destination path for manual configuration.

Security:
- The script downloads binaries from GitHub releases; review the asset URL and binaries before running in sensitive environments.

If you want I can run the script now (it will download from GitHub). Say "run it" to allow me to execute it here.