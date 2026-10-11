param(
    [Parameter(Mandatory)][ValidateSet('HomeHub')][string]$ExpectedDatabaseName,
    [Parameter(Mandatory)][switch]$ResetForRestart
)
$ErrorActionPreference = 'Stop'
if (-not $ResetForRestart) { throw 'Explicit Restart reset switch is required.' }
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$envPath = Join-Path $repositoryRoot '.env.unraid'
$line = Get-Content -LiteralPath $envPath | Where-Object { $_ -match '^HOMEHUB_CONNECTION_STRING=' } | Select-Object -First 1
if (-not $line) { throw 'HomeHub connection setting is missing.' }
$value = $line.Substring($line.IndexOf('=') + 1).Trim()
if (($value.StartsWith("'") -and $value.EndsWith("'")) -or ($value.StartsWith('"') -and $value.EndsWith('"'))) {
    $value = $value.Substring(1, $value.Length - 2)
}
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($value)
function Get-ConnectionValue([string[]]$Names) {
    foreach ($key in $Names) { if ($connection.ContainsKey($key)) { return [string]$connection[$key] } }
    return $null
}
$database = Get-ConnectionValue @('Database', 'Initial Catalog')
if ($database -cne $ExpectedDatabaseName -or $database -cne 'HomeHub') { throw 'Configured database does not exactly match the authorised HomeHub target.' }
$server = Get-ConnectionValue @('Server', 'Data Source')
$username = Get-ConnectionValue @('User Id', 'UID')
$password = Get-ConnectionValue @('Password', 'PWD')
if (-not $server -or -not $username -or -not $password) { throw 'SQL login configuration is incomplete.' }
$previousPassword = [Environment]::GetEnvironmentVariable('SQLCMDPASSWORD', 'Process')
try {
    [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $password, 'Process')
    # This fixed database name is intentional. Never generalize the destructive statement.
    $query = @'
IF DB_ID(N'HomeHub') IS NULL THROW 50000, 'Expected old HomeHub database was not found.', 1;
IF OBJECT_ID(N'HomeHub.dbo.__EFMigrationsHistory') IS NULL THROW 50000, 'Expected migration history was not found.', 1;
IF EXISTS (SELECT 1 FROM [HomeHub].dbo.__EFMigrationsHistory WHERE MigrationId LIKE '%InitialRestart')
    THROW 50000, 'Restart has already been applied; refusing to delete the new database.', 1;
IF HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE') <> 1
    THROW 50000, 'This login cannot create the replacement database; use an administrator before resetting.', 1;
BEGIN TRY
ALTER DATABASE [HomeHub] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [HomeHub];
CREATE DATABASE [HomeHub];
SELECT N'HomeHub database reset complete.' AS Result;
END TRY
BEGIN CATCH
    THROW;
END CATCH;
'@
    & sqlcmd -S $server -U $username -C -d master -b -Q $query
    if ($LASTEXITCODE -ne 0) { throw 'HomeHub database reset failed.' }
}
finally { [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $previousPassword, 'Process') }
