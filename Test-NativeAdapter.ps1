param([int]$TimeoutSeconds = 90, [string]$ModAssembly = '', [string]$CoreAssembly = '', [switch]$NativeServer, [switch]$NativeSession, [switch]$NativeAssets, [switch]$NativeCampaign, [switch]$NativeEncounters, [switch]$NativeStatus, [switch]$OfflineTools, [switch]$NativeVfx, [switch]$NativeGift, [switch]$GiftReload, [switch]$GiftTransfer, [switch]$GiftTransferReplay, [switch]$GiftMenu, [switch]$PartyTravel, [switch]$PartyBoss, [switch]$HudPolicy, [switch]$RenderUi, [switch]$LootFilters, [switch]$AutoPlay, [switch]$OmenRewards, [switch]$SharedChest, [switch]$ChestUi, [switch]$CaptureChest, [ValidateSet(0,1,2)][int]$StashVersion=0, [string]$RuntimeRoot='', [string]$SandboxRoot='')
$ErrorActionPreference = 'Stop'
if ($CaptureChest -or $StashVersion -gt 0) { $SharedChest = $true }
if ($OmenRewards -or $SharedChest) { $NativeCampaign = $true }
if ($SharedChest) { $RenderUi = $true } # Native pointer raycasts require rendered Canvas depth.
if ($NativeVfx) { $NativeCampaign = $true }
if ($LootFilters) { $NativeCampaign = $true }
if ($AutoPlay) { $NativeCampaign = $true; $RenderUi = $true }
if ($NativeEncounters) { throw 'Legacy encounter smoke was removed. Use -OmenRewards for the native isolation test.' }
if ($NativeStatus) { $NativeCampaign = $true }
if ($OfflineTools) { $NativeCampaign = $true }
if ($GiftTransferReplay) { $GiftTransfer = $true }
if ($GiftMenu) { $GiftTransfer = $true }
if ($PartyTravel -or $PartyBoss -or $HudPolicy) { $GiftReload = $true }
if ($GiftTransfer) { $GiftReload = $true }
if ($GiftReload) { $NativeGift = $true }
if ($NativeGift) { $NativeCampaign = $true }
if ($NativeCampaign) { $NativeAssets = $true }
if ($ChestUi) { $NativeAssets = $true; $RenderUi = $true }
# Dedicated headless sandbox: no campaign entry, character load, or save operations.
$sourceRuntime = if ($RuntimeRoot) { $RuntimeRoot } else { Join-Path $PSScriptRoot 'runtime' }
$qaRuntime = if ($SandboxRoot) { $SandboxRoot } else { Join-Path $PSScriptRoot 'qa-adapter-runtime' }
$qaExe = Join-Path $qaRuntime 'Last Epoch.exe'
if (Get-Process 'Last Epoch' -ErrorAction SilentlyContinue) { throw 'Close gameplay before the native adapter test.' }
New-Item -ItemType Directory -Path $qaRuntime -Force | Out-Null
Get-ChildItem -LiteralPath $sourceRuntime -File | Where-Object { $_.Extension -in '.dll', '.exe' } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $qaRuntime -Force }
foreach ($name in @('Last Epoch_Data', 'D3D12', 'Licenses')) {
    $linkPath = Join-Path $qaRuntime $name
    if (-not (Test-Path -LiteralPath $linkPath)) {
        New-Item -ItemType Junction -Path $linkPath -Target (Join-Path $sourceRuntime $name) | Out-Null
    }
}
$qaLoader = Join-Path $qaRuntime 'MelonLoader'
New-Item -ItemType Directory -Path $qaLoader -Force | Out-Null
foreach ($name in @('Dependencies', 'net6', 'net35', 'net472')) {
    if (-not (Test-Path -LiteralPath (Join-Path $qaLoader $name))) {
        Copy-Item -LiteralPath (Join-Path $sourceRuntime "MelonLoader\$name") -Destination $qaLoader -Recurse
    }
}
$interopLink = Join-Path $qaLoader 'Il2CppAssemblies'
if (-not (Test-Path -LiteralPath $interopLink)) {
    New-Item -ItemType Junction -Path $interopLink -Target (Join-Path $sourceRuntime 'MelonLoader\Il2CppAssemblies') | Out-Null
}
foreach ($name in @('Mods', 'UserLibs', 'UserData')) { New-Item -ItemType Directory -Path (Join-Path $qaRuntime $name) -Force | Out-Null }
# This reusable sandbox must not retain unrelated mods from earlier research.
# Preserve them outside the loader's Mods directory; never touch installed mods.
$qaModsRoot = [IO.Path]::GetFullPath((Join-Path $qaRuntime 'Mods'))
$qaDisabledRoot = [IO.Path]::GetFullPath((Join-Path $qaRuntime 'DisabledMods'))
foreach ($oldMod in Get-ChildItem -LiteralPath $qaModsRoot -File -Filter '*.dll') {
    if ($oldMod.Name -in @('LastEpochLAN.dll', 'LastEpochLAN.GuestProfile.dll')) { continue }
    if ($oldMod.DirectoryName -ne $qaModsRoot -or -not $qaDisabledRoot.StartsWith([IO.Path]::GetFullPath($qaRuntime) + [IO.Path]::DirectorySeparatorChar)) { throw 'Invalid sandbox mod quarantine path.' }
    New-Item -ItemType Directory -Path $qaDisabledRoot -Force | Out-Null
    Move-Item -LiteralPath $oldMod.FullName -Destination (Join-Path $qaDisabledRoot ($oldMod.BaseName + '-' + [Guid]::NewGuid().ToString('N') + '.dll'))
}
if (!$ModAssembly) { $ModAssembly = Join-Path $PSScriptRoot 'src\LastEpochLAN.Mod\bin\Release\net6.0\LastEpochLAN.dll' }
Copy-Item -LiteralPath $ModAssembly -Destination (Join-Path $qaRuntime 'Mods\LastEpochLAN.dll') -Force
if (!$CoreAssembly) { $CoreAssembly = Join-Path $PSScriptRoot 'src\LastEpochLAN.Core\bin\Release\net6.0\LastEpochLAN.Core.dll' }
Copy-Item -LiteralPath $CoreAssembly -Destination (Join-Path $qaRuntime 'UserLibs\LastEpochLAN.Core.dll') -Force
$profileMod = Join-Path $qaRuntime 'Mods\LastEpochLAN.GuestProfile.dll'
if ($NativeAssets) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'tools\GuestProfile\bin\Release\net6.0\LastEpochLAN.GuestProfile.dll') -Destination $profileMod -Force
    # GameLoader initializes Steam through the normal client API. Supply the real
    # installed game's AppID for this copied executable; Steam still checks access.
    Set-Content -LiteralPath (Join-Path $qaRuntime 'steam_appid.txt') -Value '899770' -Encoding ASCII
} elseif (Test-Path -LiteralPath $profileMod) { Remove-Item -LiteralPath $profileMod }
@{ Name = 'AdapterQA'; AutoStart = $false; RoomCode = 'AdapterQAOnly'; HostOwnedEnemies = $false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $qaRuntime 'UserData\LastEpochLAN.config.json') -Encoding UTF8
$reportPath = Join-Path $qaRuntime 'UserData\LastEpochLAN.runtime-report.json'
$startedUtc = [DateTime]::UtcNow
$qaProcess = $null
try {
    $qaArguments = @(
        '--offline', '--melonloader.agfoffline', '--lelan-adapter-smoke', '-batchmode',
        '-logFile', ('"' + (Join-Path $qaRuntime 'adapter-player.log') + '"'))
    if ($RenderUi) { $qaArguments = @($qaArguments | Where-Object { $_ -ne '-batchmode' }) }
    if (-not $NativeCampaign) { $qaArguments += '-nographics' }
    else { $qaArguments += @('-screen-fullscreen', '0', '-screen-width', '800', '-screen-height', '600') }
    if ($NativeServer) { $qaArguments += '--lelan-native-server-smoke' }
    if ($NativeSession) { $qaArguments += '--lelan-native-session-smoke' }
    if ($NativeAssets) { $qaArguments += '--lelan-native-assets-smoke' }
    if ($NativeCampaign) { $qaArguments += '--lelan-private-campaign-smoke' }
    if ($LootFilters) { $qaArguments += '--lelan-loot-filter-smoke' }
    if ($AutoPlay) { $qaArguments += '--lelan-autoplay-smoke' }
    if ($OmenRewards) { $qaArguments += '--lelan-omen-reward-smoke' }
    if ($SharedChest) { $qaArguments += '--lelan-shared-chest-smoke' }
    if ($CaptureChest) { $qaArguments += '--lelan-render-chest-qa' }
    if ($StashVersion -gt 0) { $qaArguments += ('--lelan-stash-v'+$StashVersion) }
    if ($ChestUi) { $qaArguments += '--lelan-chest-ui-smoke' }
    if ($NativeEncounters) { $qaArguments += '--lelan-encounter-smoke' }
    if ($NativeStatus) { $qaArguments += '--lelan-status-smoke' }
    if ($OfflineTools) { $qaArguments += '--lelan-offline-tools-smoke' }
    if ($NativeVfx) { $qaArguments += '--lelan-vfx-smoke' }
    if ($NativeGift) { $qaArguments += '--lelan-gift-smoke' }
    if ($GiftReload) { $qaArguments += '--lelan-gift-reload' }
    if ($GiftTransfer) { $qaArguments += '--lelan-gift-transfer' }
    if ($GiftTransferReplay) { $qaArguments += '--lelan-gift-transfer-replay' }
    if ($GiftMenu) { $qaArguments += '--lelan-gift-menu' }
    if ($PartyTravel) { $qaArguments += '--lelan-party-travel' }
    if ($PartyBoss) { $qaArguments += '--lelan-party-boss' }
    if ($HudPolicy) { $qaArguments += '--lelan-hud-policy' }
    $qaProcess = Start-Process -FilePath $qaExe -WorkingDirectory $qaRuntime -WindowStyle Hidden -PassThru -ArgumentList $qaArguments
    Write-Output "Native adapter QA process: $($qaProcess.Id). Installed gameplay DLLs unchanged."
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Seconds 2
        $qaProcess.Refresh()
        if ($qaProcess.HasExited) { throw "Native QA exited before passing (exit $($qaProcess.ExitCode))." }
        if ((Test-Path -LiteralPath $reportPath) -and (Get-Item -LiteralPath $reportPath).LastWriteTimeUtc -ge $startedUtc) {
            try { $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json } catch { continue }
            if ($report.adapterSmokePassed -and $report.worldSmokePassed) {
                if ($NativeAssets) {
                    $assetsReportPath = Join-Path $qaRuntime 'UserData\LastEpochLAN.native-assets-probe.json'
                    if (-not (Test-Path -LiteralPath $assetsReportPath) -or (Get-Item -LiteralPath $assetsReportPath).LastWriteTimeUtc -lt $startedUtc) { continue }
                    Copy-Item -LiteralPath $assetsReportPath -Destination (Join-Path $PSScriptRoot 'research\native-assets-probe-result.json') -Force
                    $assetsReport = Get-Content -LiteralPath $assetsReportPath -Raw | ConvertFrom-Json
                    if (-not $assetsReport.passed) { throw "Native asset probe failed: $($assetsReport.error)" }
                }
                if ($NativeCampaign) {
                    $campaignReportPath = Join-Path $qaRuntime 'UserData\LastEpochLAN.native-campaign-probe.json'
                    if (-not (Test-Path -LiteralPath $campaignReportPath) -or (Get-Item -LiteralPath $campaignReportPath).LastWriteTimeUtc -lt $startedUtc) { continue }
                    Copy-Item -LiteralPath $campaignReportPath -Destination (Join-Path $PSScriptRoot 'research\native-campaign-probe-result.json') -Force
                    $campaignReport = Get-Content -LiteralPath $campaignReportPath -Raw | ConvertFrom-Json
                    if (-not $campaignReport.passed) { throw "Native campaign probe failed: $($campaignReport.error)" }
                    if ($LootFilters -and @($campaignReport.nativeLootFilters).Count -ne 10) {
                        throw 'Native LootFilter probe did not complete all ten observations.'
                    }
                    if ($OmenRewards -and (@($campaignReport.nativeEncounterIsolation).Count -ne 2 -or
                        @($campaignReport.nativeEncounterIsolation | Where-Object { $_.created -lt 1 -or $_.corrupted -lt 1 }).Count -gt 0)) {
                        throw 'Native isolation did not prove Corrupted ground drops in both Omen reward paths.'
                    }
                    if ($NativeStatus -and (-not $campaignReport.nativeSourceOwnedDot -or -not $campaignReport.nativeRemoteStatusVisualsOnly -or -not $campaignReport.nativeCombinedIgnitePoison -or -not $campaignReport.nativeAggregateVfxReplacementAndCleanse)) {
                        throw 'Native source-owned DoT/combined fire-poison visual-only/cleanse observations did not finish.'
                    }
                    if ($NativeEncounters -and -not $campaignReport.encounterNativePrefabsAndLifecycle) {
                        throw 'Encounter native prefab/lifecycle observations did not finish.'
                    }
                    if ($NativeEncounters -and (-not $campaignReport.nemesisNativeCarrierVisualAndSpawner -or -not $campaignReport.nemesisPrivateCompletionCallbacksAndSave -or
                        -not $campaignReport.nemesisGuestNativePanelIntentSaveAck -or -not $campaignReport.nemesisAuthenticatedGuestNativeHostSpawnerDeath)) {
                        throw 'Nemesis native spawner/private completion/save observations did not finish.'
                    }
                }
                if ($ChestUi) {
                    $uiResultPath = Join-Path $qaRuntime 'UserData\shared-chest-ui-qa.json'
                    if (!(Test-Path -LiteralPath $uiResultPath) -or (Get-Item -LiteralPath $uiResultPath).LastWriteTimeUtc -lt $startedUtc) { continue }
                    $uiResult = Get-Content -LiteralPath $uiResultPath -Raw | ConvertFrom-Json
                    if (!$uiResult.passed) { throw ('Chest UI fixture failed: ' + $uiResult.error) }
                }
                # A reported result is not a pass if Unity's native callbacks threw.
                if ($NativeGift) {
                    $giftReportPath = Join-Path $qaRuntime 'UserData\LastEpochLAN.gift-probe.json'
                    if (-not (Test-Path -LiteralPath $giftReportPath) -or (Get-Item -LiteralPath $giftReportPath).LastWriteTimeUtc -lt $startedUtc) { continue }
                    $giftSuffix = if ($HudPolicy) { 'hud-policy' } elseif ($PartyBoss) { 'party-boss' } elseif ($PartyTravel) { 'party-travel' } elseif ($GiftMenu) { 'menu' } elseif ($GiftTransferReplay) { 'transfer-replay' } elseif ($GiftTransfer) { 'transfer' } elseif ($GiftReload) { 'reload' } else { 'save' }
                    Copy-Item -LiteralPath $giftReportPath -Destination (Join-Path $PSScriptRoot "research\native-gift-$giftSuffix-result.json") -Force
                    $giftReport = Get-Content -LiteralPath $giftReportPath -Raw | ConvertFrom-Json
                    if (-not $giftReport.passed) { throw "Native Gift $giftSuffix probe failed. See research/native-gift-$giftSuffix-result.json." }
                }
                if ($NativeVfx) {
                    $vfxReportPath = Join-Path $qaRuntime 'UserData\LastEpochLAN.vfx-probe.json'
                    if (-not (Test-Path -LiteralPath $vfxReportPath) -or (Get-Item -LiteralPath $vfxReportPath).LastWriteTimeUtc -lt $startedUtc) { continue }
                    Copy-Item -LiteralPath $vfxReportPath -Destination (Join-Path $PSScriptRoot 'research\native-vfx-probe-result.json') -Force
                    $vfxReport = Get-Content -LiteralPath $vfxReportPath -Raw | ConvertFrom-Json
                    if (-not $vfxReport.passed) { throw 'Native VFX capture/replay failed. See native-vfx-probe-result.json.' }
                }
                $playerLog = Join-Path $qaRuntime 'adapter-player.log'
                # Match error records, not ordinary stack-frame method names such as
                # AsyncUniTaskMethodBuilder.SetException in a warning's call stack.
                $errorPattern = '^\d{4}-\d\d-\d\dT[^\t]+\t(?:Exception|Error)\t|^\s*(?:System|Il2CppInterop)\.[A-Za-z.]*Exception:|\[ERROR\].*(?:Exception|Missing replication)|Crash!!!|Fatal error'
                $nativeErrors = @(Select-String -LiteralPath @($playerLog, (Join-Path $qaLoader 'Latest.log')) -Pattern $errorPattern -ErrorAction SilentlyContinue)
                if ($nativeErrors.Count -gt 0) {
                    if ($NativeCampaign -and $campaignReport) {
                        $campaignReport | Add-Member -NotePropertyName observationsPassed -NotePropertyValue $campaignReport.passed -Force
                        $campaignReport.passed = $false
                        $campaignReport | Add-Member -NotePropertyName runtimeErrors -NotePropertyValue @($nativeErrors.Line) -Force
                        $campaignReport | ConvertTo-Json -Depth 8 |
                            Set-Content -LiteralPath (Join-Path $PSScriptRoot 'research\native-campaign-probe-result.json') -Encoding UTF8
                    }
                    @{ utc = [DateTime]::UtcNow.ToString('o'); passed = $false; nativeEncounters = [bool]$NativeEncounters;
                        modSha256 = (Get-FileHash -LiteralPath (Join-Path $qaRuntime 'Mods\LastEpochLAN.dll')).Hash;
                        coreSha256 = (Get-FileHash -LiteralPath (Join-Path $qaRuntime 'UserLibs\LastEpochLAN.Core.dll')).Hash;
                        errors = @($nativeErrors.Line) } |
                        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'research\native-adapter-qa-result.json') -Encoding UTF8
                    throw ('Native QA emitted runtime errors: ' + ($nativeErrors.Line -join '; '))
                }
                if ($NativeSession) {
                    $sessionReportPath = Join-Path $qaRuntime 'UserData\LastEpochLAN.native-session-probe.json'
                    if (-not (Test-Path -LiteralPath $sessionReportPath) -or (Get-Item -LiteralPath $sessionReportPath).LastWriteTimeUtc -lt $startedUtc) { continue }
                    Copy-Item -LiteralPath $sessionReportPath -Destination (Join-Path $PSScriptRoot 'research\native-session-probe-result.json') -Force
                    $sessionReport = Get-Content -LiteralPath $sessionReportPath -Raw | ConvertFrom-Json
                    if (-not $sessionReport.passed) { throw "Native session probe failed: $($sessionReport.error)" }
                    Write-Output 'PASS: native session, object lifecycle/reconnect and generated RPC receivers. Campaign co-op is unverified.'
                }
                if ($NativeServer) {
                    $serverReportPath = Join-Path $qaRuntime 'UserData\LastEpochLAN.native-server-probe.json'
                    if (-not (Test-Path -LiteralPath $serverReportPath) -or (Get-Item -LiteralPath $serverReportPath).LastWriteTimeUtc -lt $startedUtc) { continue }
                    Copy-Item -LiteralPath $serverReportPath -Destination (Join-Path $PSScriptRoot 'research\native-server-probe-result.json') -Force
                    $serverReport = Get-Content -LiteralPath $serverReportPath -Raw | ConvertFrom-Json
                    if (-not $serverReport.passed) { throw "Native server transport probe failed: $($serverReport.error)" }
                    Write-Output "PASS: native server transport started/stopped at $($serverReport.endpoint). Gameplay server is unverified."
                }
                Copy-Item -LiteralPath $reportPath -Destination (Join-Path $PSScriptRoot 'research\runtime-integrated-adapter-smoke.json') -Force
                if ($NativeAssets) { Write-Output 'PASS: private native asset bootstrap and all five class movement/attack animation drivers.' }
                @{ utc = [DateTime]::UtcNow.ToString('o'); passed = $true; nativeAssets = [bool]$NativeAssets; nativeSession = [bool]$NativeSession; nativeCampaign = [bool]$NativeCampaign; nativeEncounters = [bool]$NativeEncounters;
                    scope = 'Opt-in native QA gates only; two-PC campaign co-op is unverified';
                    modSha256 = (Get-FileHash -LiteralPath (Join-Path $qaRuntime 'Mods\LastEpochLAN.dll')).Hash;
                    coreSha256 = (Get-FileHash -LiteralPath (Join-Path $qaRuntime 'UserLibs\LastEpochLAN.Core.dll')).Hash } |
                    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'research\native-adapter-qa-result.json') -Encoding UTF8
                Write-Output 'PASS: native inactive-component pose/HP and actual damage detours. Campaign and host-owned spawning are not validated.'
                return
            }
        }
        if ($qaProcess.HasExited) { throw "Native QA exited before passing (exit $($qaProcess.ExitCode))." }
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Native adapter QA timed out. Inspect qa-adapter-runtime/MelonLoader/Latest.log.'
}
finally {
    if ($qaProcess -ne $null) {
        $runningQA = Get-Process -Id $qaProcess.Id -ErrorAction SilentlyContinue
        if ($runningQA -and $runningQA.Path -eq $qaExe) { Stop-Process -Id $qaProcess.Id }
    }
}
