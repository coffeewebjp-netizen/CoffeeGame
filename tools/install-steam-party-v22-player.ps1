param([ValidateSet('Install','Verify','Restore')][string]$Mode = 'Verify')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$normal = [IO.Path]::GetFullPath((Join-Path $repo 'unity\CoffeeGame\Builds\Windows'))
$source = [IO.Path]::GetFullPath((Join-Path $repo 'unity\CoffeeGame\Builds\Windows-SteamPartyV22-Ready'))
$backup = [IO.Path]::GetFullPath((Join-Path $repo '.task-local-backup\ORC-20260911-002-normal-player'))
$previous = Join-Path $backup 'previous'
$prepared = Join-Path $backup 'prepared'
$retained = Join-Path $backup 'retained-v22'
$manifestPath = Join-Path $backup 'manifest.json'
$payload = @(
  'CoffeeGAME_BurstDebugInformation_DoNotShip','CoffeeGAME_Data','D3D12','MonoBleedingEdge',
  'CoffeeGAME.exe','dstorage.dll','dstoragecore.dll','UnityCrashHandler64.exe','UnityPlayer.dll','WinPixEventRuntime.dll'
)

function Assert-Within([string]$Path,[string]$Root) {
  $absolute = [IO.Path]::GetFullPath($Path)
  $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
  if ($absolute -ne [IO.Path]::GetFullPath($Root) -and -not $absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {
    throw "Path escapes expected root: $absolute"
  }
  return $absolute
}
function Assert-SafeTree([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) { throw "Missing directory: $Path" }
  if ((Get-Item -LiteralPath $Path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point: $Path" }
  if (@(Get-ChildItem -LiteralPath $Path -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) {
    throw "Reparse point under: $Path"
  }
}
function Assert-GameClosed {
  foreach ($process in Get-Process -ErrorAction SilentlyContinue) {
    if ($process.Path -and $process.Path.StartsWith($normal + '\',[StringComparison]::OrdinalIgnoreCase)) {
      throw 'CoffeeGAME is running from the Steam path. Close it before install or restore.'
    }
  }
}
function File-Manifest([string]$Root) {
  if (-not (Test-Path -LiteralPath $Root)) { return @() }
  return @(Get-ChildItem -LiteralPath $Root -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{
      path = $_.FullName.Substring($Root.Length).TrimStart('\')
      bytes = $_.Length
      sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
  })
}
function Assert-Files([string]$Root,$Expected) {
  foreach ($file in $Expected) {
    $path = Assert-Within (Join-Path $Root $file.path) $Root
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing payload file: $path" }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) {
      throw "Payload hash changed: $path"
    }
  }
}

foreach ($path in @($normal,$source,$backup,$previous,$prepared,$retained)) { $null = Assert-Within $path $repo }

if ($Mode -eq 'Install') {
  if (Test-Path -LiteralPath $backup) { throw "Backup already exists: $backup" }
  Assert-SafeTree $normal
  Assert-SafeTree $source
  Assert-GameClosed
  $top = @(Get-ChildItem -LiteralPath $source -Force | Select-Object -ExpandProperty Name)
  foreach ($entry in $top) { if ($entry -notin $payload) { throw "Unexpected candidate entry: $entry" } }
  foreach ($required in @('CoffeeGAME.exe','CoffeeGAME_Data','UnityPlayer.dll')) {
    if ($required -notin $top) { throw "Candidate is incomplete: $required" }
  }
  New-Item -ItemType Directory -Path $backup,$previous,$prepared | Out-Null
  foreach ($entry in $payload) {
    $old = Join-Path $normal $entry
    if (Test-Path -LiteralPath $old) { Copy-Item -LiteralPath $old -Destination $previous -Recurse }
    $new = Join-Path $source $entry
    if (Test-Path -LiteralPath $new) { Copy-Item -LiteralPath $new -Destination $prepared -Recurse }
  }
  $oldFiles = File-Manifest $previous
  $newFiles = File-Manifest $prepared
  Assert-Files $previous $oldFiles
  Assert-Files $prepared $newFiles
  try {
    foreach ($entry in $payload) {
      $target = Join-Path $normal $entry
      $old = Join-Path $previous $entry
      $new = Join-Path $prepared $entry
      if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
      if (Test-Path -LiteralPath $new) { Copy-Item -LiteralPath $new -Destination $normal -Recurse }
      elseif (Test-Path -LiteralPath $old) { throw "Prepared payload unexpectedly lacks $entry" }
    }
  } catch {
    foreach ($entry in $payload) {
      $target = Join-Path $normal $entry
      $old = Join-Path $previous $entry
      if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
      if (Test-Path -LiteralPath $old) { Copy-Item -LiteralPath $old -Destination $normal -Recurse }
    }
    throw
  }
  Assert-Files $normal $newFiles
  [ordered]@{taskId='ORC-20260911-002';installedAt=(Get-Date).ToString('o');normal=$normal;source=$source;oldFiles=$oldFiles;newFiles=$newFiles} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
  Write-Output "Installed and verified $($newFiles.Count) Steam-player files. Previous payload: $previous"
  exit
}

if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Install manifest not found: $manifestPath" }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.taskId -ne 'ORC-20260911-002' -or $manifest.normal -ne $normal -or $manifest.source -ne $source) { throw 'Unexpected manifest identity.' }
Assert-SafeTree $normal
Assert-Files $normal $manifest.newFiles
if ($Mode -eq 'Verify') {
  Assert-Files $previous $manifest.oldFiles
  Write-Output "Verified installed V22 $($manifest.newFiles.Count) files and previous $($manifest.oldFiles.Count) files."
  exit
}

Assert-GameClosed
if (Test-Path -LiteralPath $retained) { throw "Restore already started: $retained" }
New-Item -ItemType Directory -Path $retained | Out-Null
foreach ($entry in $payload) {
  $target = Join-Path $normal $entry
  $old = Join-Path $previous $entry
  if (Test-Path -LiteralPath $target) { Move-Item -LiteralPath $target -Destination $retained }
  if (Test-Path -LiteralPath $old) { Copy-Item -LiteralPath $old -Destination $normal -Recurse }
}
Assert-Files $normal $manifest.oldFiles
[ordered]@{restoredAt=(Get-Date).ToString('o');retainedV22=$retained;profileTouched=$false} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'restored.json') -Encoding UTF8
Write-Output 'Restored the pre-V22 Steam player. Save data and input/display settings were not changed.'
