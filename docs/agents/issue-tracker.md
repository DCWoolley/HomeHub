# Issue tracker: GitHub

Specs and tickets live in GitHub Issues in DCWoolley/HomeHub.
Use the gh CLI with an explicit --repo DCWoolley/HomeHub.

## Operations

- Read an issue's full body, labels, and comments before working on it.
- Create issues with descriptive titles and Markdown bodies.
  For multiline bodies, write a temporary file and use --body-file.
- Apply labels using the mapping in triage-labels.md.
- Publish tickets in dependency order.
- Use native GitHub blocking relationships, referencing issue database IDs.
  If unavailable, list blocking issue references in the ticket body.
- When splitting an existing issue, link tickets as its sub-issues.
  If unavailable, include a parent reference in each ticket.
- A ticket can start when all its blockers are complete.
- Preserve parent issues when publishing child tickets.

## Pull requests as a triage surface

PRs as a request surface: no.
