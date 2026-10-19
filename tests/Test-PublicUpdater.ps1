param([string]$UpdaterPath='D:\New folder\CostumeUnlocker-Publish\update.bat',[switch]$Live)
$ErrorActionPreference='Stop'
$repo=Split-Path $UpdaterPath -Parent
$root=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $root ('research\public-updater-fixture-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'MelonLoader\net6') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $fixture 'Last Epoch.exe'),'INACTIVE FIXTURE - DO NOT EXECUTE')
[IO.File]::WriteAllText((Join-Path $fixture 'MelonLoader\net6\MelonLoader.dll'),'inactive fixture')
$text=[IO.File]::ReadAllText($UpdaterPath);$marker='### POWERSHELL ###'
$updater=[scriptblock]::Create($text.Substring($text.LastIndexOf($marker)+$marker.Length))
$manifest=Get-Content -LiteralPath (Join-Path $repo 'latest.json') -Raw | ConvertFrom-Json
$sha='0123456789abcdef0123456789abcdef01234567'
function Packet([string]$body) {return ('{0:x4}' -f ($body.Length+4))+$body}
$valid=(Packet "# service=git-upload-pack`n")+'0000'+(Packet ($sha+" HEAD`0symref=HEAD:refs/heads/main`n"))+(Packet ($sha+" refs/heads/main`n"))+'0000'
$advertisement=$valid;$binary=$true;$corrupt=$false;$running=$false;$online=$false
$requests=New-Object 'System.Collections.Generic.List[string]'
$checks=New-Object 'System.Collections.Generic.List[string]'
function Check($condition,[string]$message) {if(!$condition){throw ('FAIL: '+$message)};$checks.Add($message);Write-Host ('PASS: '+$message)}
function Expect-Failure([scriptblock]$action,[string]$message) {$failed=$false;try {& $action} catch {$failed=$true};Check $failed $message}
function Invoke-WebRequest {
 param($Uri,$Headers,$OutFile,[switch]$UseBasicParsing,$TimeoutSec)
 $requests.Add([string]$Uri)
 if($online) {return Microsoft.PowerShell.Utility\Invoke-WebRequest @PSBoundParameters}
 if($Uri -like '*api.github.com*') {throw 'REST API must never be called'}
 if($Uri -like '*info/refs?service=git-upload-pack') {
  $content=if($binary){,[Text.Encoding]::ASCII.GetBytes($advertisement)}else{$advertisement}
  return [pscustomobject]@{Content=$content}
 }
 if(!$Uri.StartsWith('https://raw.githubusercontent.com/cielthephantom2/CostumeUnlocker/'+$sha+'/')) {throw 'Unpinned asset URL'}
 Copy-Item -LiteralPath (Join-Path $repo ($Uri.Substring($Uri.IndexOf($sha)+41))) -Destination $OutFile
 if($corrupt) {[IO.File]::AppendAllText($OutFile,'corrupt')}
}
function Invoke-RestMethod {
 param($Uri,$TimeoutSec)
 $requests.Add([string]$Uri)
 if($Uri -like '*api.github.com*') {throw 'REST API must never be called'}
 if($online) {return Microsoft.PowerShell.Utility\Invoke-RestMethod @PSBoundParameters}
 if($Uri -cne ('https://raw.githubusercontent.com/cielthephantom2/CostumeUnlocker/'+$sha+'/latest.json')) {throw 'Unpinned manifest URL'}
 return $manifest
}
function Get-Process {param($Name,$ErrorAction);if($running){[pscustomobject]@{Path=(Join-Path $fixture 'Last Epoch.exe')}}}
function Start-Process {throw 'A test must never launch the game'}
& $updater -GamePath $fixture -NoLaunch
foreach($entry in $manifest.files) {Check ((Get-FileHash -LiteralPath (Join-Path $fixture $entry.path)).Hash -eq $entry.sha256) ('verified paired install: '+$entry.path)}
Check (@($requests | Where-Object {$_ -like '*api.github.com*'}).Count -eq 0) 'release lookup uses no GitHub REST API'
Check (@($requests | Where-Object {$_ -like ('*/'+$sha+'/*')}).Count -eq 3) 'manifest and both DLLs use exactly one pinned commit'
$before=$requests.Count;$binary=$false
& $updater -GamePath $fixture -NoLaunch
Check ($requests.Count-$before -eq 2) 'already-current install accepts text advertisement and skips both DLL downloads'
foreach($bad in @('<html>403 forbidden</html>','0009abc','0003',((Packet "# service=git-upload-pack`n")+'0000'),($valid.Substring(0,$valid.Length-4)+(Packet ($sha+" refs/heads/main`n"))+'0000'))) {
 $advertisement=$bad
 Expect-Failure {& $updater -GamePath $fixture -NoLaunch} 'malformed, truncated, missing or duplicate branch advertisement rejected'
}
$advertisement=$valid
$mod=Join-Path $fixture 'Mods\LastEpochLAN.dll'
[IO.File]::WriteAllText($mod,'original installed bytes')
$corrupt=$true
Expect-Failure {& $updater -GamePath $fixture -NoLaunch} 'corrupt downloaded DLL rejected'
Check ([IO.File]::ReadAllText($mod) -eq 'original installed bytes') 'download failure preserves installed files'
$corrupt=$false;$running=$true;$before=$requests.Count
Expect-Failure {& $updater -GamePath $fixture -NoLaunch} 'running game rejected before network or writes'
Check ($requests.Count -eq $before) 'running-game guard performs no network requests'
$running=$false
if($Live) {
 $online=$true
 & $updater -GamePath $fixture -NoLaunch
 foreach($entry in $manifest.files) {Check ((Get-FileHash -LiteralPath (Join-Path $fixture $entry.path)).Hash -eq $entry.sha256) ('live GitHub download verified: '+$entry.path)}
}
Check ([IO.File]::ReadAllText((Join-Path $fixture 'Last Epoch.exe')) -eq 'INACTIVE FIXTURE - DO NOT EXECUTE') 'only inactive fixture paths were used; no game launched'
[pscustomobject]@{passed=$checks.Count;live=[bool]$Live;fixture=$fixture;checks=@($checks);requests=@($requests)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'research\public-updater-qa.json') -Encoding UTF8
Write-Host ('All '+$checks.Count+' public updater checks passed.')