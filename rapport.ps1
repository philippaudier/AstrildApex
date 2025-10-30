# Ce script PowerShell génère un rapport complet du projet pour analyse IA.
# Il liste la structure, puis extrait le contenu des fichiers importants.

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$rapport = Join-Path $root 'rapport.txt'

# Efface le rapport précédent
if (Test-Path $rapport) { Remove-Item $rapport }

# 1. Structure du projet
"=== STRUCTURE DU PROJET ===`n" | Out-File -FilePath $rapport -Encoding UTF8
Get-ChildItem -Recurse | Sort-Object FullName | Format-Table FullName, Length -AutoSize | Out-String | Out-File -FilePath $rapport -Append -Encoding UTF8

# 2. Contenu des fichiers importants
"`n=== CONTENU DES FICHIERS (.cs, .csproj, .sln, .md, .ini) ===`n" | Out-File -FilePath $rapport -Append -Encoding UTF8

$extensions = @('*.cs','*.csproj','*.sln','*.md','*.ini')
foreach ($ext in $extensions) {
    Get-ChildItem -Recurse -Filter $ext | ForEach-Object {
        "`n--- $($_.FullName) ---`n" | Out-File -FilePath $rapport -Append -Encoding UTF8
        Get-Content $_.FullName | Out-File -FilePath $rapport -Append -Encoding UTF8
    }
}

# 3. Résumé des tailles de fichiers
"`n=== RÉSUMÉ DES FICHIERS PAR TAILLE ===`n" | Out-File -FilePath $rapport -Append -Encoding UTF8
Get-ChildItem -Recurse | Sort-Object Length -Descending | Select-Object FullName, Length | Format-Table -AutoSize | Out-String | Out-File -FilePath $rapport -Append -Encoding UTF8

# 4. Fin du rapport
"`n=== FIN DU RAPPORT ===" | Out-File -FilePath $rapport -Append -Encoding UTF8

Write-Host "Rapport généré dans $rapport" -ForegroundColor Green
