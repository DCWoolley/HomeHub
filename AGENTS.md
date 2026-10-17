# HomeHub agent instructions

## Start here

- Run `./scripts/Test-Workspace.ps1` before exploration. It reports the branch, existing changes, and whether the feature application is present; use `-ExpectedRef` when the task specifies a baseline. Preserve existing changes.
- Read [README architecture](README.md#architecture) for application structure and the affected module's README for its behavior. Features live under `src/HomeHub.Web/Modules`; module-shared UI belongs in its Shared folder, while Components owns routing, layouts, and reusable application UI.
- For domain terminology, read [CONTEXT.md](CONTEXT.md). For specifications or tickets, read [tracker conventions](docs/agents/issue-tracker.md) and [triage labels](docs/agents/triage-labels.md).
- For reviews, read [CODING_STANDARDS.md](CODING_STANDARDS.md) and the originating specification. Keep planned design documents distinct from implemented module behavior.

## Verification

- Use `./scripts/Verify.ps1`; on Windows with LocalDB and Node, use `./scripts/Verify.ps1 -Sql -Browser` for the full gate. See [verification and local browser setup](docs/agents/verification.md) for prerequisites, artifacts, and cleanup.
- Revalidate current membership on every household operation. Preserve atomic writes and the affected module's domain invariants.
- Keep credentials and sensitive personal data out of source and logs. Never log invitation codes.

## Deployment

- Deploy behavior changes after verification unless the user asks otherwise. Follow [README deployment instructions](README.md#deploy-to-unraid-over-ssh), using local source and Docker context serverone.
- Preserve production data, the SQL Server service, port 5293, and the homehub_homehub-data-protection volume. Database resets require fresh explicit user authorization; the completed historical Restart reset provides no ongoing permission.
- Credentials belong in ignored .env.unraid. Never print resolved Compose configuration or container environment secrets.
- Confirm container health and /health/ready before claiming deployment success.

## Documentation

Update README when setup/architecture changes, module documentation when behavior changes, and these navigation pointers when their destinations change.
