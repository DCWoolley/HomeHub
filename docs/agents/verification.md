# Verification

Run from a HomeHub checkout using PowerShell 7.3+, .NET 10, and Git:

```powershell
./scripts/Verify.ps1
./scripts/Verify.ps1 -Sql -Browser
```

The first command restores the pinned local EF tool and NuGet packages, checks feature placement, builds, runs the portable tests, checks the EF model against migrations, and checks staged/unstaged whitespace. SQL tests are explicitly excluded in this mode. The full Windows command requires SQL Server LocalDB, sqlcmd, and Node.js 20+; it fails if any test is skipped, checks SQL-test results exist, and then runs local browser verification. No production credentials or database are involved.

The checked-in Verify workflow runs the full command on Windows for pushes and pull requests. It checks the committed comparison diff too and uploads test results, logs, and screenshots. Deployment is separate.

## Local browser verification

```powershell
./scripts/Test-LocalBrowser.ps1
```

This installs the exact Playwright dependency graph with npm ci and the matching Chromium binary, builds before starting the app, creates a unique HomeHub_RestartTest_ database on the fixed local MSSQLLocalDB instance, chooses a free loopback port, starts the app in Development with isolated data-protection keys, and waits for readiness. It then exercises the existing recipe browser journeys, including real rejected database writes and successful retries. It always attempts to stop only its own process trees and remove only its generated test database, including on failure or timeout. It never reads .env.unraid.

Logs, screenshots, the generated database name/URL, owned host PID, and cleanup confirmation remain under artifacts/browser/<run>. Host logs are readable while the app runs. The runner has bounded startup and browser timeouts; use -StartupTimeoutSeconds and -BrowserTimeoutSeconds to override them. -NoBuild is for an already verified Debug build (Verify uses it to avoid a redundant build).

If the PowerShell process is forcibly terminated before finally can run, use that run's run.json and host.pid to identify its resources. Confirm the PID still belongs to the test process and the database name matches HomeHub_RestartTest_ followed by 32 hexadecimal characters before manual cleanup. Never stop all dotnet processes or delete unrelated databases. Never commit or upload the generated data-protection keys; CI uploads only logs, screenshots, and non-secret metadata.

## Baseline preflight

```powershell
./scripts/Test-Workspace.ps1
./scripts/Test-Workspace.ps1 -ExpectedRef develop
```

Preflight displays the current branch/commit and existing changes, checks that this is the feature application rather than the starter checkout, and optionally verifies that HEAD contains an expected baseline. It never switches branches or discards changes. Choose the expected ref for the task rather than assuming the remote default branch contains the current feature. The shared verification and browser commands run preflight automatically.

## Host launcher issue

The sandbox shell launcher in this session failed before executing commands while enumerating the unrelated E: drive: “This drive is locked by BitLocker Drive Encryption.” Repo scripts cannot repair the host launcher. Investigate its volume enumeration or report the failure to the host's support channel; keep drive encryption and access policies intact. This is not a reason to add permanent blanket escalation instructions to the repo.
