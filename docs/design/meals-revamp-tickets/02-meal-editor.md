# Edit a meal and its recipe together with one atomic Save

Approved implementation ticket. Status: ready-for-agent.

## Parent

[Specification #20](https://github.com/DCWoolley/HomeHub/issues/20)

## What to build

Replace the meal list's permanent form and separate recipe page with a shared Add/Edit meal modal. Members can edit the meal name and full recipe as one draft, then Save everything together or discard it.

This slice may retain the existing ingredient selection control temporarily; searchable selection is a subsequent slice. It must already support selecting existing ingredients, entering quantities, and editing/removing recipe rows end to end. Use the parent specification's compact list design so the meal and ingredient pages converge without requiring the ingredient-price ticket first.

Implement from the existing develop feature baseline in an isolated branch, preserving the confirmed design documents.

## Acceptance criteria

- [ ] Meals has a page title, name search, Add button, and compact entries with Edit and confirmed Delete. Entries stack on mobile; retain useful empty and error states.
- [ ] Add and Edit open the same modal with meal name and full ingredient/quantity draft. Editing loads stored values; name-only meals are valid and empty recipes show “No ingredients yet”.
- [ ] Existing ingredients can be selected, assigned quantities in their units, added once, edited, and removed from the draft. Existing decimal and uniqueness rules apply.
- [ ] One logical operation validates and saves the name and complete recipe atomically for both new and existing meals. Prevent duplicate submissions while saving.
- [ ] Invalid quantity, duplicate ingredient, unavailable/inaccessible ingredient, stale unit, or invalid name leaves stored data unchanged, including no orphan new meal. Preserve household write serialization and membership revalidation.
- [ ] Cancel leaves stored meal/recipe values unchanged. Closing a changed modal, including Cancel or Escape, requires discard confirmation. Failed saves retain the draft for correction.
- [ ] Existing recipe URLs open the correct meal modal with authentication and household checks. Remove the obsolete separately saving recipe editor once its callers use the shared modal; preserve quick meal creation from the planner.
- [ ] Retain ingredient reference protections, meal deletion behavior, planner references, and shared ingredient records.
- [ ] Keyboard focus, accessible dialog controls, touch operation, and narrow-screen layout work within the interactive provider boundary.
- [ ] Adapt existing authenticated browser tests and MealPlanner integration/SQL tests for save/reload, edit/remove, discard/reload, name-only meals, legacy recipe links, atomic failures, and household rules. Run focused checks and update behavior documentation.

## Blocked by

None — can start immediately from the existing develop feature baseline. This ticket does not require package prices or the ingredient modal.
