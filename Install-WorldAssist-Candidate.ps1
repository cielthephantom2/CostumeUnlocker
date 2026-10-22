param([string]$GameRoot = 'D:\SteamLibrary\steamapps\common\Last Epoch')
$ErrorActionPreference = 'Stop'
$packageRoot = $PSScriptRoot
$gamePath = (Resolve-Path -LiteralPath $GameRoot).Path
if (!(Test-Path -LiteralPath (Join-Path $gamePath 'GameAssembly.dll'))) { throw 'GameRoot must be the Last Epoch game directory.' }
if (!(Test-Path -LiteralPath (Join-Path $gamePath 'MelonLoader'))) { throw 'MelonLoader is required.' }
function Assert-GameClosed {
    if (Get-Process -Name 'Last Epoch' -ErrorAction SilentlyContinue) { throw 'Close Last Epoch before installing this candidate.' }
}
Assert-GameClosed
$manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'latest.json') -Raw | ConvertFrom-Json
if ($manifest.product -ne 'LastEpochLAN' -or $manifest.version -ne '1.1.0-dev.55' -or $manifest.protocol -ne 44) { throw 'Unexpected candidate manifest.' }
$requiredPaths = @('Mods/LastEpochLAN.dll', 'UserLibs/LastEpochLAN.Core.dll')
if (@($manifest.files).Count -ne 2) { throw 'Expected exactly two paired DLLs.' }
foreach ($relative in $requiredPaths) {
    $entry = @($manifest.files | Where-Object { $_.path -ceq $relative })
    if ($entry.Count -ne 1) { throw "Missing or duplicated manifest entry: $relative" }
    $source = Join-Path $packageRoot $relative
    if ((Get-Item -LiteralPath $source).Length -ne $entry[0].size -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry[0].sha256) { throw "Candidate checksum mismatch: $relative" }
}
$backupRoot = Join-Path $gamePath ('UserData\LastEpochLAN\backups\before-world-assist-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
foreach ($relative in $requiredPaths) {
    $destination = Join-Path $gamePath $relative
    if (Test-Path -LiteralPath $destination) {
        $backup = Join-Path $backupRoot $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination $backup
    }
}
Assert-GameClosed
foreach ($relative in $requiredPaths) {
    $destination = Join-Path $gamePath $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $packageRoot $relative) -Destination $destination -Force
}
foreach ($entry in $manifest.files) {
    if ((Get-FileHash -LiteralPath (Join-Path $gamePath $entry.path) -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Installed checksum mismatch: $($entry.path). Backup: $backupRoot" }
}
Write-Output "Installed World Assist dev.55 / protocol44. Backup: $backupRoot"
