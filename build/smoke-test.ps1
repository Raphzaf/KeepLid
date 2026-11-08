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

# --- Idle footprint --------------------------------------------------------------------------------
Start-Sleep -Seconds 10
$proc.Refresh(); $cpu0 = $proc.TotalProcessorTime; $t0 = Get-Date
Start-Sleep -Seconds 30
$proc.Refresh(); $cpuPercent = 100 * ($proc.TotalProcessorTime - $cpu0).TotalMilliseconds / ((Get-Date) - $t0).TotalMilliseconds / [Environment]::ProcessorCount
$ws = [math]::Round($proc.WorkingSet64 / 1MB, 1); $priv = [math]::Round($proc.PrivateMemorySize64 / 1MB, 1)
Write-Host ("Idle CPU over 30 s: {0:N3} %  |  working set: {1} MB  |  private bytes: {2} MB" -f $cpuPercent, $ws, $priv)
if ($cpuPercent -gt 1) { $failures += "idle CPU too high ($cpuPercent %)" }
if ($ws -gt 50) { Write-Warning "Working set above the 50 MB target ($ws MB)" }

# --- Log ---------------------------------------------------------------------------------------------
$log = Join-Path $dataDir 'log.txt'
if (Test-Path $log) { Write-Host '----- log.txt -----'; Get-Content $log | Write-Host }
else { $failures += 'log.txt not found in the package LocalCache folder' }
if ((Test-Path $log) -and -not (Select-String -Path $log -Pattern 'Lid action restored' -Quiet)) { $failures += 'log does not mention the restore' }

# --- Cleanup -----------------------------------------------------------------------------------------
Stop-Process -Id $proc.Id -Force
Remove-AppxPackage -Package $pkg.PackageFullName

if ($failures) { $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }; exit 1 }
Write-Host 'Smoke test passed.'
