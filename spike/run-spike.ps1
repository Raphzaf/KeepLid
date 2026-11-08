<#
.SYNOPSIS
  Runs the Phase 0 spike in four configurations and collects the JSON results:
    1. bare exe, current user (admin on CI runners)          -> out\bare-admin.json
    2. bare exe, freshly created STANDARD user (no admin)     -> out\bare-standard.json
    3. MSIX package, current user                             -> out\msix-admin.json
    4. MSIX package, STANDARD user (per-user install + run)   -> out\msix-standard.json

  Intended for a disposable Windows machine (GitHub Actions windows-latest), because it creates a
  local user and trusts a self-signed test certificate machine-wide. Must run elevated.

.PARAMETER PublishDir  Folder containing the self-contained win-x64 publish output of KeepLid.Spike.
#>
param(
    [Parameter(Mandatory = $true)][string]$PublishDir,
    [string]$WorkDir = 'C:\klspike'
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

# --- Locate Windows SDK tools (makeappx, signtool) -------------------------------------------------
$sdkBin = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\10.*' -Directory |
    Sort-Object { [version]$_.Name } -Descending |
    Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } | Select-Object -First 1
$makeappx = Join-Path $sdkBin.FullName 'x64\makeappx.exe'
$signtool = Join-Path $sdkBin.FullName 'x64\signtool.exe'
Write-Host "Windows SDK tools: $($sdkBin.FullName)"

# --- Prepare a world-readable working folder --------------------------------------------------------
if (Test-Path $WorkDir) { Remove-Item $WorkDir -Recurse -Force }
New-Item -ItemType Directory "$WorkDir\bin", "$WorkDir\out", "$WorkDir\layout" | Out-Null
Copy-Item "$PublishDir\*" "$WorkDir\bin" -Recurse
icacls $WorkDir /grant '*S-1-5-32-545:(OI)(CI)M' /T | Out-Null   # BUILTIN\Users: modify

# --- Diagnostics: edition and power scheme security descriptors (who may write / activate) -------
(Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, ProductType | Format-List | Out-String) |
    Set-Content "$WorkDir\out\00-os.txt"
& { 'SCHEME_CURRENT:'; powercfg /getsecuritydescriptor SCHEME_CURRENT
    'ACTION_SETACTIVE:'; powercfg /getsecuritydescriptor ACTION_SETACTIVE
    'ACTION_CREATE:'; powercfg /getsecuritydescriptor ACTION_CREATE
    'ACTION_DEFAULT:'; powercfg /getsecuritydescriptor ACTION_DEFAULT } *>&1 |
    Set-Content "$WorkDir\out\00-security-descriptors.txt"

# --- 1. bare exe, current user ----------------------------------------------------------------------
& "$WorkDir\bin\KeepLid.Spike.exe" --out "$WorkDir\out\bare-admin.json" | Out-Null
Write-Host "bare-admin exit code: $LASTEXITCODE"

# --- Create a standard (non-admin) local user -------------------------------------------------------
$userName = 'klstd'
$plain = 'Kl!' + [guid]::NewGuid().ToString('N').Substring(0, 16) + 'a1'
$secure = ConvertTo-SecureString $plain -AsPlainText -Force
if (Get-LocalUser -Name $userName -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $userName }
New-LocalUser -Name $userName -Password $secure -PasswordNeverExpires | Out-Null
Add-LocalGroupMember -Group (Get-LocalGroup -SID 'S-1-5-32-545') -Member $userName   # Users only
$cred = New-Object System.Management.Automation.PSCredential(".\$userName", $secure)
Set-Service seclogon -StartupType Manual; Start-Service seclogon

function Invoke-AsStandardUser([string]$file, [string]$arguments, [string]$tag) {
    $p = Start-Process -FilePath $file -ArgumentList $arguments -Credential $cred -LoadUserProfile `
        -WorkingDirectory $WorkDir -Wait -PassThru `
        -RedirectStandardOutput "$WorkDir\out\$tag.stdout.txt" -RedirectStandardError "$WorkDir\out\$tag.stderr.txt"
    Write-Host "$tag exit code: $($p.ExitCode)"
}

# --- 2. bare exe, standard user ---------------------------------------------------------------------
Invoke-AsStandardUser "$WorkDir\bin\KeepLid.Spike.exe" "--out $WorkDir\out\bare-standard.json" 'bare-standard'

# --- Build and sign the spike MSIX ------------------------------------------------------------------
Copy-Item "$WorkDir\bin\*" "$WorkDir\layout" -Recurse
Copy-Item "$here\package\AppxManifest.xml" "$WorkDir\layout"
Copy-Item "$here\package\Assets" "$WorkDir\layout\Assets" -Recurse
& $makeappx pack /o /d "$WorkDir\layout" /p "$WorkDir\spike.msix" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx failed ($LASTEXITCODE)" }

$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=KeepLid Test' `
    -CertStoreLocation Cert:\CurrentUser\My -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
$pfxPwd = ConvertTo-SecureString 'spike' -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath "$WorkDir\spike.pfx" -Password $pfxPwd | Out-Null
Export-Certificate -Cert $cert -FilePath "$WorkDir\spike.cer" | Out-Null
Import-Certificate -FilePath "$WorkDir\spike.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
& $signtool sign /fd SHA256 /f "$WorkDir\spike.pfx" /p spike "$WorkDir\spike.msix" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)" }

# --- 3. MSIX, current user --------------------------------------------------------------------------
Add-AppxPackage -Path "$WorkDir\spike.msix"
$pkg = Get-AppxPackage -Name 'KeepLid.Spike'
Write-Host "Installed: $($pkg.PackageFullName)"
$alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\keeplid-spike.exe'
& $alias --out "$WorkDir\out\msix-admin.json" | Out-Null
Write-Host "msix-admin exit code: $LASTEXITCODE"

# --- 4. MSIX, standard user (per-user install, then run through the execution alias) ---------------
$inner = @"
`$ErrorActionPreference = 'Continue'
Add-AppxPackage -Path '$WorkDir\spike.msix'
Get-AppxPackage -Name 'KeepLid.Spike' | Format-List PackageFullName, InstallLocation
# NB: Start-Process -Credential inherits the caller's environment, so `$env:LOCALAPPDATA would point to the
# admin's profile. Resolve the folder from this user's token instead.
`$alias = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Microsoft\WindowsApps\keeplid-spike.exe'
"alias: `$alias"
for (`$i = 0; `$i -lt 20 -and -not (Test-Path `$alias -ErrorAction SilentlyContinue); `$i++) { Start-Sleep -Milliseconds 500 }
& `$alias --out '$WorkDir\out\msix-standard.json'
"exit code: `$LASTEXITCODE"
"@
Set-Content -Path "$WorkDir\inner.ps1" -Value $inner -Encoding UTF8
Invoke-AsStandardUser 'powershell.exe' "-NoProfile -ExecutionPolicy Bypass -File $WorkDir\inner.ps1" 'msix-standard'

# --- Report -----------------------------------------------------------------------------------------
Get-ChildItem "$WorkDir\out" | ForEach-Object {
    Write-Host "===== $($_.Name) ====="
    Get-Content $_.FullName | Write-Host
}

# --- Cleanup (the runner is disposable, but keep things tidy) ---------------------------------------
Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue
Remove-LocalUser -Name $userName -ErrorAction SilentlyContinue
