param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$normal = Join-Path $repo 'unity\CoffeeGame\Builds\Windows'
$backup = Join-Path $repo '.task-local-backup\ORC-20260905-001-WP17-normal-player'
$previous = Join-Path $backup 'previous'
$registry = 'HKCU:\Software\Coffee Tools\CoffeeGAME'
$allowed = @('CoffeeGAME_BurstDebugInformation_DoNotShip','CoffeeGAME_Data','D3D12','MonoBleedingEdge','CoffeeGAME.exe','dstorage.dll','dstoragecore.dll','UnityCrashHandler64.exe','UnityPlayer.dll','WinPixEventRuntime.dll')
function Within([string]$Path,[string]$Root) {
  $absolute = [IO.Path]::GetFullPath($Path)
  $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
  if (-not $absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw "Path escapes root: $absolute" }
  $absolute
}
function Check-Hash([string]$Path,[string]$Expected) {
  if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Expected) { throw "File changed; stop before replacement: $Path" }
}
foreach ($path in @($normal,$backup)) { $null = Within $path $repo }
$manifest = Get-Content -LiteralPath (Join-Path $backup 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.taskId -ne 'ORC-20260905-001' -or $manifest.inputId -ne 'IN13' -or $manifest.normal -ne $normal) { throw 'Unexpected backup identity or location.' }
foreach ($root in @($normal,$backup)) {
  if ((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point: $root" }
  if (@(Get-ChildItem -LiteralPath $root -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw "Reparse point under: $root" }
}
foreach ($file in $manifest.oldFiles) { Check-Hash (Within (Join-Path $previous $file.path) $previous) $file.sha256 }
foreach ($file in $manifest.newFiles) { Check-Hash (Within (Join-Path $normal $file.path) $normal) $file.sha256 }
$top = @($manifest.newFiles | ForEach-Object { $_.path.Split('\')[0] } | Sort-Object -Unique)
foreach ($entry in $top) { if ($entry -notin $allowed) { throw "Unexpected payload entry: $entry" } }
if ($manifest.preferences.Count -ne 3) { throw 'Invalid display-preference backup.' }
foreach ($pref in $manifest.preferences) {
  if ($pref.name -notmatch '^CoffeeGAME\.(PreviousCharacterSelection|CharacterSelectionOverride|CharacterSelectionDefaultApplied)\.v1_' -or $pref.kind -ne 'DWord') { throw 'Unexpected preference in backup.' }
}
if (-not $Restore) {
  Write-Output "Verified $($manifest.oldFiles.Count) old files and $($manifest.newFiles.Count) installed files. No changes made. Use -Restore to restore the old player and its three display preferences."
  exit
}
foreach ($process in Get-Process -ErrorAction SilentlyContinue) {
  if ($process.Path -and $process.Path.StartsWith($normal+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Close CoffeeGAME before restoring.' }
}
$ready = Within (Join-Path $backup 'rollback-ready') $backup
$retained = Within (Join-Path $backup 'rollback-retained-upgrade') $backup
if ((Test-Path -LiteralPath $ready) -or (Test-Path -LiteralPath $retained)) { throw 'Rollback was already started. Inspect the preserved folders before retrying.' }
New-Item -ItemType Directory -Path $ready,$retained | Out-Null
$oldPayload = @($manifest.oldFiles | Where-Object { $_.path.Split('\')[0] -in $top })
foreach ($directory in $manifest.oldDirectories) {
  if ($directory.Split('\')[0] -in $top) { New-Item -ItemType Directory -Path (Within (Join-Path $ready $directory) $ready) -Force | Out-Null }
}
foreach ($file in $oldPayload) {
  $source = Within (Join-Path $previous $file.path) $previous
  $destination = Within (Join-Path $ready $file.path) $ready
  New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
  Copy-Item -LiteralPath $source -Destination $destination
  Check-Hash $destination $file.sha256
}
$key = Get-Item -LiteralPath $registry
@($manifest.preferences | ForEach-Object { [ordered]@{name=$_.name;kind=$key.GetValueKind($_.name).ToString();value=$key.GetValue($_.name)} }) | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'preferences-before-rollback.json') -Encoding UTF8
foreach ($entry in $top) {
  $target = Within (Join-Path $normal $entry) $normal
  $saved = Within (Join-Path $retained $entry) $retained
  $replacement = Within (Join-Path $ready $entry) $ready
  Move-Item -LiteralPath $target -Destination $saved
  if (Test-Path -LiteralPath $replacement) { Move-Item -LiteralPath $replacement -Destination $target }
}
foreach ($file in $oldPayload) { Check-Hash (Within (Join-Path $normal $file.path) $normal) $file.sha256 }
foreach ($pref in $manifest.preferences) { Set-ItemProperty -LiteralPath $registry -Name $pref.name -Value ([int]$pref.value) }
foreach ($pref in $manifest.preferences) {
  if ((Get-Item -LiteralPath $registry).GetValue($pref.name) -ne $pref.value) { throw 'Preference restoration failed.' }
}
[ordered]@{restoredAt=(Get-Date).ToString('o');verifiedPayloadFiles=$oldPayload.Count;profileTouched=$false;upgradeRetained=$retained} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'restored.json') -Encoding UTF8
Write-Output 'Previous player and display preferences restored. Save progress and unrelated settings were preserved.'
