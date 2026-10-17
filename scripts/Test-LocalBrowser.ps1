#requires -Version 7.3
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [ValidateRange(1, 180)][int]$StartupTimeoutSeconds = 60,
    [ValidateRange(1, 600)][int]$BrowserTimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'The local browser runner requires Windows with SQL Server LocalDB.' }
$root = Split-Path $PSScriptRoot -Parent
$database = 'HomeHub_RestartTest_' + [Guid]::NewGuid().ToString('N')
$runDirectory = Join-Path $root ('artifacts/browser/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
$hostProcess = $null
$browserProcess = $null
$failure = $null
$cleanupFailures = [System.Collections.Generic.List[string]]::new()

function Start-LoggedProcess([string]$Executable, [string[]]$Arguments, [string]$Name, [hashtable]$Environment = @{}, [string]$InputJson) {
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command $Executable -ErrorAction Stop).Source
    $start.WorkingDirectory = $root
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = [bool]$InputJson
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    foreach ($key in $Environment.Keys) { $start.Environment[$key] = $Environment[$key] }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $start
    $stdout = $null; $stderr = $null; $started = $false
    try {
        $stdout = [System.IO.FileStream]::new((Join-Path $runDirectory "$Name.stdout.log"), 'Create', 'Write', 'ReadWrite', 1)
        $stderr = [System.IO.FileStream]::new((Join-Path $runDirectory "$Name.stderr.log"), 'Create', 'Write', 'ReadWrite', 1)
        if (-not $process.Start()) { throw "Could not start $Name." }
        $started = $true
        $copies = @($process.StandardOutput.BaseStream.CopyToAsync($stdout), $process.StandardError.BaseStream.CopyToAsync($stderr))
        if ($InputJson) { $process.StandardInput.WriteLine($InputJson); $process.StandardInput.Close() }
        return @{ Process = $process; Streams = @($stdout, $stderr); Copies = $copies }
    } catch {
        if ($started -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        if ($stdout) { $stdout.Dispose() }; if ($stderr) { $stderr.Dispose() }; $process.Dispose()
        throw
    }
}
function Stop-LoggedProcess($Handle) {
    if ($null -eq $Handle) { return }
    try {
        if (-not $Handle.Process.HasExited) { $Handle.Process.Kill($true) }
        if (-not $Handle.Process.WaitForExit(10000)) { throw 'Test process did not stop.' }
        foreach ($copy in $Handle.Copies) { [void]$copy.GetAwaiter().GetResult() }
    } finally {
        foreach ($stream in $Handle.Streams) { $stream.Dispose() }
        $Handle.Process.Dispose()
    }
}

Push-Location $root
try {
    & "$PSScriptRoot/Test-Workspace.ps1"
    foreach ($command in @('dotnet', 'node', 'npm', 'SqlLocalDB', 'sqlcmd')) { Get-Command $command -ErrorAction Stop | Out-Null }
    SqlLocalDB start MSSQLLocalDB
    if ($LASTEXITCODE -ne 0) { throw 'Could not start MSSQLLocalDB.' }
    npm ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Pinned browser dependency installation failed.' }
    npx --no-install playwright install chromium
    if ($LASTEXITCODE -ne 0) { throw 'Pinned Chromium installation failed.' }
    if (-not $NoBuild) {
        dotnet build HomeHub.slnx -v:minimal
        if ($LASTEXITCODE -ne 0) { throw 'Build failed before starting the local app.' }
    }
    $app = Join-Path $root 'src/HomeHub.Web/bin/Debug/net10.0/HomeHub.Web.dll'
    if (-not (Test-Path -LiteralPath $app)) { throw 'Debug build output is missing. Run without -NoBuild.' }
    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try { $listener.Start(); $port = $listener.LocalEndpoint.Port } finally { $listener.Stop() }
    $baseUri = "http://127.0.0.1:$port"
    @{ database = $database; baseUri = $baseUri } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runDirectory 'run.json')
    $hostEnvironment = @{
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
        ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=$database;Trusted_Connection=True;Connect Timeout=5"
        Database__ApplyMigrations = 'true'
        Deployment__UseHttpsRedirection = 'false'
        DataProtection__KeysPath = (Join-Path $runDirectory 'keys')
        Logging__LogLevel__Default = 'Warning'
    }
    $hostProcess = Start-LoggedProcess 'dotnet' @($app, '--urls', $baseUri, '--contentRoot', (Join-Path $root 'src/HomeHub.Web')) 'host' $hostEnvironment
    $hostProcess.Process.Id | Set-Content -LiteralPath (Join-Path $runDirectory 'host.pid')
    Write-Host "Local browser run: $baseUri; artifacts: $runDirectory"
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    $ready = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($hostProcess.Process.HasExited) { throw 'Local host exited before readiness. See host.stderr.log and host.stdout.log.' }
        try {
            if ((Invoke-RestMethod "$baseUri/health/ready" -TimeoutSec 2) -eq 'Healthy') { $ready = $true; break }
        } catch { }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Timed out waiting for local database migrations and readiness.' }
    $settings = @{ baseUri = $baseUri; register = $true; database = $database; artifacts = $runDirectory; browserChannel = 'chromium' } | ConvertTo-Json -Compress
    $browserProcess = Start-LoggedProcess 'node' @((Join-Path $PSScriptRoot 'Test-RecipesBrowser.cjs')) 'browser' @{} $settings
    if (-not $browserProcess.Process.WaitForExit($BrowserTimeoutSeconds * 1000)) { throw 'Browser verification timed out.' }
    if ($browserProcess.Process.ExitCode -ne 0) { throw 'Browser verification failed. See browser.stderr.log and screenshots.' }
    Write-Host 'Browser journeys, including database failure/retry, passed.'
} catch { $failure = $_ }
finally {
    foreach ($handle in @($browserProcess, $hostProcess)) {
        try { Stop-LoggedProcess $handle } catch { $cleanupFailures.Add("Process cleanup: $($_.Exception.Message)") }
    }
    try {
        if ($database -cnotmatch '^HomeHub_RestartTest_[a-f0-9]{32}$') { throw 'Unsafe disposable database name.' }
        # Fixed local instance, generated test name only; never read production connection settings.
        sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -I -d master -b -l 5 -t 15 -Q "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END"
        if ($LASTEXITCODE -ne 0) { throw 'Disposable LocalDB database cleanup failed.' }
        if ((Test-Path -LiteralPath $runDirectory) -and $cleanupFailures.Count -eq 0) { 'Database removed; owned processes stopped.' | Set-Content -LiteralPath (Join-Path $runDirectory 'cleanup.txt') }
    } catch { $cleanupFailures.Add("Database cleanup: $($_.Exception.Message)") }
    Pop-Location
    Write-Host "Logs and screenshots: $runDirectory"
    if ($cleanupFailures.Count) { Write-Warning ($cleanupFailures -join '; '); if (-not $failure) { $failure = $cleanupFailures -join '; ' } }
}
if ($failure) { throw $failure }
