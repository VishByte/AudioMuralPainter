param([string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'work\dist'))
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $repository ('work\package-' + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $stage 'AudioMuralPainter'
& (Join-Path $PSScriptRoot 'Verify.ps1') -OutputDirectory (Join-Path $stage 'checks')
$null = New-Item -ItemType Directory -Path $package -Force
Copy-Item -LiteralPath (Join-Path $stage 'checks\AudioMuralPainter.exe') -Destination $package
Copy-Item -LiteralPath (Join-Path $stage 'checks\checks.txt') -Destination (Join-Path $package 'Checks.txt')
# Use the same Git-visible source set checked by CheckPrivacy; ignored scratch
# files must not enter a release merely because they sit inside Source.
$sourceFiles = @(git -C $repository -c core.quotepath=false ls-files --cached --others --exclude-standard -- Source)
if ($LASTEXITCODE -ne 0 -or $sourceFiles.Count -eq 0) { throw 'Cannot enumerate the public source files.' }
$null = New-Item -ItemType Directory -Path (Join-Path $package 'Source')
foreach ($relative in $sourceFiles) {
    $sourcePath = Join-Path $repository $relative
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { continue }
    if ($relative -notmatch '^Source/[^/]+\.(cs|ps1|ico|manifest)$') { throw "Unexpected source file: $relative" }
    Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $package 'Source')
}
foreach ($name in @('README.md','SECURITY.md','CONTRIBUTING.md','AUDIT.md','Performance.md','Verification.md')) {
    Copy-Item -LiteralPath (Join-Path $repository $name) -Destination $package
}
# Construct a package from public source and named documentation; never zip the working directory.
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$archive = Join-Path (Resolve-Path -LiteralPath $OutputDirectory).Path 'AudioMuralPainter-Windows.zip'
if (Test-Path -LiteralPath $archive) { throw 'The destination ZIP already exists. Choose a new output directory.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($package,$archive,[IO.Compression.CompressionLevel]::Optimal,$true)
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -match '(?i)Amp\.settings|/Presets/|/\.git/|\.pdb$|\.env') { throw 'A private or generated file entered the archive.' }
    }
} finally { $zip.Dispose() }
Get-FileHash -LiteralPath $archive -Algorithm SHA256
