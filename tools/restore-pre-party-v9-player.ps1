param(
  [switch]$Restore,
  [string]$RepositoryRoot,
  [string]$PreferenceFixturePath,
  [string]$LocalLowRoot,
  [switch]$SkipProcessCheck
)

$ErrorActionPreference = 'Stop'
$scriptRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repo = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
  $scriptRepo
} else {
  [IO.Path]::GetFullPath($RepositoryRoot)
}
$normal = Join-Path $repo 'unity\CoffeeGame\Builds\Windows'
$source = Join-Path $repo 'unity\CoffeeGame\Builds\Windows-PartyV9'
$backup = Join-Path $repo '.task-local-backup\ORC-20260906-002-WP05-normal-player'
$previous = Join-Path $backup 'previous'
$registry = 'HKCU:\Software\Coffee Tools\CoffeeGAME'
$allowed = @(
  'CoffeeGAME_BurstDebugInformation_DoNotShip',
  'CoffeeGAME_Data',
  'D3D12',
  'MonoBleedingEdge',
  'CoffeeGAME.exe',
  'dstorage.dll',
  'dstoragecore.dll',
  'UnityCrashHandler64.exe',
  'UnityPlayer.dll',
  'WinPixEventRuntime.dll'
)

function Within([string]$Path, [string]$Root) {
  $absolute = [IO.Path]::GetFullPath($Path)
  $rootAbsolute = [IO.Path]::GetFullPath($Root).TrimEnd('\')
  $prefix = $rootAbsolute + '\'
  if (-not $absolute.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Path escapes root: $absolute"
  }
  $absolute
}

function Same-Path([string]$Left, [string]$Right) {
  [IO.Path]::GetFullPath($Left).TrimEnd('\').Equals(
    [IO.Path]::GetFullPath($Right).TrimEnd('\'),
    [StringComparison]::OrdinalIgnoreCase)
}

function At-Or-Within([string]$Path, [string]$Root) {
  $absolute = [IO.Path]::GetFullPath($Path)
  $rootAbsolute = [IO.Path]::GetFullPath($Root).TrimEnd('\')
  $absolute.Equals($rootAbsolute, [StringComparison]::OrdinalIgnoreCase) -or
    $absolute.StartsWith($rootAbsolute + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Check-Hash([string]$Path, [string]$Expected) {
  if ([string]::IsNullOrWhiteSpace($Expected)) {
    throw "Missing expected hash: $Path"
  }
  $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($actual -ne $Expected.ToLowerInvariant()) {
    throw "File changed; stop before replacement: $Path"
  }
}

function Assert-NoReparse([string]$Path, [switch]$Recursive) {
  $item = Get-Item -LiteralPath $Path
  if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw "Reparse point: $Path"
  }
  if ($Recursive -and @(Get-ChildItem -LiteralPath $Path -Force -Recurse |
      Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) {
    throw "Reparse point under: $Path"
  }
}

function Decode-PreferenceText([object]$Value, [string]$Name) {
  if ($null -eq $Value) { return '' }
  if ($Value -is [string]) { return $Value.TrimEnd([char]0) }
  if ($Value -is [byte[]]) {
    return [Text.Encoding]::UTF8.GetString($Value).TrimEnd([char]0)
  }
  if ($Value.PSObject.Properties.Name -contains 'bytes') {
    $bytes = [byte[]]@($Value.bytes | ForEach-Object { [byte]$_ })
    return [Text.Encoding]::UTF8.GetString($bytes).TrimEnd([char]0)
  }
  throw "Unsupported registry value format for $Name."
}

function Read-UnitySavePreferences {
  if (-not [string]::IsNullOrWhiteSpace($PreferenceFixturePath)) {
    $fixture = Get-Content -LiteralPath $PreferenceFixturePath -Raw | ConvertFrom-Json
    return [ordered]@{
      kind = Decode-PreferenceText $fixture.kind 'fixture kind'
      folder = Decode-PreferenceText $fixture.folder 'fixture folder'
    }
  }

  if (-not (Test-Path -LiteralPath $registry)) {
    return [ordered]@{kind='';folder=''}
  }
  $key = Get-Item -LiteralPath $registry
  $kindNames = @($key.GetValueNames() | Where-Object { $_ -match '^CoffeeGame\.Save\.Kind\.v1_' })
  $folderNames = @($key.GetValueNames() | Where-Object { $_ -match '^CoffeeGame\.Save\.Folder\.v1_' })
  if ($kindNames.Count -gt 1 -or $folderNames.Count -gt 1) {
    throw 'Ambiguous CoffeeGAME save preferences in HKCU.'
  }
  # Assign the method result directly: returning byte[] through an if pipeline
  # unrolls it into object[], losing the registry's binary value type.
  $kindValue = $null
  $folderValue = $null
  if ($kindNames.Count -eq 1) { $kindValue = $key.GetValue($kindNames[0], $null) }
  if ($folderNames.Count -eq 1) { $folderValue = $key.GetValue($folderNames[0], $null) }
  [ordered]@{
    kind = Decode-PreferenceText $kindValue 'CoffeeGame.Save.Kind.v1'
    folder = Decode-PreferenceText $folderValue 'CoffeeGame.Save.Folder.v1'
  }
}

function Resolve-CurrentProfilePath {
  $preferences = Read-UnitySavePreferences
  if ($preferences.kind.Equals('folder', [StringComparison]::OrdinalIgnoreCase) -and
      -not [string]::IsNullOrWhiteSpace($preferences.folder) -and
      (Test-Path -LiteralPath $preferences.folder -PathType Container)) {
    Assert-NoReparse $preferences.folder
    return [IO.Path]::GetFullPath((Join-Path $preferences.folder 'CoffeeGAME-player-profile.json'))
  }

  $base = $LocalLowRoot
  if ([string]::IsNullOrWhiteSpace($base)) {
    $local = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $base = Join-Path (Split-Path -Parent $local) 'LocalLow'
  }
  [IO.Path]::GetFullPath((Join-Path $base 'Coffee Tools\CoffeeGAME\CoffeeGAME\player-profile.json'))
}

function Require-ProfileField([object]$Profile, [string]$Name) {
  if ($Profile.PSObject.Properties.Name -notcontains $Name) {
    throw "The v3 profile is missing the legacy field '$Name'."
  }
}

function Read-CompatibleProfile([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    return [ordered]@{version=0;json=$null;projection=$null;sha256=$null}
  }
  Assert-NoReparse $Path
  $sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
  $json = Get-Content -LiteralPath $Path -Raw
  try { $profile = $json | ConvertFrom-Json } catch { throw "Invalid player profile JSON: $Path" }
  if ($null -eq $profile -or $profile.PSObject.Properties.Name -notcontains 'version') {
    throw "Player profile has no version: $Path"
  }
  $version = [int]$profile.version
  if ($version -lt 1 -or $version -gt 3) {
    throw "Unsupported player profile version $version; no rollback changes were made."
  }
  if ($version -lt 3) {
    return [ordered]@{version=$version;json=$json;projection=$json;sha256=$sha256}
  }

  $legacyFields = @(
    'level','experience','gold','slimeJelly','talentPoints','status',
    'claimedRewardIds','rivalAffinities','recruitedRivalIds'
  )
  foreach ($field in $legacyFields) { Require-ProfileField $profile $field }
  $projection = [ordered]@{
    version = 2
    level = $profile.level
    experience = $profile.experience
    gold = $profile.gold
    slimeJelly = $profile.slimeJelly
    talentPoints = $profile.talentPoints
    status = $profile.status
    claimedRewardIds = $profile.claimedRewardIds
    rivalAffinities = $profile.rivalAffinities
    recruitedRivalIds = $profile.recruitedRivalIds
  } | ConvertTo-Json -Depth 64
  [ordered]@{version=$version;json=$json;projection=$projection;sha256=$sha256}
}

function Write-Utf8NoBom([string]$Path, [string]$Text) {
  [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding($false)))
}

if ((Same-Path $repo $scriptRepo) -and
    (-not [string]::IsNullOrWhiteSpace($PreferenceFixturePath) -or
     -not [string]::IsNullOrWhiteSpace($LocalLowRoot))) {
  throw 'Profile-location fixture overrides are allowed only with an isolated fixture RepositoryRoot.'
}
foreach ($path in @($normal,$source,$backup)) { $null = Within $path $repo }
if (-not (Test-Path -LiteralPath $normal -PathType Container)) { throw "Normal player is missing: $normal" }
if (-not (Test-Path -LiteralPath $backup -PathType Container)) { throw "Rollback backup is missing: $backup" }
$manifestPath = Join-Path $backup 'manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.taskId -ne 'ORC-20260906-002' -or $manifest.inputId -ne 'IN07' -or
    -not (Same-Path $manifest.normal $normal) -or -not (Same-Path $manifest.source $source)) {
  throw 'Unexpected backup identity, player location, or Windows-PartyV9 source.'
}

foreach ($root in @($normal,$backup)) { Assert-NoReparse $root -Recursive }
foreach ($file in @($manifest.oldFiles)) {
  Check-Hash (Within (Join-Path $previous $file.path) $previous) $file.sha256
}
foreach ($file in @($manifest.newFiles)) {
  Check-Hash (Within (Join-Path $normal $file.path) $normal) $file.sha256
}
$top = @($manifest.newFiles | ForEach-Object { $_.path.Split('\')[0] } | Sort-Object -Unique)
if ($top.Count -eq 0) { throw 'The backup manifest has no installed payload.' }
foreach ($entry in $top) {
  if ($entry -notin $allowed) { throw "Unexpected payload entry: $entry" }
  if (-not (Test-Path -LiteralPath (Within (Join-Path $normal $entry) $normal))) {
    throw "Installed payload entry is missing: $entry"
  }
}
foreach ($pref in @($manifest.preferences)) {
  if ($null -ne $pref -and
      ($pref.name -notmatch '^CoffeeGAME\.(PreviousCharacterSelection|CharacterSelectionOverride|CharacterSelectionDefaultApplied)\.v1_' -or
       $pref.kind -ne 'DWord')) {
    throw 'Unexpected preference in backup manifest.'
  }
}

$profilePath = Resolve-CurrentProfilePath
$profilePath = [IO.Path]::GetFullPath($profilePath)
if ((At-Or-Within $profilePath $normal) -or (At-Or-Within $profilePath $backup)) {
  throw "Profile path overlaps the player or rollback backup: $profilePath"
}
$profileParent = Split-Path -Parent $profilePath
if (Test-Path -LiteralPath $profileParent) { Assert-NoReparse $profileParent }
$profileState = Read-CompatibleProfile $profilePath
$profileBackupPath = $profilePath + '.bak'
if (Test-Path -LiteralPath $profileBackupPath) { Assert-NoReparse $profileBackupPath }

if (-not $Restore) {
  $profileDescription = if ($profileState.version -eq 0) { 'no current profile' } else { "profile v$($profileState.version) ready for v2 rollback" }
  Write-Output "Verified $($manifest.oldFiles.Count) old files and $($manifest.newFiles.Count) installed files; $profileDescription at $profilePath. No changes made. Use -Restore to restore the pre-party-v9 player."
  exit
}

if ($SkipProcessCheck -and (Same-Path $repo $scriptRepo)) {
  throw 'SkipProcessCheck is allowed only with an isolated fixture RepositoryRoot.'
}
if (-not $SkipProcessCheck) {
  foreach ($process in Get-Process -ErrorAction SilentlyContinue) {
    try {
      if ($process.Path -and $process.Path.StartsWith($normal+'\',[StringComparison]::OrdinalIgnoreCase)) {
        throw 'Close CoffeeGAME before restoring.'
      }
    } catch [System.ComponentModel.Win32Exception] {
      # Some system process paths are unreadable; they cannot be this user player path.
    }
  }
}

$ready = Within (Join-Path $backup 'rollback-ready') $backup
$retained = Within (Join-Path $backup 'rollback-retained-party-v9') $backup
if ((Test-Path -LiteralPath $ready) -or (Test-Path -LiteralPath $retained)) {
  throw 'Rollback was already started. Inspect the preserved folders before retrying.'
}
New-Item -ItemType Directory -Path $ready,$retained | Out-Null

$oldPayload = @($manifest.oldFiles | Where-Object { $_.path.Split('\')[0] -in $top })
foreach ($directory in @($manifest.oldDirectories)) {
  if ($null -ne $directory -and $directory.Split('\')[0] -in $top) {
    New-Item -ItemType Directory -Path (Within (Join-Path $ready $directory) $ready) -Force | Out-Null
  }
}
foreach ($file in $oldPayload) {
  $oldSource = Within (Join-Path $previous $file.path) $previous
  $destination = Within (Join-Path $ready $file.path) $ready
  New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
  Copy-Item -LiteralPath $oldSource -Destination $destination
  Check-Hash $destination $file.sha256
}

$stagedProfile = $null
if ($profileState.version -gt 0) {
  $stagedProfile = Within (Join-Path $ready 'profile-v2-projection.json') $ready
  Write-Utf8NoBom $stagedProfile $profileState.projection
  $stagedData = Get-Content -LiteralPath $stagedProfile -Raw | ConvertFrom-Json
  $expectedVersion = if ($profileState.version -eq 3) { 2 } else { $profileState.version }
  if ([int]$stagedData.version -ne $expectedVersion) { throw 'Staged profile version verification failed.' }
}

$retainedProfileDirectory = Within (Join-Path $retained 'profile') $retained
New-Item -ItemType Directory -Path $retainedProfileDirectory | Out-Null
$retainedProfile = $null
if ($profileState.version -gt 0) {
  $retainedProfile = Within (Join-Path $retainedProfileDirectory 'player-profile-before-rollback.json') $retainedProfileDirectory
  Move-Item -LiteralPath $profilePath -Destination $retainedProfile
  Check-Hash $retainedProfile $profileState.sha256
}
if (Test-Path -LiteralPath $profileBackupPath) {
  $profileBackupHash = (Get-FileHash -LiteralPath $profileBackupPath -Algorithm SHA256).Hash.ToLowerInvariant()
  $retainedBackup = Within (Join-Path $retainedProfileDirectory 'player-profile-before-rollback.json.bak') $retainedProfileDirectory
  Move-Item -LiteralPath $profileBackupPath -Destination $retainedBackup
  Check-Hash $retainedBackup $profileBackupHash
}

foreach ($entry in $top) {
  $target = Within (Join-Path $normal $entry) $normal
  $saved = Within (Join-Path $retained $entry) $retained
  $replacement = Within (Join-Path $ready $entry) $ready
  Move-Item -LiteralPath $target -Destination $saved
  if (Test-Path -LiteralPath $replacement) { Move-Item -LiteralPath $replacement -Destination $target }
}
foreach ($file in $oldPayload) {
  Check-Hash (Within (Join-Path $normal $file.path) $normal) $file.sha256
}

if ($null -ne $stagedProfile) {
  if (-not (Test-Path -LiteralPath $profileParent)) { New-Item -ItemType Directory -Path $profileParent | Out-Null }
  Move-Item -LiteralPath $stagedProfile -Destination $profilePath
}
$record = [ordered]@{
  restoredAt = (Get-Date).ToString('o')
  verifiedPayloadFiles = $oldPayload.Count
  profilePath = $profilePath
  sourceProfileVersion = $profileState.version
  rollbackProfileVersion = if ($profileState.version -eq 3) { 2 } else { $profileState.version }
  retainedProfile = $retainedProfile
  upgradeRetained = $retained
}
$record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'restored.json') -Encoding UTF8
Write-Output 'Pre-party-v9 player restored. Shared level, EXP, Gold, materials, talent points, rival state, and reward IDs were preserved. Party resources remain in the retained v3 profile.'
