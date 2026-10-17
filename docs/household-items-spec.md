# Household Items Catalogue and Expected Weekly Usage

## Problem Statement

Household members need a shared way to list non-food consumables and estimate how much they will use before shopping. Without a reusable catalogue and recurring usage baseline, they must repeatedly remember items and calculate quantities themselves. They need consumption estimates while retaining responsibility for checking supplies and deciding what to purchase.

## Solution

Provide a shared Household Items Catalogue and optional Expected Weekly Usage amounts. Members choose a Planning Period to see expected consumption totals. Each item has a suggested or custom usage unit; quantities and totals may be fractional. Household membership controls access, and a transferable Household Manager role controls membership. Archiving removes an item from planning while preserving it and its baseline for restoration.

## User Stories

1. As a signed-in user without a Household, I want to create a Household, so that I can start planning household consumables.
2. As a Household creator, I want to become its initial Household Manager, so that I can manage membership.
3. As a Household Manager, I want to invite another person, so that we can plan together.
4. As an invited user, I want to accept an invitation before joining, so that membership requires my agreement.
5. As a Household member, I want invitations alone to grant no access, so that unaccepted invitees cannot see our information.
6. As a user, I want to belong to at most one Household, so that my shared catalogue has an unambiguous Household context.
7. As a Household member, I want to view our shared catalogue, so that I can see the non-food consumables we plan for.
8. As a Household member, I want to add a Household Item, so that it can be reused in future planning.
9. As a Household member, I want to edit an item's identifying details, so that the catalogue remains useful.
10. As a Household member, I want each item to have a default usage unit, so that its quantities have a clear meaning.
11. As a Household member, I want suggested units such as rolls, bottles, tablets, bags, ml, and litres, so that common items are easy to enter.
12. As a Household member, I want custom unit labels, so that unusual consumables can be represented.
13. As a Household member, I want to set Expected Weekly Usage for an item, so that I can estimate future consumption.
14. As a Household member, I want fractional weekly quantities, so that items used slowly can be planned accurately.
15. As a Household member, I want an item's baseline to repeat until changed, so that I do not need to recreate a list each week.
16. As a Household member, I want to change a baseline, so that planning reflects our current habits.
17. As a Household member, I want catalogue items without a baseline, so that occasionally purchased items remain available.
18. As a Household member, I want to clear an item's baseline, so that I can keep the item without including it in usage totals.
19. As a Household member, I want to provide a replacement quantity when changing the unit of an existing baseline, so that the old quantity is not silently given a different meaning.
20. As a Household member, I want every member to edit the shared catalogue and usage amounts, so that maintenance is not restricted to one person.
21. As a Household member, I want planning to start at one week, so that I can immediately see a useful estimate.
22. As a Household member, I want to choose a positive whole number of weeks, so that I can plan for the interval before my next shopping trip.
23. As a Household member, I want each planning total to equal weekly usage multiplied by the selected weeks, so that I can understand the estimate.
24. As a Household member, I want fractional totals to stay visible without rounding up, so that estimates reflect expected consumption.
25. As a Household member, I want items without a baseline excluded from planning totals, so that missing usage amounts are not treated as demand.
26. As a Household member, I want to archive an item, so that it stops appearing in active planning without being permanently lost.
27. As a Household member, I want to find and restore archived items, so that I can recover accidental removals or resume using an item.
28. As a Household member, I want restoration to recover the previous baseline, so that I do not have to recreate it.
29. As a Household member, I want to edit a restored baseline, so that I can adjust it before using its estimate.
30. As a Household member, I want another Household's data to be inaccessible, so that each Household's information stays private.
31. As a Household Manager, I want to remove members, so that I can maintain the Household's membership.
32. As a Household member, I want ordinary members prevented from managing membership, so that those decisions remain with the Household Manager.
33. As a Household Manager, I want to transfer management to another current member, so that responsibility can change hands.
34. As a Household Manager leaving a Household with other members, I want management transferred first, so that the remaining members retain a manager.
35. As a sole Household member, I want to retain my Household without inviting anyone, so that personal use is supported.
36. As a Household member, I want to leave the Household, so that I can stop participating in it, subject to the manager-transfer requirement.
37. As a Household member, I want leaving or removal to revoke access immediately, so that former members cannot continue reading or editing shared data.
38. As a person planning purchases, I want consumption estimates clearly distinguished from quantities to buy, so that I can check existing supplies and decide what is needed.

## Implementation Decisions

