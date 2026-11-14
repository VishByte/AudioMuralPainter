param([string]$OutputDirectory = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler is required.' }
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$destination = Join-Path (Resolve-Path -LiteralPath $OutputDirectory).Path 'AudioMuralPainter.exe'
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
$arguments = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/warnaserror+', '/utf8output',
    "/out:$destination", "/win32manifest:$PSScriptRoot\Amp.manifest", "/win32icon:$PSScriptRoot\Amp.ico",
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Xml.dll'
)
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built $destination"
