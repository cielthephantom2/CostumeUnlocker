@echo off
setlocal
set "CU_UPDATE_SELF=%~f0"
set "CU_GAME_PATH=%~1"
if not defined CU_GAME_PATH set "CU_GAME_PATH=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$t=[IO.File]::ReadAllText($env:CU_UPDATE_SELF);$m='### POWERSHELL_PAYLOAD ###'; & ([scriptblock]::Create($t.Substring($t.LastIndexOf($m)+$m.Length))) -GamePath $env:CU_GAME_PATH"
set "CU_RESULT=%ERRORLEVEL%"
echo.
pause
exit /b %CU_RESULT%
### POWERSHELL_PAYLOAD ###
param([string]$GamePath,[string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
function Get-Sha256([string]$Path) {
    $stream=[IO.File]::OpenRead($Path);$sha=[Security.Cryptography.SHA256]::Create()
    try {return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}
    finally {$stream.Dispose();$sha.Dispose()}
}
$repo='https://raw.githubusercontent.com/cielthephantom2/CostumeUnlocker/main/'
if(!$GamePath -or !(Test-Path -LiteralPath (Join-Path $GamePath 'Last Epoch.exe'))) {
    $GamePath=(Read-Host 'Enter the Last Epoch game folder').Trim('"')
}
$gameRoot=(Resolve-Path -LiteralPath $GamePath).Path
if(!(Test-Path -LiteralPath (Join-Path $gameRoot 'Last Epoch.exe'))) {throw 'This folder does not contain Last Epoch.exe.'}
if(!(Test-Path -LiteralPath (Join-Path $gameRoot 'MelonLoader\net6\MelonLoader.dll'))) {throw 'Install MelonLoader 0.7.3 x64 first.'}
Write-Host 'Checking the latest Costume Unlocker release...'
if($ReleaseDirectory) {$manifest=Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'latest.json') -Raw | ConvertFrom-Json}
else {$manifest=Invoke-RestMethod -Uri ($repo+'latest.json?cache='+[DateTime]::UtcNow.Ticks) -TimeoutSec 25}
if($manifest.schema -ne 1 -or $manifest.version -notmatch '^\d+\.\d+\.\d+$' -or $manifest.sha256 -notmatch '^[A-Fa-f0-9]{64}$') {throw 'Invalid release manifest.'}
$fileName='CostumeUnlocker-'+$manifest.version+'.dll'
if($manifest.path -cne ('Mods/'+$fileName) -or $manifest.size -le 0 -or $manifest.size -gt 10485760) {throw 'Invalid release filename or size.'}
$modsRoot=Join-Path $gameRoot 'Mods'
$destination=Join-Path $modsRoot $fileName
$pattern='^CostumeUnlocker(?:-\d+\.\d+\.\d+(?:\.\d+)?)?\.dll$'
$active=@(Get-ChildItem -LiteralPath $modsRoot -Filter 'CostumeUnlocker*.dll' -File -ErrorAction SilentlyContinue | Where-Object Name -match $pattern)
if($active.Count -eq 1 -and $active[0].Name -ceq $fileName -and (Get-Sha256 $active[0].FullName) -eq $manifest.sha256) {
    Write-Host ('Already up to date: '+$manifest.version+' (SHA-256 verified).') -ForegroundColor Green
    return
}
foreach($game in (Get-Process 'Last Epoch' -ErrorAction SilentlyContinue)) {
    if(!$game.Path -or $game.Path -eq (Join-Path $gameRoot 'Last Epoch.exe')) {throw 'Exit the game through its menu, then run this updater again.'}
}
$work=Join-Path ([IO.Path]::GetTempPath()) ('CostumeUnlocker-update-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$download=Join-Path $work 'payload.download'
$staged=Join-Path $work $fileName
$moved=@();$writing=$false
try {
    if($ReleaseDirectory) {Copy-Item -LiteralPath (Join-Path $ReleaseDirectory $manifest.path) -Destination $download}
    else {Invoke-WebRequest -UseBasicParsing -Uri ($repo+$manifest.path+'?sha='+$manifest.sha256) -OutFile $download -TimeoutSec 90}
    if((Get-Item -LiteralPath $download).Length -ne $manifest.size -or (Get-Sha256 $download) -ne $manifest.sha256) {throw 'Downloaded DLL failed size/SHA-256 verification. Existing mod was not changed.'}
    Move-Item -LiteralPath $download -Destination $staged
    $identity=[Reflection.AssemblyName]::GetAssemblyName($staged)
    if($identity.Name -ne 'CostumeUnlocker' -or $identity.Version.ToString(3) -ne $manifest.version) {throw 'DLL identity/version does not match the manifest.'}
    # Check again after the download, before changing any game file.
    foreach($game in (Get-Process 'Last Epoch' -ErrorAction SilentlyContinue)) {
        if(!$game.Path -or $game.Path -eq (Join-Path $gameRoot 'Last Epoch.exe')) {throw 'The game started during the download. Close it and run the updater again.'}
    }
    New-Item -ItemType Directory -Path $modsRoot -Force | Out-Null
    $backupRoot=Join-Path $gameRoot 'UserData\CostumeUnlocker\backups'
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    $active=@(Get-ChildItem -LiteralPath $modsRoot -Filter 'CostumeUnlocker*.dll' -File | Where-Object Name -match $pattern)
    foreach($old in $active) {
        if(([Reflection.AssemblyName]::GetAssemblyName($old.FullName)).Name -ne 'CostumeUnlocker') {throw ('Unexpected assembly: '+$old.Name)}
    }
    foreach($old in $active) {
        $hash=Get-Sha256 $old.FullName
        $backup=Join-Path $backupRoot ($old.Name+'.'+$hash+'.bak')
        Move-Item -LiteralPath $old.FullName -Destination $backup -Force
        $moved+=@{Original=$old.FullName;Backup=$backup}
    }
    $writing=$true
    Copy-Item -LiteralPath $staged -Destination $destination
    if((Get-Sha256 $destination) -ne $manifest.sha256) {throw 'Installed hash verification failed.'}
    Write-Host ('Updated to '+$manifest.version+' (SHA-256 verified).') -ForegroundColor Green
    Write-Host ('Installed: '+$destination)
    Write-Host 'Your costumes, room settings and host certificate were kept.'
} catch {
    if($writing -and (Test-Path -LiteralPath $destination)) {Remove-Item -LiteralPath $destination -Force}
    foreach($entry in $moved) {Copy-Item -LiteralPath $entry.Backup -Destination $entry.Original -Force}
    throw
} finally {
    foreach($temporary in @($download,$staged)) {if(Test-Path -LiteralPath $temporary) {Remove-Item -LiteralPath $temporary -Force}}
    if(Test-Path -LiteralPath $work) {[IO.Directory]::Delete($work,$false)}
}

