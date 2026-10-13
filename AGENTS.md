# HomeHub agent instructions

These instructions apply to the entire repository.

## Architecture and organisation

- HomeHub is a single .NET 10 Blazor Interactive Server application: `src/HomeHub.Web`, using ASP.NET Core Identity, EF Core SQL Server, and Blazor Blueprint.
- Use vertical slices at the project root: `Modules/<Module>/<UseCase>/`. Never place modules under Components.
- Keep a use case's request, validation, handler, and response in one C# file where needed. Razor UI may be separate in that folder. UI shared by a module belongs within that module's Shared folder.
- Modules may have shared entities and technical integration at module level. `Data` owns shared EF context and migrations. Identity framework adapters live in Modules/Account.
- Components is reserved for routing, layouts, error/not-found pages, and genuinely reusable UI.
- Use the shared ResponsiveMenu native disclosure for household and account navigation. Keep it usable with static SSR; wwwroot/navigation.js supplies viewport defaults, mobile link closing, and Escape focus handling across interactive navigation.
- Prefer small cohesive types, direct EF use in use cases, asynchronous I/O, cancellation, and DI. No mediator, generic repositories, or speculative abstractions.
- Razor imports are folder-scoped; project-level _Imports.razor applies to both Components and Modules.
- Account forms use static SSR for cookies and antiforgery. Email delivery is not configured: never claim a no-op email was sent or expose confirmation tokens. Other pages use Interactive Server. Keep Blueprint overlays within an interactive provider boundary.
- Revalidate current membership on every household operation. Accounts have at most one household; only the owner invites/removes account members or transfers ownership. Everyone can manage profiles without logins.
- Registration must atomically create an account with household membership. Invitations are revocable, single-use, and expire after seven days.
- Keep credentials and sensitive personal data out of source and logs. Do not log invitation codes.
- Ingredients are reusable household-owned names; recipe items reference an existing meal and ingredient in that household. Quantities are positive decimals with at most four decimal places (SQL decimal(12,4)), representing packets/items. Reject excess precision rather than rounding. Block deletion of referenced ingredients; meal deletion cascades recipe items while preserving ingredients. Shopping-list generation remains future work.

## Verification

- Build from the root: `dotnet build HomeHub.slnx`.
- On Windows with SQL Server LocalDB, set HOMEHUB_TEST_LOCALDB=1 to include isolated fresh-migration and concurrency tests; never target production for tests.
- Run focused meaningful tests under tests: `dotnet test HomeHub.slnx --no-restore -v:m`.
- For persistence changes: `dotnet ef migrations has-pending-model-changes --project src/HomeHub.Web`. Do not apply production migrations just to verify.
- Verify affected Razor routes, rendering, and authentication when possible.
- Check documentation paths/links and `git diff --check`. Report blocked checks honestly.

## Deployment

- Deploy behavior changes to production after required verification, unless the user asks otherwise.
- Follow [README deployment instructions](README.md#deploy-to-unraid-over-ssh), using local source and Docker context serverone (ssh://serverone).
- Preserve port 5293, SQL Server service, and named volume homehub_homehub-data-protection. Never delete persistent Docker volumes.
- The user explicitly authorised deleting the old HomeHub database/data for Restart. Verify the exact database target and leave unrelated databases untouched.
- Credentials belong in ignored .env.unraid; .env.unraid.example contains placeholders only. Never print resolved Compose configuration or container environment secrets.
- Check container health and /health/ready before claiming success.

## Documentation maintenance

Update these instructions when conventions change, README when setup/architecture changes, and useful module documentation when behavior changes. Describe actual implementation separately from remaining planned work.
