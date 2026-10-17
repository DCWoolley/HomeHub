#requires -Version 7.3
[CmdletBinding()]
param([string]$ExpectedRef)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $branch = git branch --show-current
    if ($LASTEXITCODE -ne 0) { throw 'Run verification from a Git checkout of HomeHub.' }
    $head = git rev-parse --short HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve checkout HEAD.' }
    Write-Host "Workspace: $root"
    Write-Host "Baseline: $(if ($branch) { $branch } else { 'detached HEAD' }) ($head)"
    if ($ExpectedRef) {
        git merge-base --is-ancestor $ExpectedRef HEAD
        if ($LASTEXITCODE -ne 0) { throw "HEAD does not contain expected baseline '$ExpectedRef'. Choose the intended development baseline before changing files." }
    }
    foreach ($required in @('HomeHub.slnx', 'AGENTS.md', 'src/HomeHub.Web/Modules/MealPlanner/Meal.cs')) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $required) -PathType Leaf)) {
            throw "Missing $required. This checkout may contain only the starter application; select the intended development baseline."
        }
    }
    $changes = @(git status --short)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect working-tree changes.' }
    if ($changes.Count) { Write-Host 'Preserve these existing changes:'; $changes | ForEach-Object { Write-Host $_ } }
} finally { Pop-Location }
