param([string]$PackageRoot = (Split-Path -Parent $PSScriptRoot), [string]$ReportPath = 'validation/dev55-world-assist-installer.json')
$ErrorActionPreference='Stop'
$caseRoot=Join-Path $PackageRoot ('research/world-assist-installer-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$fakeGame=Join-Path $caseRoot 'game'
New-Item -ItemType Directory -Force -Path (Join-Path $fakeGame 'MelonLoader'),(Join-Path $fakeGame 'Mods'),(Join-Path $fakeGame 'UserLibs') | Out-Null
Set-Content -LiteralPath (Join-Path $fakeGame 'GameAssembly.dll') -Value 'fake game for installer test'
Set-Content -LiteralPath (Join-Path $fakeGame 'Mods/LastEpochLAN.dll') -Value 'previous mod'
Set-Content -LiteralPath (Join-Path $fakeGame 'UserLibs/LastEpochLAN.Core.dll') -Value 'previous core'
$checks=[Collections.Generic.List[string]]::new()
function Check([bool]$Passed,[string]$Label){if(!$Passed){throw "FAIL: $Label"};$checks.Add($Label);Write-Output "PASS: $Label"}
function Get-Process { param($Name,$ErrorAction) [pscustomobject]@{Id=1;ProcessName='Last Epoch'} }
$installer=Join-Path $PackageRoot 'Install-WorldAssist-Candidate.ps1'
$blocked=$false
try{ & $installer -GameRoot $fakeGame | Out-Null }catch{$blocked=$_.Exception.Message -like 'Close Last Epoch*';if(!$blocked){throw}}
Check $blocked 'running game blocks installer'
Check ((Get-Content -LiteralPath (Join-Path $fakeGame 'Mods/LastEpochLAN.dll') -Raw).Trim() -eq 'previous mod') 'blocked install keeps old mod'
Check (!(Test-Path -LiteralPath (Join-Path $fakeGame 'UserData'))) 'blocked install creates no backup or user data'
function Get-Process { param($Name,$ErrorAction) }
& $installer -GameRoot $fakeGame | Out-Null
$manifest=Get-Content -LiteralPath (Join-Path $PackageRoot 'latest.json') -Raw | ConvertFrom-Json
foreach($entry in $manifest.files){Check ((Get-FileHash -LiteralPath (Join-Path $fakeGame $entry.path)).Hash -eq $entry.sha256) "paired installed SHA256: $($entry.path)"}
$backups=@(Get-ChildItem -LiteralPath (Join-Path $fakeGame 'UserData/LastEpochLAN/backups') -Directory)
Check ($backups.Count -eq 1) 'one backup directory created'
Check ((Get-Content -LiteralPath (Join-Path $backups[0].FullName 'Mods/LastEpochLAN.dll') -Raw).Trim() -eq 'previous mod') 'backup contains old mod'
Check ((Get-Content -LiteralPath (Join-Path $backups[0].FullName 'UserLibs/LastEpochLAN.Core.dll') -Raw).Trim() -eq 'previous core') 'backup contains old core'
$badPackage=Join-Path $caseRoot 'tampered-package'
foreach($name in @('Mods/LastEpochLAN.dll','UserLibs/LastEpochLAN.Core.dll','latest.json','Install-WorldAssist-Candidate.ps1')){
    $to=Join-Path $badPackage $name
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $to) | Out-Null
    Copy-Item -LiteralPath (Join-Path $PackageRoot $name) -Destination $to
}
Add-Content -LiteralPath (Join-Path $badPackage 'Mods/LastEpochLAN.dll') -Value 'tamper'
$blocked=$false
try{ & (Join-Path $badPackage 'Install-WorldAssist-Candidate.ps1') -GameRoot $fakeGame | Out-Null }catch{$blocked=$_.Exception.Message -like 'Candidate checksum mismatch*'}
Check $blocked 'tampered candidate refused before destination writes'
Check (@(Get-ChildItem -LiteralPath (Join-Path $fakeGame 'UserData/LastEpochLAN/backups') -Directory).Count -eq 1) 'invalid package creates no extra backup'
foreach($entry in $manifest.files){Check ((Get-FileHash -LiteralPath (Join-Path $fakeGame $entry.path)).Hash -eq $entry.sha256) "invalid package leaves installed DLL unchanged: $($entry.path)"}
$manifest.version='invalid'
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $badPackage 'latest.json')
$blocked=$false
try{ & (Join-Path $badPackage 'Install-WorldAssist-Candidate.ps1') -GameRoot $fakeGame | Out-Null }catch{$blocked=$_.Exception.Message -like 'Unexpected candidate manifest*'}
Check $blocked 'wrong version manifest refused'
[pscustomobject]@{passed=$checks.Count;checks=$checks;actualGameModified=$false;fixtureRoot=$caseRoot} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath
Write-Output "All $($checks.Count) candidate installer checks passed; fake game directory only."