- Extend the existing Blazor application, authenticated user model, and EF Core SQL Server persistence. Implementation starts from the existing develop baseline, which already includes Household membership, invitations, ownership transfer, access enforcement, Ingredients, recipes, and test infrastructure. Reuse these capabilities; the earlier design checkout predated them.
- Introduce household membership and invitation capabilities, a Household Items Catalogue, recurring usage maintenance, and a planning totals view.
- Persist Household membership, the current Household Manager, invitations and their acceptance state, Household Items with usage units and archive state, and optional recurring usage amounts. Exact entity layout and storage types remain implementation choices.
- Scope every item, usage operation, invitation, and membership operation to its Household. Enforce access and role checks on the server as well as reflecting permissions in the interface.
- Enforce at most one Household membership per user, including when accepting an invitation. An invitation alone is not membership.
- All current members may maintain items and usage. Only the current Household Manager manages membership; transfer management only to another current member.
- Prevent a manager from leaving other members without transferring management. Leaving or removal must revoke access immediately, including further operations from an already-open session; do not rely solely on a future sign-in or periodic authentication refresh.
- Keep catalogue inclusion distinct from having a baseline. An absent baseline contributes nothing to planning.
- Treat the baseline as recurring expected consumption, not a dated usage log. Changing it changes subsequent planning estimates.
- Each item has a default usage-unit label. Offer suggestions and custom labels without automatic conversion.
- Where a baseline exists, changing the unit requires an explicitly supplied replacement quantity in the new unit. Items without a baseline can change unit without acquiring one.
- Support fractional quantities and preserve fractional totals. A Planning Period is a positive whole number of weeks, defaulting to one. An active item with a baseline contributes that baseline multiplied by the period, expressed in its own unit.
- Archive rather than permanently delete catalogue items. Exclude archived items from active planning and retain their previous usage baseline for restoration.
- Provide shared views for catalogue maintenance, expected usage, planning totals, archived items, and membership management; exact navigation and visual layout remain implementation choices.
- Do not introduce an API contract or separate service architecture solely for this feature; those details were not agreed in the design interview.

## Testing Decisions

- The user confirmed one primary test boundary: authenticated browser workflows against the running application and a test database.
- Test externally observable behaviour and persisted outcomes, not internal method calls, component structure, or an exact database schema.
- Cover Household onboarding and membership, invitation acceptance, catalogue and baseline editing, planning totals, unit changes, archive and restore, and management transfer through that shared boundary.
- Exercise separate users and Households to verify both shared editing and isolation. Attempt protected reads and mutations as an invitee, ordinary member, manager, and former member as appropriate.
- Verify that leaving or removal denies subsequent access from an already-open session as well as after navigation or a new sign-in.
- Verify that invitations cannot bypass the one-Household rule and that a manager cannot leave other members without transferring responsibility.
- Verify representative consumption examples: 6 rolls per week becomes 18 rolls over 3 weeks; 0.25 bottles per week becomes 0.75 bottles over 3 weeks. Changing the Planning Period recalculates displayed totals.
- Verify rejection of zero, negative, and fractional Planning Periods, which violate the agreed positive-whole-weeks rule.
- Verify that an absent baseline contributes no demand; archiving excludes an item; restoring reinstates the previous baseline; and that baseline remains editable.
- Verify that a unit change with an existing baseline requires a replacement quantity and never silently reinterprets the old value.
- Verify persistence by revisiting the feature after edits and by observing shared changes as another member.
- Reuse the existing authenticated browser verification and test infrastructure on develop. The confirmed primary seam for this feature is authenticated browser workflows against a running app and a test database.

## Out of Scope

- Food, Ingredients, recipes, and meal planning.
- Actual consumption recording, dated weekly usage history, and independently maintained lists for each calendar week.
- Inventory or cupboard stock tracking, stock deductions, and automatic replenishment.
- Automatic purchase quantities, pack-size rounding, shopping orders, pricing, budgets, and retailer integration.
- Automatic conversion between units.
- Membership in multiple Households at once.
- Additional member permission tiers beyond shared editing and the Household Manager's membership powers.
- Permanent deletion of Household Items.

## Further Notes

- This spec synthesises the user's accepted design decisions; it does not implement the feature.
- Household Manager names the current transferable role. Household creator refers only to the person who originally created the Household.
- The existing domain glossary supplies the canonical terminology. No ADR has been adopted for this feature.
- Preserve existing invitation delivery and expiry behaviour. Household Manager maps to the existing owner role; do not rename that role. Item-name uniqueness, detailed quantity validation limits, and visual styling remain implementation choices consistent with existing conventions.
- The approved implementation graph is published as GitHub issues #25, #26, #27, and #28 in DCWoolley/HomeHub. All carry ready-for-agent, with native blocking links in that order. The user approved implementation of the entire graph.

