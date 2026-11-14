param([string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'work\verify'))
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'CheckPrivacy.ps1')
& (Join-Path $repository 'Source\Build.ps1') -OutputDirectory $OutputDirectory
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$executable = Join-Path $OutputDirectory 'AudioMuralPainter.exe'
$report = Join-Path $OutputDirectory 'checks.txt'
$process = Start-Process -FilePath $executable -ArgumentList ('--self-test "{0}"' -f $report) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Self-tests exceeded 60 seconds.' }
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw "Self-tests failed. Inspect $report" }
$lines = @(Get-Content -LiteralPath $report)
if ($lines -match '^FAIL') { throw "Self-test report contains a failure: $report" }
Write-Output "PASS: $(($lines -match '^PASS ').Count) checks."
$invalidReportArguments = '--benchmark "{0}" "{1}"' -f $OutputDirectory, (Join-Path $OutputDirectory 'missing.xml')
foreach ($case in @(@('--unknown-audit-command', 2), @($invalidReportArguments, 1))) {
    $process = Start-Process -FilePath $executable -ArgumentList $case[0] -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(10000)) { $process.Kill(); throw 'Invalid diagnostic command did not exit.' }
    if ($process.ExitCode -ne $case[1]) { throw "Unexpected diagnostic exit code $($process.ExitCode)." }
}
Write-Output 'PASS: invalid diagnostic commands exit cleanly.'
