param([string]$BaseUri = 'http://serverone:5293', [switch]$VerifyRegistration)
$ErrorActionPreference = 'Stop'
function Assert-Condition([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
Assert-Condition ((Invoke-RestMethod "$BaseUri/health/live") -eq 'Healthy') 'Liveness failed.'
Assert-Condition ((Invoke-RestMethod "$BaseUri/health/ready") -eq 'Healthy') 'Readiness failed.'
foreach ($path in @('/', '/Account/Login', '/Account/Register')) {
    Assert-Condition ((Invoke-WebRequest "$BaseUri$path").StatusCode -eq 200) "Public route failed: $path"
}
if (-not $VerifyRegistration) { Write-Output 'Deployment health and public routes verified.'; return }

$runId = [Guid]::NewGuid().ToString('N')
$householdName = "Restart verification $runId"
$ownerEmail = "restart-owner-$runId@example.invalid"
$memberEmail = "restart-member-$runId@example.invalid"
$testPassword = 'Restart!9aA' + [Guid]::NewGuid().ToString('N')
$ownerSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$memberSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
function Submit-Form($Session, [string]$Path, [string]$Handler, [hashtable]$Fields) {
    $html = (Invoke-WebRequest "$BaseUri$Path" -WebSession $Session).Content
    $match = [regex]::Match($html, '<input[^>]*name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    Assert-Condition $match.Success 'Antiforgery token missing.'
    $Fields['__RequestVerificationToken'] = [Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
    $Fields['_handler'] = $Handler
    return Invoke-WebRequest "$BaseUri$Path" -Method Post -WebSession $Session -Body $Fields
}
$envFile = Join-Path (Split-Path $PSScriptRoot -Parent) '.env.unraid'
$line = Get-Content -LiteralPath $envFile | Where-Object { $_ -match '^HOMEHUB_CONNECTION_STRING=' } | Select-Object -First 1
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($line.Substring($line.IndexOf('=') + 1).Trim().Trim("'").Trim('"'))
Assert-Condition ([string]$connection['Database'] -ceq 'HomeHub') 'Unsafe smoke-test database.'
$previousPassword = [Environment]::GetEnvironmentVariable('SQLCMDPASSWORD', 'Process')
function Invoke-TestSql([string]$Query) {
    $output = & sqlcmd -S ([string]$connection['Server']) -U ([string]$connection['User Id']) -C -I -d HomeHub -b -h -1 -W -Q "SET NOCOUNT ON; $Query"
    if ($LASTEXITCODE -ne 0) { throw ('Smoke-test SQL failed: ' + ( -join ' ')) }
    return $output
}
try {
    [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', [string]$connection['Password'], 'Process')
    $response = Submit-Form $ownerSession '/Account/Register' 'register' @{
        'Input.Email'=$ownerEmail; 'Input.Name'='Verification owner'; 'Input.Password'=$testPassword;
        'Input.ConfirmPassword'=$testPassword; 'Input.Choice'='Create'; 'Input.HouseholdName'=$householdName
    }
    Assert-Condition ($response.Content -match [regex]::Escape($householdName)) 'Owner registration failed.'
    $invitationCode = [Guid]::NewGuid().ToString('N').ToUpperInvariant()
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($invitationCode)))
    Invoke-TestSql "DECLARE @h uniqueidentifier = (SELECT Id FROM Households WHERE Name = N'$householdName'); IF @h IS NULL THROW 50000,'Test household missing.',1; INSERT INTO HouseholdInvitations (Id,HouseholdId,CodeHash,ExpiresAtUtc) VALUES (NEWID(),@h,N'$hash',DATEADD(DAY,7,SYSUTCDATETIME()));" | Out-Null
    $joined = Submit-Form $memberSession '/Account/Register' 'register' @{
        'Input.Email'=$memberEmail; 'Input.Name'='Verification member'; 'Input.Password'=$testPassword;
        'Input.ConfirmPassword'=$testPassword; 'Input.Choice'='Join'; 'Input.InvitationCode'=$invitationCode
    }
    Assert-Condition ($joined.Content -match [regex]::Escape($householdName)) 'Invitation registration failed.'
    foreach ($path in @('/Dashboard','/Meals','/MealPlanner','/Household','/Household/Members','/Household/Accounts','/Household/Invitations','/Household/Ownership','/Household/Leave','/Account/Manage')) {
        Assert-Condition ((Invoke-WebRequest "$BaseUri$path" -WebSession $ownerSession).StatusCode -eq 200) "Authenticated route failed: $path"
    }
    $loginSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $login = Submit-Form $loginSession '/Account/Login' 'login' @{ 'Input.Email'=$ownerEmail; 'Input.Password'=$testPassword; 'Input.RememberMe'='false' }
    Assert-Condition ($login.Content -match 'Welcome home') 'Login/dashboard smoke test failed.'
    Write-Output 'Production registration, invitation joining, login, dashboard, and protected routes verified.'
}
finally {
    try {
        # Cleanup only this unique synthetic household and its two synthetic identities.
        Invoke-TestSql "SET XACT_ABORT ON; BEGIN TRANSACTION; DECLARE @h uniqueidentifier=(SELECT Id FROM Households WHERE Name=N'$householdName'); IF @h IS NOT NULL BEGIN IF EXISTS(SELECT 1 FROM HouseholdMemberships m JOIN AspNetUsers u ON m.UserId=u.Id WHERE m.HouseholdId=@h AND u.Email NOT IN(N'$ownerEmail',N'$memberEmail')) THROW 50000,'Unexpected household member; refusing cleanup.',1; DELETE FROM HouseholdInvitations WHERE HouseholdId=@h; DELETE FROM HouseholdMemberships WHERE HouseholdId=@h; DELETE FROM HouseholdMembers WHERE HouseholdId=@h; DELETE FROM Households WHERE Id=@h; END; DELETE FROM AspNetUsers WHERE Email IN(N'$ownerEmail',N'$memberEmail'); COMMIT TRANSACTION;" | Out-Null
        Write-Output 'Temporary verification accounts and household removed.'
    }
    finally { [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $previousPassword, 'Process') }
}
