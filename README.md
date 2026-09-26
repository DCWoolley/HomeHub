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

The new class libraries establish the project structure. Existing template Identity and Entity Framework code currently remains in Web; moving persistence into Infrastructure is a separate refactoring step.

Build the solution with `dotnet build HomeHub.slnx`.

## Feature organization

Use vertical slices, grouping code by feature and use case. Web features live at `src/HomeHub.Web/Features/<Feature>/<UseCase>/`, alongside `Components`, never inside it. Application use cases live at `src/HomeHub.Application/Features/<Feature>/<UseCase>/` and preserve the dependency boundaries above.

The minimal landing page at `/` lives in `Features/Home/Index.razor`. Template demo content has been removed. Identity pages and the error/not-found pages remain under `Components`; new business features use the root-level `Features` folder.

## Working in this repository

Read [AGENTS.md](AGENTS.md) for clean code conventions, feature placement, verification, and documentation maintenance. Agents must update relevant documentation as part of each change when behavior, structure, or conventions change, without requiring a separate reminder.
