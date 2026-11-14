$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$paths = @(git -C $repository -c core.quotepath=false ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Privacy checking requires a Git checkout.' }
$patterns = @(
    '(?i)[A-Z]:[\\/]+Users[\\/]+(?!Public[\\/]|Default[\\/])[^\\/\s"<>]+',
    '(?i)(gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{50,}|sk-[A-Za-z0-9_-]{20,}|AKIA[0-9A-Z]{16})',
    '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
    '(?i)[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}'
)
$failures = @()
foreach ($relative in $paths) {
    if (-not (Test-Path -LiteralPath (Join-Path $repository $relative) -PathType Leaf)) { continue }
    if ($relative -match '(?i)(^|/)(Amp\.settings[^/]*\.xml|Presets|\.env(?:\..*)?)($|/)|\.(exe|dll|pdb|zip|pfx|pem|key)$') {
        $failures += "$relative (private or generated file)"
        continue
    }
    if ([IO.Path]::GetExtension($relative) -eq '.ico') { continue }
    $content = [IO.File]::ReadAllText((Join-Path $repository $relative))
    foreach ($pattern in $patterns) {
        if ($content -match $pattern) { $failures += "$relative (sensitive-data pattern)"; break }
    }
}
if ($failures.Count) { throw "Privacy review required for: $($failures -join ', '). Values are intentionally omitted." }
Write-Output "Privacy pattern check passed for $($paths.Count) checkout files. This is a guardrail, not an exhaustive secret detector."
