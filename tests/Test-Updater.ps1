$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$script = Join-Path $root 'Update-And-Launch.ps1'
$fixture = Join-Path $root ('research\updater-fixture-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$global:updaterQadownloads = 0; $global:updaterQalaunches = 0; $global:updaterQacorrupt = $false; $global:updaterQaoffline = $false
$global:updaterQafailWrites = $false; $global:updaterQaactiveGame = $false
$global:updaterQasources = @{}
$global:updaterQarelease = @{ tag_name='v1.0.7'; draft=$false; prerelease=$false; assets=@() }
foreach ($rel in 'Mods\LastEpochLAN.dll','Mods\LastEpoch_Hud.dll','UserLibs\LastEpochLAN.Core.dll') {
    $source = Join-Path $root ('releases\TM-Coop-1.0.7-DLLs\' + $rel)
    $name = [IO.Path]::GetFileName($source)
    $global:updaterQasources[$name] = $source
    $global:updaterQarelease.assets += @{name=$name;state='uploaded';size=(Get-Item $source).Length;
        digest=('sha256:'+(Get-FileHash $source).Hash.ToLowerInvariant());
        browser_download_url=('https://github.com/cielthephantom2/LastEpoch/releases/download/v1.0.7/'+$name)}
}
function Invoke-RestMethod { param($Uri,$Headers,$TimeoutSec)
    if ($global:updaterQaoffline) {throw 'Simulated GitHub outage'}
    return $global:updaterQarelease
}
function Invoke-WebRequest { param($Uri,$Headers,$OutFile,[switch]$UseBasicParsing,$TimeoutSec)
    $global:updaterQadownloads++
    Copy-Item -LiteralPath $global:updaterQasources[[IO.Path]::GetFileName($Uri)] -Destination $OutFile
    if ($global:updaterQacorrupt) { [IO.File]::AppendAllText($OutFile,'corrupt') }
}
function Get-FileHash { param($LiteralPath,$Algorithm='SHA256')
    if ($LiteralPath.EndsWith('\GameAssembly.dll')) { return @{Hash='D5F9FB458F0E9878E697A92B590A586A52FD77E645F8BF87BB4F2B74C6C56567'} }
    Microsoft.PowerShell.Utility\Get-FileHash -LiteralPath $LiteralPath -Algorithm $Algorithm
}
function Get-Process { param($Name,$ErrorAction)
    if ($global:updaterQaactiveGame) { [pscustomobject]@{Path=(Join-Path $fixture 'Last Epoch.exe')} }
}
function Start-Process { param($FilePath,$WorkingDirectory,$ArgumentList)
    if ($FilePath -ne (Join-Path $fixture 'Last Epoch.exe') -or $ArgumentList[0] -ne '--offline') {throw 'Wrong launch target'}
    $global:updaterQalaunches++
}
$global:updaterQachecks = @()
function Check($condition,$message) {
    if (!$condition) {throw "FAIL: $message"}
    $global:updaterQachecks += $message; Write-Host "PASS: $message"
}
function Expect-Failure($action,$message) {
    $failed=$false; try {& $action} catch {$failed=$true}
    Check $failed $message
}
foreach ($rel in 'Last Epoch.exe','GameAssembly.dll','version.dll','MelonLoader\net6\MelonLoader.dll') {
    $p=Join-Path $fixture $rel; New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($p)) -Force | Out-Null
    [IO.File]::WriteAllText($p,'inactive fixture; never executed')
}
& $script -GamePath $fixture -NoLaunch
Check ($global:updaterQadownloads -eq 3 -and $global:updaterQalaunches -eq 0) 'first install verifies and places all three DLLs without launching'
foreach ($asset in $global:updaterQarelease.assets) {
    $folder=if ($asset.name -eq 'LastEpochLAN.Core.dll') {'UserLibs'} else {'Mods'}
    $file=Join-Path $fixture ($folder+'\'+$asset.name)
    Check (('sha256:'+(Get-FileHash $file).Hash.ToLowerInvariant()) -eq $asset.digest) ("installed bytes match release: "+$asset.name)
}
& $script -GamePath $fixture
Check ($global:updaterQadownloads -eq 3 -and $global:updaterQalaunches -eq 1) 'already-current launcher skips downloads and opens only the intended offline game'
$mod=Join-Path $fixture 'Mods\LastEpochLAN.dll'
[IO.File]::WriteAllText($mod,'previous fixture version')
$global:updaterQacorrupt=$true
Expect-Failure { & $script -GamePath $fixture } 'corrupt download rejected before writes or launch'
Check ([IO.File]::ReadAllText($mod) -eq 'previous fixture version' -and $global:updaterQalaunches -eq 1) 'corrupt download leaves installed DLL intact'
$global:updaterQacorrupt=$false
$handle=[IO.File]::Open((Join-Path $fixture 'UserLibs\LastEpochLAN.Core.dll'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try {Expect-Failure { & $script -GamePath $fixture } 'locked DLL blocks the whole update'} finally {$handle.Dispose()}
Check ([IO.File]::ReadAllText($mod) -eq 'previous fixture version' -and $global:updaterQalaunches -eq 1) 'lock failure never partially replaces DLLs or launches'
& $script -GamePath $fixture -NoLaunch
$saved=@(Get-ChildItem -LiteralPath (Join-Path $fixture 'TM-Coop-Backups') -Recurse -Filter LastEpochLAN.dll)
Check ($saved.Count -eq 1 -and [IO.File]::ReadAllText($saved[0].FullName) -eq 'previous fixture version') 'successful replacement preserves the previous DLL in backup'
$global:updaterQaoffline=$true
Expect-Failure { & $script -GamePath $fixture } 'GitHub outage does not start the game'
$global:updaterQaoffline=$false; $global:updaterQaactiveGame=$true
Expect-Failure { & $script -GamePath $fixture } 'running game is rejected before download or update'
$global:updaterQaactiveGame=$false
# Inject a one-shot write failure after the first DLL was changed. Backups
# and native FileStreams use real filesystem operations throughout this test.
[IO.File]::WriteAllText($mod,'rollback original mod')
$hud=Join-Path $fixture 'Mods\LastEpoch_Hud.dll'; [IO.File]::WriteAllText($hud,'rollback original hud')
function global:Write-Stream { param($source,$destination)
    $global:updaterQawriteCalls++
    if ($global:updaterQawriteCalls -eq 2) {throw 'Simulated disk write failure'}
    $inputFile=[IO.File]::OpenRead($source)
    try {$destination.Position=0; $destination.SetLength(0); $inputFile.CopyTo($destination); $destination.Flush($true)} finally {$inputFile.Dispose()}
}
# Function definitions inside the updater override parent mocks. Instead, run
# a fixture-only copy with its Write-Stream renamed so this injected implementation is used.
$testCopy=Join-Path $fixture 'Updater-FaultFixture.ps1'
$text=[IO.File]::ReadAllText($script).Replace('function Write-Stream(', 'function Original-Write-Stream(')
[IO.File]::WriteAllText($testCopy,$text)
$global:updaterQawriteCalls=0
Expect-Failure { & $testCopy -GamePath $fixture } 'mid-transaction write failure is reported'
Check ([IO.File]::ReadAllText($mod) -eq 'rollback original mod' -and [IO.File]::ReadAllText($hud) -eq 'rollback original hud' -and $global:updaterQalaunches -eq 1) 'mid-write failure restores both previous DLLs and does not launch'
Remove-Item function:global:Write-Stream
@{passed=$global:updaterQachecks.Count;checks=$global:updaterQachecks;fixture=$fixture;scope='PS5.1 mocked GitHub/process responses; real DLL metadata, SHA256, exclusive file locks, backups and rollback; no game started'} |
    ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root 'research\updater-qa.json')
