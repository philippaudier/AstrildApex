$file = "C:\Users\Philippe\Documents\Programming\AstrildApex\Editor\Inspector\AudioSourceInspector.cs"
$content = Get-Content $file
$keep1 = $content[0..341]
$keep2 = $content[592..$content.Length]
$result = $keep1 + $keep2
$result | Set-Content $file
Write-Host "Removed lines 342-591 from AudioSourceInspector.cs"
