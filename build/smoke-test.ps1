<#
.SYNOPSIS
  Installs the signed KeepLid MSIX on a disposable Windows machine (CI) and checks, for real:
    1. crash recovery: a leftover restore.json is applied at launch and the lid action is put back;
    2. the app starts, stays idle at ~0 % CPU, and its memory footprint (target < 50 MB);
    3. the log is written in the package's redirected LocalAppData folder.
  Must run elevated (it changes the lid action with powercfg to simulate the crash).
#>
param(
    [Parameter(Mandatory = $true)][string]$MsixPath,
    [Parameter(Mandatory = $true)][string]$CertificatePath  # .cer
)
$ErrorActionPreference = 'Stop'
$failures = @()

function Get-LidAc {
    $out = powercfg /q SCHEME_CURRENT SUB_BUTTONS LIDACTION | Out-String
    if ($out -match 'Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)') { return [Convert]::ToInt32($Matches[1], 16) }
    throw "Cannot read the lid action:`n$out"
}

# On machines without a lid (CI VMs) Windows hides the lid setting from 'powercfg /q': unhide it.
powercfg -attributes SUB_BUTTONS LIDACTION -ATTRIB_HIDE

# --- Install ---------------------------------------------------------------------------------------
Import-Certificate -FilePath $CertificatePath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
Add-AppxPackage -Path $MsixPath
$pkg = Get-AppxPackage -Name 'KeepLid*' | Select-Object -First 1
Write-Host "Installed $($pkg.PackageFullName)"
$dataDir = Join-Path $env:LOCALAPPDATA "Packages\$($pkg.PackageFamilyName)\LocalCache\Local\KeepLid"
New-Item -ItemType Directory -Force $dataDir | Out-Null

# --- Simulate a crash that left the lid action modified ---------------------------------------------
$scheme = ((powercfg /getactivescheme) -replace '.*:\s*([0-9a-fA-F-]{36}).*', '$1').Trim()
$original = Get-LidAc
if ($original -eq 0) { powercfg /setacvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 1; $original = 1 }
powercfg /setacvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 0
powercfg /setactive SCHEME_CURRENT
Write-Host "Scheme $scheme, original AC lid action $original, now $(Get-LidAc) (simulated crash)"
@{
    FormatVersion = 1; SchemeGuid = $scheme; OriginalAc = $original; OriginalDc = $null
    AppliedValue = 0; CreatedAt = (Get-Date).ToString('o'); ProcessId = 0
} | ConvertTo-Json | Set-Content (Join-Path $dataDir 'restore.json') -Encoding UTF8
'{ "FirstRunCompleted": true }' | Set-Content (Join-Path $dataDir 'settings.json') -Encoding UTF8

# --- Launch through the execution alias ---------------------------------------------------------------
$alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\keeplid.exe'
Start-Process -FilePath $alias
$proc = $null
for ($i = 0; $i -lt 40 -and -not $proc; $i++) { Start-Sleep -Milliseconds 500; $proc = Get-Process -Name KeepLid -ErrorAction SilentlyContinue | Select-Object -First 1 }
if (-not $proc) { throw 'KeepLid did not start' }
Write-Host "KeepLid running, pid $($proc.Id)"

for ($i = 0; $i -lt 30 -and (Test-Path (Join-Path $dataDir 'restore.json')); $i++) { Start-Sleep -Milliseconds 500 }
$after = Get-LidAc
if (Test-Path (Join-Path $dataDir 'restore.json')) { $failures += 'restore.json still present after launch' }
if ($after -ne $original) { $failures += "lid action not restored (expected $original, got $after)" }
else { Write-Host "Crash recovery OK: lid action back to $after" }

