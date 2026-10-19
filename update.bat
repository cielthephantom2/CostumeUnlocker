@echo off
setlocal
set "LE_UPDATE_SELF=%~f0"
set "LE_GAME_PATH=%~1"
if not defined LE_GAME_PATH set "LE_GAME_PATH=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$s=[IO.File]::ReadAllText($env:LE_UPDATE_SELF);$m='### POWERSHELL ###'; & ([scriptblock]::Create($s.Substring($s.LastIndexOf($m)+$m.Length))) -GamePath $env:LE_GAME_PATH"
if errorlevel 1 (
  echo Update failed. Your game was not started.
  pause
  exit /b 1
)
exit /b 0
### POWERSHELL ###
param([string]$GamePath,[string]$ReleaseDirectory,[switch]$NoLaunch)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
if(!$GamePath -or !(Test-Path -LiteralPath (Join-Path $GamePath 'Last Epoch.exe'))) {
    $GamePath=(Read-Host 'Enter the Last Epoch game folder').Trim('"')
}
$root=(Resolve-Path -LiteralPath $GamePath).Path
$exe=Join-Path $root 'Last Epoch.exe'
if(!(Test-Path -LiteralPath $exe)) {throw 'Last Epoch.exe not found.'}
if(!(Test-Path -LiteralPath (Join-Path $root 'MelonLoader/net6/MelonLoader.dll'))) {throw 'Install MelonLoader 0.7.3 x64 first.'}
function Assert-Closed {
    foreach($running in @(Get-Process 'Last Epoch' -ErrorAction SilentlyContinue)) {
        if(!$running.Path -or $running.Path -eq $exe) {throw 'Exit the game through its menu before updating. This updater never closes your game.'}
    }
}
Assert-Closed
Write-Host 'Checking offline Co-op updates...'
function Read-ReleaseCommit([string]$advertisement) {
    # Git's public HTTPS ref advertisement is independent of REST API quotas.
    # Parse packet boundaries; never accept a SHA scraped from an error page.
    if(!$advertisement -or $advertisement.Length -gt 1048576) {throw 'Invalid Git release response.'}
    $offset=0; $service=$false; $commit=$null
    while($offset -lt $advertisement.Length) {
        if($offset+4 -gt $advertisement.Length) {throw 'Truncated Git release response.'}
        $prefix=$advertisement.Substring($offset,4)
        if($prefix -notmatch '^[0-9a-fA-F]{4}$') {throw 'Invalid Git packet header.'}
        $length=[Convert]::ToInt32($prefix,16); $offset+=4
        if($length -eq 0) {continue}
        if($length -lt 4 -or $offset+$length-4 -gt $advertisement.Length) {throw 'Invalid Git packet length.'}
        $line=$advertisement.Substring($offset,$length-4); $offset+=$length-4
        if(!$service) {
            if($line -cne "# service=git-upload-pack`n") {throw 'Unexpected Git service.'}
            $service=$true; continue
        }
        if($line -cmatch '^([a-f0-9]{40}) refs/heads/main(?:\x00[^\n]*)?\n$') {
            if($commit) {throw 'Duplicate release branch.'}
            $commit=$Matches[1]
        }
    }
    if(!$commit) {throw 'Release branch main was not advertised.'}
    return $commit
}
if($ReleaseDirectory) {$manifest=Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'latest.json') -Raw | ConvertFrom-Json}
else {
    $refs=Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/cielthephantom2/CostumeUnlocker.git/info/refs?service=git-upload-pack' -Headers @{'User-Agent'='LastEpochLAN-Updater';'Cache-Control'='no-cache'} -TimeoutSec 25
    $advertisement=if($refs.Content -is [byte[]]) {[Text.Encoding]::ASCII.GetString($refs.Content)} else {[string]$refs.Content}
    $commit=Read-ReleaseCommit $advertisement
    $base='https://raw.githubusercontent.com/cielthephantom2/CostumeUnlocker/'+$commit+'/'
    $manifest=Invoke-RestMethod -Uri ($base+'latest.json') -TimeoutSec 25
}
$paths=@('Mods/LastEpochLAN.dll','UserLibs/LastEpochLAN.Core.dll')
if($manifest.schema -ne 2 -or $manifest.product -cne 'LastEpochLAN' -or $manifest.version -notmatch '^\d+\.\d+\.\d+(?:-dev\.\d+)?$' -or @($manifest.files).Count -ne 2) {throw 'Invalid release manifest.'}
foreach($relative in $paths) {
    $entry=@($manifest.files | Where-Object path -CEQ $relative)
    if($entry.Count -ne 1 -or $entry[0].sha256 -notmatch '^[A-Fa-f0-9]{64}$' -or $entry[0].size -le 0 -or $entry[0].size -gt 10485760) {throw 'Invalid release file.'}
}
function Get-Conflicts {
    @(Get-ChildItem -LiteralPath (Join-Path $root 'Mods') -File -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -match '^CostumeUnlocker(?:-\d+\.\d+\.\d+(?:\.\d+)?)?\.dll$' -or
        ($_.Name -ieq 'Mod.dll' -and (Get-FileHash -LiteralPath $_.FullName).Hash -eq '55EEBC450ECD6C06BCB010B91B2F2F1B78DE89C3D323222BE9D77B2913E9E464')
    })
}
$upToDate=@(Get-Conflicts).Count -eq 0
foreach($entry in $manifest.files) {
    $destination=Join-Path $root $entry.path
    if(!(Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $destination).Hash -ne $entry.sha256) {$upToDate=$false}
}
if(!$upToDate) {
    $work=Join-Path ([IO.Path]::GetTempPath()) ('LastEpochLAN-update-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    $changed=@()
    try {
        # Download and verify the complete pair before changing any installed file.
        foreach($entry in $manifest.files) {
            $target=Join-Path $work ([IO.Path]::GetFileName($entry.path))
            if($ReleaseDirectory) {Copy-Item -LiteralPath (Join-Path $ReleaseDirectory $entry.path) -Destination $target}
            else {Invoke-WebRequest -UseBasicParsing -Uri ($base+$entry.path) -OutFile $target -TimeoutSec 90}
            if((Get-Item -LiteralPath $target).Length -ne $entry.size -or (Get-FileHash -LiteralPath $target).Hash -ne $entry.sha256) {throw 'Downloaded DLL failed size/SHA-256 verification.'}
            $identity=[Reflection.AssemblyName]::GetAssemblyName($target)
            if($identity.Name -cne [IO.Path]::GetFileNameWithoutExtension($entry.path)) {throw 'Unexpected DLL identity.'}
            # Core has its own stable assembly version; manifest hash pins the exact paired build.
            if($identity.Name -ceq 'LastEpochLAN' -and $identity.Version.ToString(3) -ne ($manifest.version -replace '-.*$','')) {throw 'Unexpected mod version.'}
        }
        Assert-Closed
        $backup=Join-Path $root ('UserData/LastEpochLAN/backups/update-'+[Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        $conflicts=@(Get-Conflicts)
        $targets=@($paths)+@($conflicts | ForEach-Object {'Mods/'+$_.Name})
        # Back up every original before the first overwrite/removal.
        foreach($relative in $targets) {
            $destination=Join-Path $root $relative
            $saved=Join-Path $backup ([IO.Path]::GetFileName($relative))
            $exists=Test-Path -LiteralPath $destination
            if($exists) {Copy-Item -LiteralPath $destination -Destination $saved}
            $changed+=@{Destination=$destination;Saved=$saved;Existed=$exists}
        }
        foreach($entry in $manifest.files) {
            $destination=Join-Path $root $entry.path
            New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $work ([IO.Path]::GetFileName($entry.path))) -Destination $destination -Force
            if((Get-FileHash -LiteralPath $destination).Hash -ne $entry.sha256) {throw 'Installed DLL hash mismatch.'}
        }
        foreach($conflict in $conflicts) {Remove-Item -LiteralPath $conflict.FullName}
        Write-Host ('Updated to '+$manifest.version+'. Backups: '+$backup)
    } catch {
        foreach($item in $changed) {
            if($item.Existed) {Copy-Item -LiteralPath $item.Saved -Destination $item.Destination -Force}
            elseif(Test-Path -LiteralPath $item.Destination) {Remove-Item -LiteralPath $item.Destination}
        }
        throw
    } finally {
        foreach($relative in $paths) {
            $temporary=Join-Path $work ([IO.Path]::GetFileName($relative))
            if(Test-Path -LiteralPath $temporary) {Remove-Item -LiteralPath $temporary}
        }
        [IO.Directory]::Delete($work,$false)
    }
} else {Write-Host ('Already current: '+$manifest.version+' (both SHA-256 hashes verified).')}
Write-Host 'Use Play Offline. Update both PCs before connecting.'
if(!$NoLaunch) {
    Assert-Closed
    $parent=[IO.Directory]::GetParent($root)
    $steamManifest=if($parent -and $parent.Parent) {Join-Path $parent.Parent.FullName 'appmanifest_899770.acf'} else {''}
    if($parent -and $parent.Name -eq 'common' -and [IO.Path]::GetFileName($root) -eq 'Last Epoch' -and $steamManifest -and (Test-Path -LiteralPath $steamManifest)) {
        Start-Process -FilePath 'steam://rungameid/899770'
    } else {Start-Process -FilePath $exe -WorkingDirectory $root -WindowStyle Normal}
}
