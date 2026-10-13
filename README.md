# HomeHub

HomeHub helps families organise home and family life. The [Restart project](https://github.com/users/DCWoolley/projects/1) establishes accounts, households, the dashboard shell, and deployment. The [Meal Planner project](https://github.com/users/DCWoolley/projects/2) tracks meal planning, and [Recipes and Recipe Items](https://github.com/users/DCWoolley/projects/3) tracks ingredients and recipes. Cleaning will be separately planned; budgeting and family schedules are future ideas.

## Current scope

- Identity accounts with email/password login, profile settings, password changes, optional authenticator-based two-factor authentication, personal-data download, and guarded account deletion.
- Registration must create a household or redeem an invitation. Account creation, membership, and code redemption are atomic.
- Each account belongs to one household. The creator is its owner. Only the owner can invite or remove account members and transfer ownership.
- Invitations are single-use, expire seven days after creation, and can be revoked. Codes are shown once and stored as hashes.
- Everyone with active household access can manage profiles without logins, such as children. Removing a profile retires it while preserving its stable ID.
- Removing an account member or leaving retires its profile and immediately revokes access while preserving the account. That account can sign in to create/join a household or manage account settings.
- An owner must transfer ownership to another active account member before leaving. Account deletion requires leaving first.
- Household navigation collapses to a compact menu rail on desktop and starts closed on screens up to 760px. On mobile, choosing a link closes the menu. Native disclosure controls support keyboard use and static SSR account settings; Escape closes an open menu and returns focus to its toggle.
- The shared theme uses saturated violet with teal, pink, and amber accents, including Blueprint components.
- Household members can create, view, rename, and delete meals at /Meals. A name is all that is required; recipes are optional. Duplicate names are prevented within each household. Deleting an in-use meal warns that it will also be removed from all plans and deletes its recipe items while keeping reusable ingredients.
- Household members manage named ingredients at /Ingredients and each meal's recipe at /Meals/{mealId}/Recipe. Recipe items reference reusable ingredients with positive decimal quantities: Pasta 0.5 means half a packet. Quantities support four decimal places, up to 99,999,999.9999, and are not rounded to whole packets. Ingredients used in recipes cannot be deleted until removed from those recipes. Shopping-list generation, pack-size conversions, and serving scaling remain future work.
- The planner at /MealPlanner is one reusable Monday–Sunday schedule with multiple meals for breakfast, lunch, and dinner. It has no calendar dates or previous/next weeks and stays the same until edited. Members can select, replace, and remove individual meals from the shared list, or create a meal directly in a planner slot. Matching names reuse an existing meal. Cleaning schedules, budget, calendar, floors, and rooms remain future work.
- Dinner is the only enabled slot by default. Household members can enable or disable breakfast, lunch, and dinner at /MealPlanner/Settings for the whole schedule. Disabled slots keep their selections for when they are enabled again.

Email confirmation is not required. Email delivery is not configured; email changes and password recovery by email are explicitly unavailable. The UI does not claim to send messages. Passkey/external-provider template adapters remain available for future configuration but are not offered in the initial login/navigation; passkeys require HTTPS.

See [Meal Planner](src/HomeHub.Web/Modules/MealPlanner/README.md), [Households](src/HomeHub.Web/Modules/Households/README.md) and [Account](src/HomeHub.Web/Modules/Account/README.md).

## Architecture

One application project, `src/HomeHub.Web`, targets .NET 10 and uses Blazor Interactive Server, ASP.NET Core Identity, EF Core SQL Server, and [Blazor Blueprint](https://blazorblueprintui.com/docs/installation) 4.1.0. Test projects are separate under tests.

Use vertical slices at the project root:

```text
Modules/
  Account/
    Register/
      Register.cs
      Register.razor
    Shared/
  Households/
    ManageProfiles/
      ManageProfiles.cs
      Index.razor
    Household.cs
  Dashboard/
    Index/
      Index.razor
  MealPlanner/
    ManageMeals/
    ManageIngredients/
    ManageRecipe/
    PlanWeek/
    CreateMealFromPlanner/
    ManagePlannerSettings/
    Meal.cs
    Ingredient.cs
Components/    # routing, layouts and reusable application shell
Data/          # Identity user, EF context, migrations and database health
```

Each use case keeps its request, validation, handler, and response in one C# file where needed. Razor files contain UI/form state and can sit beside that file. Module-shared UI belongs within the module; shared entities and technical adapters may live at module level. No mediator or repository layer is used.

Identity pages use static server rendering for cookies and antiforgery. Ordinary application pages use Interactive Server with a Blueprint portal host in the layout. Household HTTP routes and every household data operation check current membership. Open application sessions also recheck on navigation and every five seconds.

## Development and verification

Install the .NET 10 SDK. SQL Server LocalDB is the checked-in development default. Override `ConnectionStrings:DefaultConnection` with user secrets or environment configuration for another SQL Server; never commit credentials.

```powershell
dotnet build HomeHub.slnx
dotnet run --project src/HomeHub.Web
dotnet test HomeHub.slnx --no-restore -v:m
dotnet ef migrations has-pending-model-changes --project src/HomeHub.Web
dotnet ef database update --project src/HomeHub.Web
```

Use EF tools compatible with .NET 10. The fresh migration lives in Data/Migrations. Apply it to a development database before registering users; production applies migrations at startup through Compose. Do not run development EF updates against production merely to verify the model.

Tests use isolated SQLite databases for use-case/HTTP behavior. On Windows with SQL Server LocalDB, set `$env:HOMEHUB_TEST_LOCALDB = '1'` before running tests to include fresh SQL migrations and concurrent invitation/ownership tests. These create and delete only a unique HomeHub_RestartTest_ database and never use the production connection. Without that setting, the SQL test reports a skip.

The landing route is /, login /Account/Login, registration /Account/Register, dashboard /Dashboard, household /Household, and membership recovery /Household/Onboarding. Authentication is separate from layout; signed-in accounts without membership cannot access household routes.

## Deploy to Unraid over SSH

Agents have standing authorisation to deploy verified behavior changes. The target is Docker context serverone (ssh://serverone), identifying ServerOne, Linux, and Unraid OS. Keep credentials in ignored .env.unraid and placeholders only in .env.unraid.example. Never print resolved Compose environment values.

The application uses the existing SQL Server service and the fresh HomeHub database. The old HomeHub database and data were deleted for Restart, as explicitly requested. Startup applies the fresh EF migrations when Database:ApplyMigrations is enabled. The application login owns HomeHub and has migration rights; it does not need server-wide create-database permission.

The project explicitly requires ASP.NET web assets so Docker's project-only restore includes the Blazor runtime before Razor files are copied. The image build checks that `blazor.web.js` is published, and the deployment smoke test verifies the bootstrap URL returns JavaScript and authenticated meal pages contain Interactive Server markers.

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

For a full deployment smoke test, `./scripts/Test-Deployment.ps1 -VerifyRegistration` exercises account creation, invitation joining, login, and authenticated routes, including ingredient/recipe rendering with a 0.5 quantity. It uses unique synthetic accounts, a synthetic household, and synthetic meal/ingredient rows, then cleans up only those records transactionally. It requires Windows sqlcmd and the configured application SQL login. SQL commands enable quoted identifiers to support filtered indexes.

Add `-VerifyBrowser` to that command to exercise live ingredient CRUD and recipe decimal editing, reload, duplicate/precision validation, deletion guards, and mobile layout. This optional check requires Node.js, the Playwright package (on the module path), and installed Microsoft Edge. It uses only the synthetic test household and always cleans up its data. Set `HOMEHUB_BROWSER_ARTIFACTS` to an existing directory to save desktop/mobile screenshots.

`scripts/Reset-HomeHubDatabase.ps1` is the guarded one-time legacy reset utility, not an update command. It refuses to run after the InitialRestart migration and checks create-database permission before deleting anything. It targets only the exact HomeHub database. Routine updates preserve all application data.
