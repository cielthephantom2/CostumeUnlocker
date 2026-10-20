param([string]$ReportPath='validation\encounter-simulation-dev22.json')
$ErrorActionPreference='Stop'
# Pure managed subprocesses and loopback TCP only. No game/sandbox executable,
# Steam DLL writes, save/profile changes, or game process termination.
$projectRoot=$PSScriptRoot
$steamRoot='D:\SteamLibrary\steamapps\common\Last Epoch'
$installedFiles=@((Join-Path $steamRoot 'Mods\LastEpochLAN.dll'),(Join-Path $steamRoot 'UserLibs\LastEpochLAN.Core.dll'))
function InstalledHashes {
    @($installedFiles | Where-Object {Test-Path -LiteralPath $_} | ForEach-Object {
        @{path=$_;hash=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}
    })
}
$before=InstalledHashes
$gameBefore=@(Get-Process 'Last Epoch' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$started=Get-Date -Format o
$output=@(& dotnet run --project (Join-Path $projectRoot 'tests\LastEpochLAN.SelfTest') -c Release -- --encounter-tests 2>&1)
$testExit=$LASTEXITCODE
$after=InstalledHashes
$gameAfter=@(Get-Process 'Last Epoch' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$unchanged=($before | ConvertTo-Json -Compress) -eq ($after | ConvertTo-Json -Compress)
$passed=@($output | Where-Object {"$_" -like 'PASS:*'}).Count
$report=@{started=$started;finished=(Get-Date -Format o);passed=($testExit -eq 0 -and $unchanged);exitCode=$testExit;checks=$passed;
    scope='Managed simulation: loopback TCP 1 Host + 2 Clients, simulated native adapters and rewards. No native engine/gameplay acceptance.';
    installedDllsUnchanged=$unchanged;before=$before;after=$after;gamePidsBefore=$gameBefore;gamePidsAfter=$gameAfter;output=@($output | ForEach-Object {"$_"})}
$absoluteReport=if([IO.Path]::IsPathRooted($ReportPath)){$ReportPath}else{Join-Path $projectRoot $ReportPath}
New-Item -ItemType Directory -Path (Split-Path $absoluteReport) -Force | Out-Null
$report | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $absoluteReport -Encoding utf8
$output | ForEach-Object {Write-Output $_}
if(!$report.passed){throw "Encounter simulation failed; report: $absoluteReport"}
Write-Output "Report: $absoluteReport; Steam DLLs unchanged."
