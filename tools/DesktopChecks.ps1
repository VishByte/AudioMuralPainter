param([string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'work\desktop-checks'))
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
& (Join-Path $repository 'Source\Build.ps1') -OutputDirectory $OutputDirectory
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$application = Join-Path $OutputDirectory 'AudioMuralPainter.exe'
$helper = Join-Path $OutputDirectory 'DesktopSmoke.exe'
& $compiler /nologo /target:winexe /optimize+ /warnaserror+ "/out:$helper" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$application" "/win32manifest:$repository\Source\Amp.manifest" "$PSScriptRoot\DesktopSmoke.cs"
if ($LASTEXITCODE -ne 0) { throw 'Desktop helper compilation failed.' }
# This explicitly invoked test shows temporary windows and reserves Ctrl+Alt+Shift+F9.
$process = Start-Process -FilePath $helper -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Desktop test exceeded 60 seconds.' }
Get-Content -LiteralPath (Join-Path $OutputDirectory 'desktop-checks.txt')
if ($process.ExitCode -ne 0) { throw 'Desktop tests failed. Check the report and Windows desktop availability.' }
