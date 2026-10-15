# Ingredient catalogue with modal editing and GBP package prices

Approved implementation ticket. Status: ready-for-agent.

## Parent

[Specification #20](https://github.com/DCWoolley/HomeHub/issues/20)

## What to build

Give household members an independent, searchable Ingredients page with compact entries and one shared Add/Edit ingredient modal. Let members record an optional GBP price for the ingredient's stated package amount and unit.

Implement from the existing develop feature baseline in an isolated branch, preserving the confirmed design documents. Keep the reusable ingredient editor within MealPlanner so the later inline-creation ticket can use it without a separate prefactoring ticket.

## Acceptance criteria

- [ ] Retain direct Ingredients navigation and replace its permanent form with page title, name search, and Add button. Compact entries stack on mobile and offer Edit and confirmed Delete.
- [ ] Add and Edit share a modal for name, unit, positive package amount, and optional package price. Preserve custom-unit creation within this modal.
- [ ] Persist optional exact GBP prices through existing ingredient operations and a non-destructive migration. Existing ingredients retain their data and acquire null prices.
- [ ] Accept blank, zero, and positive prices within a documented decimal storage bound. Reject negative, excess-precision, and out-of-range prices server-side without silently rounding.
- [ ] Display package amount/unit and known prices with £ and two decimal places; clearly distinguish unknown prices from £0.00. Editing can clear a known price.
- [ ] Saving refreshes the catalogue; cancelling makes no ingredient changes. Validation failures retain the entered values and give useful feedback.
- [ ] Search, loading, empty catalogue, no matches, and save/delete errors are usable. Modal focus, keyboard operation, and narrow-screen behavior work within the existing interactive provider.
- [ ] Preserve household membership checks, scoped name uniqueness, package precision, and protections against deleting or changing units of referenced ingredients.
- [ ] Extend existing authenticated browser and ingredient integration/SQL tests for the complete flow, null/zero/valid price round trips, invalid prices, and migrated records. Run relevant build and migration checks and update module behavior documentation.

## Blocked by

None — can start immediately from the existing develop feature baseline.
