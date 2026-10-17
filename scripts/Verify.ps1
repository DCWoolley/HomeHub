#requires -Version 7.3
[CmdletBinding()]
param([switch]$Sql, [switch]$Browser, [string]$ExpectedRef)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$previousSql = $env:HOMEHUB_TEST_LOCALDB
Push-Location $root
try {
    & "$PSScriptRoot/Test-Workspace.ps1" -ExpectedRef $ExpectedRef
    & "$PSScriptRoot/Test-Repository.ps1"
    if (($Sql -or $Browser) -and -not $IsWindows) { throw 'SQL and browser checks require Windows with SQL Server LocalDB.' }
    if ($Sql) {
        Get-Command SqlLocalDB -ErrorAction Stop | Out-Null
        SqlLocalDB start MSSQLLocalDB
        if ($LASTEXITCODE -ne 0) { throw 'Could not start MSSQLLocalDB.' }
        $env:HOMEHUB_TEST_LOCALDB = '1'
    } else {
        $env:HOMEHUB_TEST_LOCALDB = '0'
        Write-Host 'SQL migration/concurrency checks are excluded. Use -Sql to require them.'
    }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Local .NET tool restore failed.' }
    dotnet restore HomeHub.slnx
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }
    dotnet build HomeHub.slnx --no-restore -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $results = Join-Path $root ('artifacts/verification/' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $results -Force | Out-Null
    dotnet test HomeHub.slnx --no-build --no-restore -v:minimal --logger 'trx;LogFileName=tests.trx' --results-directory $results
    if ($LASTEXITCODE -ne 0) { throw "Tests failed. Results: $results" }
    if ($Sql) {
        [xml]$trx = Get-Content -LiteralPath (Join-Path $results 'tests.trx') -Raw
        $executed = @($trx.TestRun.Results.UnitTestResult)
        if ($executed.Count -eq 0) { throw 'No test results were recorded.' }
        $skipped = @($executed | Where-Object { $_.outcome -ne 'Passed' })
        if ($skipped.Count) { throw "The required SQL lane had skipped or unsuccessful tests: $($skipped.testName -join ', ')" }
        $sqlNames = @(Get-ChildItem -LiteralPath 'tests/HomeHub.Web.Tests' -Filter '*.cs' | Select-String -Pattern '\[LocalDbFact\]' -List)
        if ($sqlNames.Count -eq 0) { throw 'No LocalDB test sources were found; SQL coverage cannot be confirmed.' }
        foreach ($source in $sqlNames) {
            $className = [System.IO.Path]::GetFileNameWithoutExtension($source.Path)
            if (-not ($executed.testName | Where-Object { $_ -like "*$className.*" })) { throw "No SQL test result found for $className." }
        }
        Write-Host 'Required SQL tests ran successfully; no skipped tests.'
    }
    dotnet ef migrations has-pending-model-changes --project src/HomeHub.Web --no-build
    if ($LASTEXITCODE -ne 0) { throw 'EF model differs from migrations.' }
    git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Working-tree whitespace check failed.' }
    git diff --cached --check
    if ($LASTEXITCODE -ne 0) { throw 'Staged whitespace check failed.' }
    # CI checkouts are clean: inspect the committed PR diff as well as local changes.
    if ($env:VERIFY_BASE_REF) {
        $base = $env:VERIFY_BASE_REF
        git diff --check "$base...HEAD"
        if ($LASTEXITCODE -ne 0) { throw 'Committed PR whitespace check failed.' }
    }
    if ($Browser) { & "$PSScriptRoot/Test-LocalBrowser.ps1" -NoBuild }
    Write-Host 'Verification passed.'
} finally {
    $env:HOMEHUB_TEST_LOCALDB = $previousSql
    Pop-Location
}
