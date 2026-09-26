# Web features

Place business features here, at the Web project root, using `<Feature>/<UseCase>/` folders. Keep each use case's Razor components, endpoints, and UI models together. Application orchestration belongs in the matching `HomeHub.Application/Features/<Feature>/<UseCase>/` slice.

Components shared within a feature can live in `<Feature>/Shared/`. The app shell, layouts, routing, and reusable cross-feature UI belong in the sibling `Components` folder.

Razor files here do not inherit `Components/_Imports.razor`. Add imports within this folder tree as needed. Do not create a `Components/Features` folder.

See the repository [agent instructions](../../../AGENTS.md) for dependency rules, clean code practices, and documentation maintenance requirements.
