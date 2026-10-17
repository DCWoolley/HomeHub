# Household Items

Current household account members share a Household Items Catalogue at `/HouseholdItems`, accessible from household navigation. This is for non-food consumables and is separate from Ingredients and recipes.

Members can add, list, rename, and change the default usage unit of an item. Names and unit labels are trimmed, with maximum lengths of 100 and 50 characters respectively. Names are unique within the household using invariant uppercase normalization. Unit suggestions include rolls, bottles, tablets, bags, ml, and litres; any custom label is allowed without conversion. A catalogue item requires no usage quantity. There is no permanent delete operation.

The catalogue persists in its own table through an additive migration. Each read checks current account membership. Writes lock the household and recheck membership inside the transaction, serializing with removal and leaving. Item IDs are looked up only within the current household. All account members can edit; membership powers remain with the existing owner role. Refresh catalogue reloads other members' changes. Controls enable only after the Interactive Server circuit renders; saves and cancelled edits give feedback.

Members can set, change, or clear optional Expected Weekly Usage in the same editor. It is a recurring consumption baseline until explicitly changed, not an actual usage log or a quantity to buy. A blank value means no baseline and clearing it preserves the catalogue item. SQL `decimal(12,4)` stores positive values from 0.0001 to 99,999,999.9999. Server validation rejects zero, negatives, out-of-range amounts, malformed values, exponent/grouping notation, and more than four entered decimal places (including extra trailing zeros), without rounding or losing the entered text. Entry and display use the server's current culture; the form shows a fractional example with the applicable decimal separator.

Changing the unit of an item with a baseline displays a separate, initially empty replacement quantity field. The member must enter a valid replacement in the new unit; both values save in one transaction. No quantity is converted or silently reinterpreted. Cancellation or validation failure leaves both stored values intact. To change unit while removing a baseline, clear and save the baseline first. Items without baselines can change unit without acquiring one.

Every successful edit refreshes an item version. Saves compare the version loaded by the editor while holding the household lock, rejecting stale editors after another member's change and retaining their input for review. Cancel the edit, refresh, and reopen to use the current item. Future item mutations must also refresh this version. The additive migrations preserve existing catalogue entries with absent baselines.

Planning totals and archive/restore are subsequent tickets and are not yet implemented.

## Browser verification

The browser test follows real registration, invitation joining, catalogue create/edit/reload, shared editing, isolation, removal, and a retained editor after revocation. It also covers fractional baseline persistence, editing/clearing, quantity validation with retained input, supported endpoints, missing baselines, atomic unit replacement and cancellation, shared 0.25-bottle usage, stale unit editors, and independent baselines in separate households. Assertions observe pages, not database tables. The local runner extends the existing Playwright verification approach and publishes the app to exercise production assets, applies fresh migrations to a uniquely named LocalDB database, uses a random localhost port, and removes only that generated database afterward. It does not load production credentials or target the production database.

Prerequisites: .NET 10 SDK, Windows SQL Server LocalDB (`MSSQLLocalDB`), `sqlcmd`, Microsoft Edge, and Playwright resolvable through Node. For example, install Playwright into ignored scratch tooling with `npm install --prefix .scratch/browser-tooling playwright`, then set `NODE_PATH` to that absolute node_modules directory.

From the repository root, run `node scripts/Test-HouseholdItemsLocal.cjs`. The runner publishes the host itself. Existing `dotnet test` checks remain separate.
