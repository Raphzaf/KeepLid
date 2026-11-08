<#
.SYNOPSIS
  Builds the KeepLid MSIX packages for the Microsoft Store from the command line (no Visual Studio needed).

.DESCRIPTION
  1. runs the unit tests (unless -SkipTests);
  2. publishes KeepLid.App self-contained for each architecture (x64, arm64);
  3. creates one .msix per architecture (makepri + makeappx), from src\KeepLid.Package\Package.appxmanifest;
  4. bundles them into a .msixbundle and wraps it into a .msixupload (the file to upload to Partner Center);
  5. optionally signs the .msix / .msixbundle with a certificate for local testing (-CertificatePath).

  Requirements: Windows 10/11, .NET 8 SDK, Windows 10/11 SDK (makeappx.exe, makepri.exe, signtool.exe).

.EXAMPLE
  # Store build (identity values from Partner Center > Product identity):
  .\build\package.ps1 -Version 1.0.0.0 -IdentityName 12345KeepLid.KeepLid `
      -Publisher "CN=00000000-0000-0000-0000-000000000000" -PublisherDisplayName "Your publisher name"

.EXAMPLE
  # Local test build, signed with a self-signed certificate (see docs/BUILD_ET_PACKAGING.md):
  .\build\package.ps1 -CertificatePath .\KeepLidTest.pfx -CertificatePassword (Read-Host -AsSecureString)
#>
[CmdletBinding()]
param(
    [string]$Version = '1.0.0.0',
    [string[]]$Architectures = @('x64', 'arm64'),
    [string]$IdentityName = 'KeepLid',
    [string]$Publisher = 'CN=KeepLid Test',
    [string]$PublisherDisplayName = 'KeepLid',
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\artifacts'),
    [string]$CertificatePath,
    [SecureString]$CertificatePassword,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$packageProject = Join-Path $root 'src\KeepLid.Package'
$work = Join-Path $root 'out\package'
New-Item -ItemType Directory -Force $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force $work | Out-Null

if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw "Version must look like 1.2.3.0 (the Store requires the last number to be 0)." }

# --- Windows SDK tools ----------------------------------------------------------------------------
$sdkBin = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\10.*' -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $sdkBin) { throw 'Windows SDK not found (install the Windows 10/11 SDK).' }
$makeappx = Join-Path $sdkBin.FullName 'x64\makeappx.exe'
$makepri = Join-Path $sdkBin.FullName 'x64\makepri.exe'
$signtool = Join-Path $sdkBin.FullName 'x64\signtool.exe'
Write-Host "Windows SDK: $($sdkBin.Name)"

function Invoke-Tool([string]$exe, [string[]]$arguments) {
    & $exe @arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$(Split-Path $exe -Leaf) failed with exit code $LASTEXITCODE" }
}

# --- 1. Tests ---------------------------------------------------------------------------------------
if (-not $SkipTests) {
    Invoke-Tool 'dotnet' @('test', (Join-Path $root 'tests\KeepLid.Core.Tests'), '-c', 'Release')
}

# --- 2-3. One .msix per architecture ----------------------------------------------------------------
$appVersion = ($Version -split '\.')[0..2] -join '.'
$msixFiles = @()
foreach ($arch in $Architectures) {
    $layout = Join-Path $work "layout-$arch"
    # Same layout as the Visual Studio packaging project: the app lives in "KeepLid.App\".
    $appDir = Join-Path $layout 'KeepLid.App'
    Invoke-Tool 'dotnet' @('publish', (Join-Path $root 'src\KeepLid.App\KeepLid.App.csproj'), '-c', 'Release',
        '-r', "win-$arch", '--self-contained', 'true', "-p:Version=$appVersion", '-o', $appDir)
    Get-ChildItem $appDir -Filter *.pdb -Recurse | Remove-Item

    Copy-Item (Join-Path $packageProject 'Images') (Join-Path $layout 'Images') -Recurse

    # Manifest: resolve the Visual Studio tokens and the identity.
    [xml]$manifest = Get-Content (Join-Path $packageProject 'Package.appxmanifest') -Raw
    $manifest.Package.Identity.Name = $IdentityName
    $manifest.Package.Identity.Publisher = $Publisher
    $manifest.Package.Identity.Version = $Version
    $manifest.Package.Identity.SetAttribute('ProcessorArchitecture', $arch)
    $manifest.GetElementsByTagName('PublisherDisplayName')[0].InnerText = $PublisherDisplayName
    $text = $manifest.OuterXml.Replace('$targetnametoken$', 'KeepLid.App\KeepLid').Replace('$targetentrypoint$', 'Windows.FullTrustApplication')
    $manifestPath = Join-Path $layout 'AppxManifest.xml'
    [System.IO.File]::WriteAllText($manifestPath, $text, (New-Object System.Text.UTF8Encoding($false)))

    # resources.pri: resolves Images\*.scale-*.png / targetsize-* and declares en-US + fr-FR.
    # Only the Images folder is indexed (the app's own strings live in .NET satellite assemblies).
    $priRoot = Join-Path $work "pri-$arch"
    New-Item -ItemType Directory -Force $priRoot | Out-Null
    Copy-Item (Join-Path $packageProject 'Images') (Join-Path $priRoot 'Images') -Recurse
    $priConfig = Join-Path $work "priconfig-$arch.xml"
    Invoke-Tool $makepri @('createconfig', '/cf', $priConfig, '/dq', 'en-US_fr-FR', '/pv', '10.0.0', '/o')
    Invoke-Tool $makepri @('new', '/pr', $priRoot, '/cf', $priConfig, '/mn', $manifestPath, '/of', (Join-Path $layout 'resources.pri'), '/o')

    $msix = Join-Path $work "KeepLid_$($Version)_$arch.msix"
    Invoke-Tool $makeappx @('pack', '/o', '/h', 'SHA256', '/d', $layout, '/p', $msix)
    $msixFiles += $msix
}

# --- 4. Bundle and .msixupload --------------------------------------------------------------------
$bundleInput = Join-Path $work 'bundle-input'
New-Item -ItemType Directory -Force $bundleInput | Out-Null
$msixFiles | ForEach-Object { Copy-Item $_ $bundleInput }
$archTag = ($Architectures -join '_')
$bundle = Join-Path $OutputDir "KeepLid_$($Version)_$archTag.msixbundle"
Invoke-Tool $makeappx @('bundle', '/o', '/bv', $Version, '/d', $bundleInput, '/p', $bundle)

$upload = Join-Path $OutputDir "KeepLid_$($Version)_$archTag.msixupload"
if (Test-Path $upload) { Remove-Item $upload }
$zip = "$upload.zip"
Compress-Archive -Path $bundle -DestinationPath $zip -Force
Move-Item $zip $upload
$msixFiles | ForEach-Object { Copy-Item $_ $OutputDir }

# --- 5. Optional signing (local tests only; the Store signs the uploaded package itself) ------------
if ($CertificatePath) {
    $plain = if ($CertificatePassword) { [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($CertificatePassword)) } else { '' }
    foreach ($file in @($bundle) + ($msixFiles | ForEach-Object { Join-Path $OutputDir (Split-Path $_ -Leaf) })) {
        Invoke-Tool $signtool @('sign', '/fd', 'SHA256', '/f', $CertificatePath, '/p', $plain, $file)
    }
    Write-Host 'Signed for local testing. Do NOT upload signed test packages to the Store: use the .msixupload.'
}

Write-Host ''
Write-Host 'Done:'
Get-ChildItem $OutputDir -File | Where-Object { $_.Name -like "KeepLid_$Version*" } |
    ForEach-Object { '  {0,-60} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) } | Write-Host
