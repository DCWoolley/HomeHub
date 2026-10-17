#requires -Version 7.3
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    # Include new files; git diff alone does not inspect untracked additions.
    $files = @(git ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect repository file locations.' }
    $misplaced = @($files | Where-Object { $_ -match '^src/HomeHub.Web/Components/(?:.*/)?Modules/' -and (Test-Path -LiteralPath (Join-Path $root $_)) })
    if ($misplaced.Count) { throw "Feature modules belong at the project root, not under Components: $($misplaced -join ', ')" }
    Write-Host 'Repository layout check passed. Razor/style association is checked by dotnet build.'
} finally { Pop-Location }
