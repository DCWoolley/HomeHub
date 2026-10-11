# HomeHub

HomeHub helps families organise home and family life. The Restart foundation is being implemented in [GitHub project Restart](https://github.com/users/DCWoolley/projects/1); meal planning and cleaning will be separate modules planned later.

## Architecture

One .NET 10 Blazor Interactive Server application with Identity accounts, EF Core SQL Server, and [Blazor Blueprint](https://blazorblueprintui.com/docs/installation). Use `Modules/<Module>/<UseCase>/` at the project root, one C# file for use-case logic and separate Razor UI. Shared module UI belongs within the module. Components holds the application shell; Data holds EF persistence.

Identity forms use static server rendering for authentication cookies. Ordinary application pages use Interactive Server. The old multi-project application, meals, floors and rooms have been removed.

## Development

Install the .NET 10 SDK. Build with `dotnet build HomeHub.slnx` and run with `dotnet run --project src/HomeHub.Web`. The development connection uses SQL Server LocalDB; override ConnectionStrings:DefaultConnection with user secrets or environment configuration for another SQL Server.

Household management, the protected dashboard, and deployment are being implemented in the remaining Restart issues. Isolated tests cover persistence, registration, rollback, single-use redemption, SSR cookies, and antiforgery. Do not deploy this intermediate foundation to the existing database.

## Deploy to Unraid over SSH

Agents have standing authorisation to deploy verified behavior changes. The target is Docker context serverone (ssh://serverone), identifying ServerOne, Linux, and Unraid OS. Keep credentials in ignored .env.unraid and placeholders only in .env.unraid.example. Never print resolved Compose environment values.

The application uses the existing SQL Server service and the fresh HomeHub database. The old HomeHub database and data were deleted for Restart, as explicitly requested. Startup applies the fresh EF migrations when Database:ApplyMigrations is enabled. The application login owns HomeHub and has migration rights; it does not need server-wide create-database permission.

Preserve port 5293, container homehub, image homehub:local, and named volume homehub_homehub-data-protection mounted at /home/app/.aspnet/DataProtection-Keys. Never delete Docker volumes. Compose explicitly enables LAN HTTP; other configurations retain HTTPS redirection. The container runs as the non-root app user.

```powershell
docker context inspect serverone --format '{{json .Endpoints.docker.Host}}'
docker --context serverone info --format '{{.Name}}|{{.ServerVersion}}|{{.OSType}}|{{.OperatingSystem}}'
dotnet build HomeHub.slnx
$env:HOMEHUB_TEST_LOCALDB = '1' # Windows with SQL Server LocalDB
dotnet test HomeHub.slnx --no-restore -v:m
dotnet ef migrations has-pending-model-changes --project src/HomeHub.Web
docker --context serverone compose --env-file .env.unraid config --quiet
docker --context serverone compose --env-file .env.unraid up --detach --build --wait --wait-timeout 180
docker --context serverone compose --env-file .env.unraid ps
./scripts/Test-Deployment.ps1
```

Liveness is /health/live. Readiness is /health/ready and checks database connectivity and pending migrations. Check both container healthy and readiness Healthy before reporting deployment success.

For a full deployment smoke test, `./scripts/Test-Deployment.ps1 -VerifyRegistration` exercises account creation, invitation joining, login, and authenticated routes with unique synthetic accounts and a synthetic household, then cleans up only those records transactionally. It requires Windows sqlcmd and the configured application SQL login. SQL commands enable quoted identifiers to support filtered indexes.

`scripts/Reset-HomeHubDatabase.ps1` is the guarded one-time legacy reset utility, not an update command. It refuses to run after the InitialRestart migration and checks create-database permission before deleting anything. It targets only the exact HomeHub database. Routine updates preserve all application data.
