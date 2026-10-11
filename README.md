# HomeHub

HomeHub helps families organise home and family life. The Restart foundation is being implemented in [GitHub project Restart](https://github.com/users/DCWoolley/projects/1); meal planning and cleaning will be separate modules planned later.

## Architecture

One .NET 10 Blazor Interactive Server application with Identity accounts, EF Core SQL Server, and [Blazor Blueprint](https://blazorblueprintui.com/docs/installation). Use `Modules/<Module>/<UseCase>/` at the project root, one C# file for use-case logic and separate Razor UI. Shared module UI belongs within the module. Components holds the application shell; Data holds EF persistence.

Identity forms use static server rendering for authentication cookies. Ordinary application pages use Interactive Server. The old multi-project application, meals, floors and rooms have been removed.

## Development

Install the .NET 10 SDK. Build with `dotnet build HomeHub.slnx` and run with `dotnet run --project src/HomeHub.Web`. The development connection uses SQL Server LocalDB; override ConnectionStrings:DefaultConnection with user secrets or environment configuration for another SQL Server.

Household registration, household management, protected dashboard, deployment, and focused tests are being implemented in the remaining Restart issues. Do not deploy this intermediate foundation to the existing database.

## Deploy to Unraid over SSH

Deployment setup is retained while Restart is implemented. The completed application will use Docker context `serverone` (`ssh://serverone`), port 5293, the existing SQL Server service, and the named volume `homehub_homehub-data-protection`. Secrets stay in ignored `.env.unraid`; never print resolved Compose environment values. The old HomeHub database/data will be deleted as explicitly requested, after verification and exact target checks. Do not delete Docker volumes.