# --- Watchdog: restore when the process it guards disappears ------------------------------------------
powercfg /setacvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 0
powercfg /setactive SCHEME_CURRENT
@{
    FormatVersion = 1; SchemeGuid = $scheme; OriginalAc = $original; OriginalDc = $null
    AppliedValue = 0; CreatedAt = (Get-Date).ToString('o'); ProcessId = 0
} | ConvertTo-Json | Set-Content (Join-Path $dataDir 'restore.json') -Encoding UTF8
$dummy = Start-Process -FilePath ping.exe -ArgumentList '-n 600 127.0.0.1' -WindowStyle Hidden -PassThru
Start-Process -FilePath $alias -ArgumentList @('--watchdog', $dummy.Id, "`"$(Join-Path $dataDir 'restore.json')`"", "`"$(Join-Path $dataDir 'log.txt')`"")
Start-Sleep -Seconds 3
Stop-Process -Id $dummy.Id -Force   # the "app" is killed
for ($i = 0; $i -lt 20 -and (Test-Path (Join-Path $dataDir 'restore.json')); $i++) { Start-Sleep -Milliseconds 500 }
if ((Get-LidAc) -ne $original -or (Test-Path (Join-Path $dataDir 'restore.json'))) { $failures += 'watchdog did not restore the lid action' }
else { Write-Host "Watchdog OK: lid action back to $original after the guarded process was killed" }

# --- Idle footprint --------------------------------------------------------------------------------
Start-Sleep -Seconds 10
$proc.Refresh(); $cpu0 = $proc.TotalProcessorTime; $t0 = Get-Date
Start-Sleep -Seconds 30
$proc.Refresh(); $cpuPercent = 100 * ($proc.TotalProcessorTime - $cpu0).TotalMilliseconds / ((Get-Date) - $t0).TotalMilliseconds / [Environment]::ProcessorCount
$ws = [math]::Round($proc.WorkingSet64 / 1MB, 1); $priv = [math]::Round($proc.PrivateMemorySize64 / 1MB, 1)
# "Memory" column of Task Manager = private working set.
$privateWs = try { [math]::Round((Get-Counter "\Process(KeepLid)\Working Set - Private").CounterSamples[0].CookedValue / 1MB, 1) } catch { $null }
Write-Host ("Idle CPU over 30 s: {0:N3} %  |  private working set (Task Manager): {1} MB  |  working set: {2} MB  |  private bytes: {3} MB" -f $cpuPercent, $privateWs, $ws, $priv)
if ($cpuPercent -gt 1) { $failures += "idle CPU too high ($cpuPercent %)" }
if ($privateWs -and $privateWs -gt 50) { $failures += "private working set above 50 MB ($privateWs MB)" }

# --- Log ---------------------------------------------------------------------------------------------
$log = Join-Path $dataDir 'log.txt'
if (Test-Path $log) { Write-Host '----- log.txt -----'; Get-Content $log | Write-Host }
else { $failures += 'log.txt not found in the package LocalCache folder' }
if ((Test-Path $log) -and -not (Select-String -Path $log -Pattern 'Lid action restored' -Quiet)) { $failures += 'log does not mention the restore' }

# --- First launch (no settings.json): the welcome window must appear and the app must stay alive -------
Stop-Process -Id $proc.Id -Force
Start-Sleep -Seconds 1
Remove-Item (Join-Path $dataDir 'settings.json') -ErrorAction SilentlyContinue
Start-Process -FilePath $alias
$proc = $null; $title = ''
for ($i = 0; $i -lt 40 -and -not $title; $i++) {
    Start-Sleep -Milliseconds 500
    $proc = Get-Process -Name KeepLid -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle } | Select-Object -First 1
    if ($proc) { $title = $proc.MainWindowTitle }
}
$all = @(Get-Process -Name KeepLid -ErrorAction SilentlyContinue)
Write-Host "First launch: $($all.Count) KeepLid process(es), main window title: '$title'"
if (-not $all) { $failures += 'KeepLid exited on first launch' }
if ($title -ne 'Welcome to KeepLid') { $failures += "welcome window not shown on first launch (title '$title')" }
Write-Host '----- log.txt (after first launch) -----'; Get-Content $log | Select-Object -Last 15 | Write-Host
$proc = $all | Select-Object -First 1

# --- UI self-test: every window opens without error ------------------------------------------------------
if ($proc) { Stop-Process -Id $proc.Id -Force; Start-Sleep -Seconds 1; $proc = $null }
$selfTest = Start-Process -FilePath $alias -ArgumentList '--ui-selftest' -PassThru
for ($i = 0; $i -lt 60 -and -not (Select-String -Path $log -Pattern 'UI self-test (passed|failed)' -Quiet); $i++) { Start-Sleep -Milliseconds 500 }
Write-Host '----- UI self-test -----'; Select-String -Path $log -Pattern 'UI self-test' | ForEach-Object { Write-Host $_.Line }
if (-not (Select-String -Path $log -Pattern 'UI self-test passed' -Quiet)) { $failures += 'UI self-test did not pass' }
Get-Process -Name KeepLid -ErrorAction SilentlyContinue | Stop-Process -Force

# --- Cleanup -----------------------------------------------------------------------------------------
if ($proc) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
Remove-AppxPackage -Package $pkg.PackageFullName

if ($failures) { $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }; exit 1 }
Write-Host 'Smoke test passed.'
