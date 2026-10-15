# Create a missing ingredient without leaving the meal draft

Approved implementation ticket. Status: ready-for-agent.

## Parent

[Specification #20](https://github.com/DCWoolley/HomeHub/issues/20)

## What to build

Offer Add new ingredient from recipe search and open the same ingredient editor used by Ingredients. Save the ingredient independently, return to the preserved meal draft, and select it for quantity entry. Support custom-unit creation without navigating away.

## Acceptance criteria

- [ ] Recipe search offers a clear Add new ingredient action, including when there are no matches, using the shared ingredient modal and its existing name/unit/package/optional GBP price validation.
- [ ] Opening ingredient creation preserves the meal name, recipe rows, and unsaved changes. Cancelling creation returns to that draft without creating an ingredient.
- [ ] Confirming the ingredient modal explicitly saves the ingredient independently. Return to the meal editor and automatically select the new ingredient for quantity entry; require quantity and Add before creating a draft recipe row.
- [ ] Cancelling or discarding the meal later retains the independently saved ingredient in Ingredients while leaving the stored meal/recipe unchanged.
- [ ] Create and select custom units inside this ingredient flow as well as from Ingredients. Preserve the existing household unit rules.
- [ ] Duplicate names, invalid price/quantity, revoked membership, or save failure show actionable feedback without losing either draft or adding duplicate records. Retain existing membership, reference, and precision protections.
- [ ] Dialog transitions remain inside the interactive provider with appropriate keyboard focus, Escape behavior, accessible controls, and usable narrow-screen layouts. The meal draft's discard confirmation must not appear merely because ingredient creation opens.
- [ ] Extend the existing browser harness for create/select/add/save/reload, cancel ingredient creation, create ingredient then discard meal, custom units, and invalid input. Confirm independently saved catalogue data through existing integration tests.
- [ ] Run focused browser/integration checks plus affected build checks; verify both complete list-to-editor journeys and update module documentation, without adding costing, instructions, or servings.

## Blocked by

- [#21](https://github.com/DCWoolley/HomeHub/issues/21) — Ingredient catalogue with modal editing and GBP package prices.
- [#23](https://github.com/DCWoolley/HomeHub/issues/23) — Search and select recipe ingredients in the meal editor.

The searchable-selection ticket already depends on the meal-editor ticket; no redundant direct blocking edge is required.
