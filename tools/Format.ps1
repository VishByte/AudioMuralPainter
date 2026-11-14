param([switch]$Check, [string]$RoslynDirectory = $PSHOME)

$ErrorActionPreference = 'Stop'
# PowerShell 7 bundles Roslyn. This is a development tool, not an app dependency.
Add-Type -Path (Join-Path $RoslynDirectory 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $RoslynDirectory 'Microsoft.CodeAnalysis.CSharp.dll')
$sourceDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'Source'
$files = @(Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.cs') + @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs')
$changed = @()
foreach ($file in ($files | Sort-Object FullName)) {
    $original = [IO.File]::ReadAllText($file.FullName)
    $syntax = [Microsoft.CodeAnalysis.CSharp.SyntaxFactory]::ParseCompilationUnit($original, 0, $null)
    if ($syntax.ContainsDiagnostics) { throw "Resolve syntax diagnostics before formatting $($file.Name)." }
    $normalized = [Microsoft.CodeAnalysis.SyntaxNodeExtensions]::NormalizeWhitespace[Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax]($syntax, '    ', "`r`n", $false)
    $formatted = $normalized.ToFullString().TrimEnd() + "`r`n"
    $parsed = [Microsoft.CodeAnalysis.CSharp.SyntaxFactory]::ParseCompilationUnit($formatted, 0, $null)
    $beforeTokens = @($syntax.DescendantTokens())
    $afterTokens = @($parsed.DescendantTokens())
    if ($parsed.ContainsDiagnostics -or $beforeTokens.Count -ne $afterTokens.Count) { throw "Formatting changed the syntax of $($file.Name)." }
    for ($i = 0; $i -lt $beforeTokens.Count; $i++) {
        if ($beforeTokens[$i].RawKind -ne $afterTokens[$i].RawKind -or $beforeTokens[$i].Text -cne $afterTokens[$i].Text) {
            throw "Formatting changed a code token in $($file.Name)."
        }
    }
    if ($original -cne $formatted) {
        $changed += $file.Name
        if (-not $Check) { [IO.File]::WriteAllText($file.FullName, $formatted, [Text.UTF8Encoding]::new($false)) }
    }
}
if ($Check -and $changed.Count) { throw "Run tools/Format.ps1 for: $($changed -join ', ')" }
Write-Output "Formatting checked; $($changed.Count) file(s) required changes."
