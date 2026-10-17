# Household Items

Current household account members share a Household Items Catalogue at `/HouseholdItems`, accessible from household navigation. This is for non-food consumables and is separate from Ingredients and recipes.

Members can add, list, rename, and change the default usage unit of an item. Names and unit labels are trimmed, with maximum lengths of 100 and 50 characters respectively. Names are unique within the household using invariant uppercase normalization. Unit suggestions include rolls, bottles, tablets, bags, ml, and litres; any custom label is allowed without conversion. A catalogue item requires no usage quantity. There is no permanent delete operation.

The catalogue persists in its own table through an additive migration. Each read checks current account membership. Writes lock the household and recheck membership inside the transaction, serializing with removal and leaving. Item IDs are looked up only within the current household. All account members can edit; membership powers remain with the existing owner role. Refresh catalogue reloads other members' changes. Controls enable only after the Interactive Server circuit renders; saves and cancelled edits give feedback.

Expected Weekly Usage, planning totals, and archive/restore are subsequent tickets and are not yet implemented.

## Browser verification

The browser test follows real registration, invitation joining, catalogue create/edit/reload, shared editing, isolation, removal, and a retained editor after revocation. Assertions observe pages, not database tables. The local runner extends the existing Playwright verification approach and publishes the app to exercise production assets, applies fresh migrations to a uniquely named LocalDB database, uses a random localhost port, and removes only that generated database afterward. It does not load production credentials or target the production database.

Prerequisites: .NET 10 SDK, Windows SQL Server LocalDB (`MSSQLLocalDB`), `sqlcmd`, Microsoft Edge, and Playwright resolvable through Node. For example, install Playwright into ignored scratch tooling with `npm install --prefix .scratch/browser-tooling playwright`, then set `NODE_PATH` to that absolute node_modules directory.

From the repository root, run `node scripts/Test-HouseholdItemsLocal.cjs`. The runner publishes the host itself. Existing `dotnet test` checks remain separate.
