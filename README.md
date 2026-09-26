# HomeHub

## Architecture

The solution uses the following Clean Architecture project boundaries:

| Project | Responsibility | Direct project references |
| --- | --- | --- |
| HomeHub.Domain | Entities, value objects, and domain rules | None |
| HomeHub.Application | Use cases and interfaces for external services | Domain |
| HomeHub.Infrastructure | Persistence and implementations of application interfaces | Application, Domain |
| HomeHub.Web | UI, HTTP endpoints, and dependency injection composition root | Application, Infrastructure |

Dependencies point inward: Domain is independent, and Application does not reference Infrastructure or Web. Web references Infrastructure to wire implementations into dependency injection.

Infrastructure owns Identity persistence, its user type, migrations, and email delivery implementation. Web hosts the account UI and authentication HTTP integration under `Features/Account`. Domain and Application have no Identity or Entity Framework dependencies.

Build the solution with `dotnet build HomeHub.slnx`.

Run integration tests with `dotnet test HomeHub.slnx --no-restore -v:m`. Tests cover deployment health, HTTP account routes, authentication requirements, and data-protection persistence. They do not require the production database.

## Feature organization

Use vertical slices, grouping code by feature and use case. Web features live at `src/HomeHub.Web/Features/<Feature>/<UseCase>/`, alongside `Components`, never inside it. Application use cases live at `src/HomeHub.Application/Features/<Feature>/<UseCase>/` and preserve the dependency boundaries above.

The minimal landing page at `/` lives in `Features/Home/Index.razor`. Template demo content has been removed. Identity pages live in `Features/Account` by use case; error/not-found pages remain under `Components`.

## Identity development

Run the app with `dotnet run --project src/HomeHub.Web`. The host supplies `ConnectionStrings:DefaultConnection` and retains the existing user-secrets identifier. The checked-in connection string uses SQL Server LocalDB.

EF commands target Infrastructure with Web as the startup project, for example:

```shell
dotnet ef migrations has-pending-model-changes --project src/HomeHub.Infrastructure --startup-project src/HomeHub.Web
dotnet ef migrations add <MigrationName> --project src/HomeHub.Infrastructure --startup-project src/HomeHub.Web --output-dir Persistence/Migrations
```

Migration history remains in Infrastructure. See the [account feature documentation](src/HomeHub.Web/Features/Account/README.md) for authentication boundaries and verification guidance.

## Deploy to Unraid over SSH

Production deployment requires an explicit request. Install the Docker CLI and Compose, and configure SSH key access through the `serverone` SSH alias with Docker socket access. Keep host, username, SSH port, and key settings in SSH configuration, outside the repository.

Confirm the target:

```powershell
docker context inspect serverone --format '{{json .Endpoints.docker.Host}}'
docker --context serverone info --format '{{.Name}}|{{.ServerVersion}}|{{.OSType}}|{{.OperatingSystem}}'
```

The endpoint must be `ssh://serverone`, identifying `ServerOne`, Linux, and Unraid OS. If the context is missing, create it with `docker context create serverone --docker "host=ssh://serverone" --description "Unraid over SSH"`.

For initial configuration only, copy [.env.unraid.example](.env.unraid.example) to `.env.unraid` and replace the connection-string placeholders. Do not overwrite an existing configured file. The file is ignored by Git and excluded from Docker builds; use single quotes around values containing `$`.

HomeHub uses the existing SQL Server database specified by `HOMEHUB_CONNECTION_STRING`. The server must be reachable from the container; `localhost` refers to the container itself. SQL data remains on the existing database server. Startup applies pending EF migrations when `Database:ApplyMigrations` is enabled, as it is in Compose. The database login needs permission for those migrations. Back up the database before deploying schema changes.

| Setting | HomeHub value |
| --- | --- |
| Compose project / service | `homehub` / `app` |
| Container / image | `homehub` / `homehub:local` |
| LAN port / container port | `5293` / `8080` |
| Environment / restart policy | `Production` / `unless-stopped` |
| Authentication-key volume | `homehub_homehub-data-protection` |
| Key mount | `/home/app/.aspnet/DataProtection-Keys` |
| Liveness / readiness | `/health/live` / `/health/ready` |

[Dockerfile](Dockerfile) publishes in a .NET 10 SDK stage and runs the ASP.NET runtime as the non-root `app` user. [compose.yaml](compose.yaml) preserves the existing HomeHub key volume and resource names. [.dockerignore](.dockerignore) excludes environment files, Git metadata, and local build output. No image registry is required.

Compose disables HTTPS redirection and HSTS for LAN HTTP. Other configurations retain HTTPS redirection by default. Passkeys require a secure browser context, so LAN HTTP does not support passkey flows. Account email delivery is still the template no-op implementation; see the account documentation.

Check the intended revision and working tree before deploying. The Docker build includes uncommitted source changes; deploy them only intentionally. Check existing container ports before changing `HOMEHUB_PORT`.

```powershell
git status --short
git log -1 --oneline
dotnet build HomeHub.slnx
dotnet test HomeHub.slnx --no-restore -v:m
dotnet ef migrations has-pending-model-changes --project src/HomeHub.Infrastructure --startup-project src/HomeHub.Web
docker --context serverone compose --env-file .env.unraid config --quiet
docker --context serverone compose --env-file .env.unraid up --detach --build --wait --wait-timeout 180
docker --context serverone compose --env-file .env.unraid ps
Invoke-RestMethod -Uri 'http://serverone:5293/health/ready' -TimeoutSec 15
```

The container health check verifies HTTP liveness. Readiness additionally requires database connectivity and no pending migrations; failures return HTTP 503 without diagnostic details. Wait for both container `healthy` and readiness `Healthy` before reporting success. The same deployment command updates the application while retaining named volumes.

```powershell
# Recent logs (use --follow to stream).
docker --context serverone compose --env-file .env.unraid logs --tail 100 app

# Stop and remove containers while preserving named volumes.
docker --context serverone compose --env-file .env.unraid down
```

Never add `--volumes` unless persistent-data deletion is explicitly intended. Deleting the key volume signs users out and invalidates protected authentication data.

## Working in this repository

Read [AGENTS.md](AGENTS.md) for clean code conventions, feature placement, verification, and documentation maintenance. Agents must update relevant documentation as part of each change when behavior, structure, or conventions change, without requiring a separate reminder.
