$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$destination = Join-Path (Split-Path -Parent $PSScriptRoot) 'AudioMuralPainter.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /utf8output "/out:$destination" "/win32manifest:$PSScriptRoot\Amp.manifest" "/win32icon:$PSScriptRoot\Amp.ico" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll "$PSScriptRoot\Program.cs" "$PSScriptRoot\Audio.cs" "$PSScriptRoot\Preferences.cs" "$PSScriptRoot\Renderer.cs" "$PSScriptRoot\RenderEngine.cs" "$PSScriptRoot\LayeredSurface.cs" "$PSScriptRoot\Settings.cs" "$PSScriptRoot\Tests.cs" "$PSScriptRoot\Benchmark.cs" "$PSScriptRoot\NativeDrawing.cs" "$PSScriptRoot\AppInfo.cs" "$PSScriptRoot\Hotkeys.cs" "$PSScriptRoot\DesktopIntegration.cs" "$PSScriptRoot\HotkeyDialog.cs" "$PSScriptRoot\InputTestTarget.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built $destination"
