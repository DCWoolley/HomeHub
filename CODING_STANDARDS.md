# Review standards

Review changes against the originating specification and the following cross-file expectations. Mechanical module placement is checked by scripts/Test-Repository.ps1; build and tests check Razor/style association, type correctness, persistence rules, and access behavior.

- Keep each use case cohesive: request, validation, handler, and response belong together. Keep module-specific shared UI inside that module, and reserve Components for routing, layouts, and UI reusable across modules.
- Prefer small cohesive types, direct EF use in use cases, asynchronous I/O, cancellation, and DI. Introduce abstractions when they simplify a real requirement rather than anticipating hypothetical callers.
- Preserve the established account static-SSR boundary for cookies and antiforgery and the Interactive Server boundary for ordinary application pages. Keep overlays inside the interactive provider and controls protected during prerender.
- Apply the existing ResponsiveMenu behavior consistently to household/account navigation, including static SSR, mobile link closing, and Escape focus return.
- Verify household ownership and current membership at operation boundaries, including concurrent writes. Preserve the invariants documented in the affected module README and [CONTEXT.md](CONTEXT.md).
- Keep ingredient quantities, package amounts, and optional GBP package prices consistent across validation, storage, and UI. Preserve the distinction between unknown and zero price, atomic meal/recipe saves, and independently saved ingredients.
- Give failure paths the same attention as successful flows: preserve editable drafts, show actionable errors, and keep credentials and sensitive personal data out of source and logs. Invitation codes are never logged.
- Keep documentation aligned with implemented behavior. Setup/architecture belongs in README, domain behavior in module documentation, terminology in CONTEXT.md, and future plans in design documents.

Use the existing browser harness for user journeys and MealPlanner integration tests for persistence and validation. Review tests for observable behavior rather than dependence on private methods or incidental markup/classes.
