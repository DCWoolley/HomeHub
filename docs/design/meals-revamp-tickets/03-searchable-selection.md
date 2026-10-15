# Search and select recipe ingredients in the meal editor

Approved implementation ticket. Status: ready-for-agent.

## Parent

[Specification #20](https://github.com/DCWoolley/HomeHub/issues/20)

## What to build

Replace the meal modal's temporary ingredient selection control with quick name search and selectable results. Members see package size, choose an ingredient, enter a quantity in its unit, and explicitly Add it to the draft recipe.

## Acceptance criteria

- [ ] Typing filters accessible household ingredients by name and displays package amount/unit in results. Provide clear no-results feedback.
- [ ] Click, tap, and keyboard selection choose a result and reveal quantity entry with its unit and an Add action.
- [ ] Selection alone does not insert or persist a recipe row. Add inserts a valid quantity into the existing meal draft, and quantity editing/removal continue to work.
- [ ] Mark ingredients already in the draft as “Already added” and prevent duplicate addition. Removing a row makes that ingredient selectable again.
- [ ] Preserve the entire meal draft while searching and selecting; all recipe changes still use the single meal Save and discard behavior.
- [ ] Keep quantities in ingredient units rather than packages, preserving decimal precision, stale-unit validation, and household access rules.
- [ ] Verify desktop, mobile, keyboard/focus behavior, empty search, duplicate prevention, quantity validation, save/reload, and cancel/reload through the existing browser harness. Retain integration coverage at the existing use-case boundary and update behavior documentation.

## Blocked by

- [#22](https://github.com/DCWoolley/HomeHub/issues/22) — Edit a meal and its recipe together with one atomic Save.
