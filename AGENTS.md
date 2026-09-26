# HomeHub agent instructions

These instructions apply to the entire repository. Read them before making changes.

## Architecture and dependencies

Use Clean Architecture with vertical slices. Keep compile-time dependencies pointing inward:

- `HomeHub.Domain`: domain entities, value objects, invariants, and domain services. No references to other projects, UI, EF Core, or external service implementations.
- `HomeHub.Application`: use cases and the interfaces they need. References Domain only.
- `HomeHub.Infrastructure`: persistence and external service implementations. References Application and Domain.
- `HomeHub.Web`: Blazor UI, endpoints, and the composition root. References Application and Infrastructure. Use Infrastructure from the composition root to register implementations; business feature code should depend on Application contracts.

Do not introduce circular references or reference Web from another project. Do not add frameworks or packages to inner layers merely to support an outer-layer concern.

Identity is an outer-layer framework integration: Infrastructure owns `Identity/ApplicationUser`, the email sender, and `Persistence/ApplicationDbContext` and its migrations. Web's `Features/Account` owns the Identity UI, cookie setup, redirects, authentication state, and HTTP endpoints. This framework adapter may use Infrastructure's Identity types and ASP.NET Core managers, but must not access the database context directly. Keep Identity and EF dependencies out of Domain and Application; introduce Application contracts when business use cases need user-related capabilities. Preserve authentication behavior and migration history when changing this integration.

## Vertical slices

Organize business functionality by feature and use case, rather than global folders of controllers, services, handlers, or DTOs.

- Web feature code belongs in `src/HomeHub.Web/Features/<Feature>/<UseCase>/`.
- **The Web `Features` folder must be at the project root, alongside `Components`. Never create `Components/Features`.**
- Application use cases belong in `src/HomeHub.Application/Features/<Feature>/<UseCase>/`.
- Keep each use case's request, response, validation, and handler together where needed. Simple use cases do not need separate classes for every role or a mediator library.
- Keep feature-specific Razor components, endpoints, and UI models with the Web slice. Put components shared only by one feature in that feature's `Shared` folder.
- Reserve Web `Components` for the app shell, routing, layouts, error/not-found pages, and genuinely reusable UI. Account pages and their supporting endpoints live in `Features/Account/<UseCase>/` or `Features/Account/Manage/<UseCase>/`, with shared account UI in `Features/Account/Shared`. The minimal landing page lives in `Features/Home/Index.razor`; template demo content has been removed.
- Organize Domain by domain concept or aggregate. Organize Infrastructure implementations by capability or feature as appropriate; do not force either project to mirror every UI use case.
- A slice can span projects, but it must respect the dependency boundaries above. Keep business rules in Domain, orchestration in Application, and technical details in Infrastructure.
- Share code only when there is a demonstrated need. Do not couple slices through another slice's internal handler or UI model.

Razor imports are folder-scoped. Root-level Web features do not inherit `Components/_Imports.razor`. Add imports at `Features/_Imports.razor` or in a narrower feature scope as needed. When moving Razor files, check namespaces, component references, routes, authorization, CSS isolation, and collocated JavaScript paths.

## Clean code practices

- Prefer small, cohesive types and methods with clear, intention-revealing names.
- Follow the surrounding C# and Razor formatting. Keep nullable reference types and implicit usings enabled.
- Prefer the simplest implementation that satisfies the use case. Avoid speculative abstractions, generic repositories, unnecessary base classes, and unrelated refactors.
- Protect domain invariants in Domain. Validate incoming input at the boundary and enforce authorization on the server, not only in the UI.
- Use dependency injection for external dependencies. Avoid service locators, hidden mutable global state, and direct database access from new UI features.
- Use asynchronous APIs for I/O and pass cancellation tokens through when supported. Do not block on tasks with `.Result` or `.Wait()`.
- Handle expected failures explicitly; do not swallow exceptions. Keep secrets and sensitive personal data out of source control and logs.
- Write comments to explain non-obvious decisions or constraints, rather than narrating the code.

## Verification

- Build from the repository root with `dotnet build HomeHub.slnx` after code or project-reference changes.
- Add or update focused tests under `tests/` for meaningful behavior changes. Prefer domain/use-case tests for rules and integration tests for persistence or HTTP behavior where appropriate.
- Run `dotnet test HomeHub.slnx --no-restore -v:m`. Deployment integration tests under `tests/HomeHub.Web.Tests` verify health endpoints, LAN HTTP routes, account protection, and persistent data-protection keys without using the production database.
- For Razor moves or UI changes, verify affected routes, rendering, and authentication behavior in addition to building when the environment permits.
- For Identity persistence changes, run `dotnet ef migrations has-pending-model-changes --project src/HomeHub.Infrastructure --startup-project src/HomeHub.Web`. Do not apply database migrations merely to verify a refactor. See `src/HomeHub.Web/Features/Account/README.md` for Identity verification details.
- For documentation-only changes, check accuracy, paths, links, and `git diff --check`; a build is not required.
- Report what was verified and any blocked checks honestly.

## Unraid deployment

- After implementing new functionality or changing application behavior, always deploy to production once the required verification passes, unless the user explicitly asks not to deploy. This is standing user authorization; do not ask for deployment confirmation again. Follow [the deployment runbook](README.md#deploy-to-unraid-over-ssh).
- Documentation-only changes do not require deployment. If verification or deployment is blocked, report the blocker and do not claim deployment succeeded.
- Use Docker context `serverone` (`ssh://serverone`) and the local working tree. Preserve HomeHub's port 5293, existing SQL database, and named volume `homehub_homehub-data-protection` unless a change is requested.
- Keep credentials in ignored `.env.unraid`; `.env.unraid.example` contains placeholders only. Never print resolved Compose configuration or container environment variables containing secrets.
- Infrastructure owns startup migrations and database readiness; Web maps health endpoints and configures data protection. Compose opts into startup migrations and LAN HTTP explicitly; local HTTPS defaults remain enabled.
- Check container health and `/health/ready` before reporting deployment success. Never delete persistent volumes as part of an update or shutdown.

## Keep documentation current automatically

Documentation maintenance is part of every relevant change; do not wait for a separate user request.

- Update this file in the same change when project conventions, dependency boundaries, feature placement, or verification commands change.
- Update `README.md` when setup, build/run instructions, architecture, or the repository's current state changes.
- Update feature documentation when behavior, entry points, configuration, or important constraints change. Add a feature README only when it provides useful context beyond the code.
- Remove or revise transitional-state notes when the corresponding migration is completed.
- Before finishing, compare documentation with the implemented result and correct stale statements and paths. Describe actual behavior separately from planned work.
- Keep instructions concise and use links to authoritative documentation instead of duplicating long explanations across files.

This is a change-time maintenance rule for agents working in the repository; it does not require a scheduled background job.
