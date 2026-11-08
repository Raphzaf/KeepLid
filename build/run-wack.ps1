<#
.SYNOPSIS
  Runs the Windows App Certification Kit (WACK) on a signed, trusted .msix and prints every test that did
  not pass. Must run elevated. Exit code 1 if the overall result is FAIL.
  Manual equivalent: Start menu › "Windows App Cert Kit" › Validate Store App › select the .msix.
#>
param(
    [Parameter(Mandatory = $true)][string]$MsixPath,
    [string]$ReportPath = (Join-Path (Split-Path $MsixPath) 'wack-report.xml')
)
$ErrorActionPreference = 'Stop'
$ReportPath = [System.IO.Path]::GetFullPath($ReportPath)   # appcert.exe requires an absolute path
if (Test-Path $ReportPath) { Remove-Item $ReportPath }
$appcert = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe' -ErrorAction SilentlyContinue
if (-not $appcert) {
    Write-Warning 'Windows App Certification Kit not installed (Windows SDK component). Skipping.'
    exit 0
}
& $appcert.FullName reset | Out-Host
& $appcert.FullName test -appxpackagepath (Resolve-Path $MsixPath).Path -reportoutputpath $ReportPath | Out-Host

[xml]$report = Get-Content $ReportPath -Raw
$overall = $report.REPORT.OVERALL_RESULT
Write-Host "WACK overall result: $overall"
foreach ($req in $report.REPORT.REQUIREMENTS.REQUIREMENT) {
    foreach ($test in $req.TEST) {
        $result = $test.RESULT.'#cdata-section'
        if ($result -ne 'PASS') {
            $optional = if ($test.OPTIONAL -eq 'TRUE' -or $req.OPTIONAL -eq 'TRUE') { ' (optional)' } else { '' }
            Write-Host ("[{0}{3}] {1} / {2}" -f $result, $req.TITLE, $test.NAME, $optional)
            foreach ($m in $test.MESSAGES.MESSAGE) { if ($m.TEXT) { Write-Host "    - $($m.TEXT)" } }
        }
    }
}
if ($overall -eq 'FAIL') { exit 1 }
