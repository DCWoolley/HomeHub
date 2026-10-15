# Meals and ingredients revamp

Status: shared understanding confirmed by the user on 15 October 2026. The implementation specification is [meals-and-ingredients-spec.md](meals-and-ingredients-spec.md). This document describes planned behavior, not implemented behavior.

Published specification: [GitHub issue #20](https://github.com/DCWoolley/HomeHub/issues/20), labelled ready-for-agent. The user confirmed the existing browser and MealPlanner integration test boundaries and default triage vocabulary.

## Repository baseline

The attached worktree starts at main, commit 50d07e2, containing the starter application. The existing meals implementation was inspected read-only on develop at de57a76. Implementation will start from the current develop baseline in an isolated codex/ branch, after checking its latest state and preserving these design documents. No branch changes have been made during the interview.

On that baseline, Ingredients already has its own page and navigation entry. Ingredients have a name, unit, and package amount, but no price. Meals have a name and an optional recipe consisting of ingredient quantities. The recipe page saves individual changes immediately. Cooking instructions and servings are not currently represented.

## Requested behavior

- Replace the meal list's top form with an Add button opening a modal for the meal name and recipe.
- Use the same modal for editing meals.
- Provide ingredient search with click/tap selection during recipe editing.
- Give Ingredients a consistent design with the meal list and retain its independent page.

## Confirmed decisions

- Ingredient price is optional and represents the price of a package with its stated amount and unit.
- This revamp records and displays prices. Automatic recipe and meal-plan costing is outside its scope.
- A missing ingredient can be created within the recipe editor and is then selected automatically.
- One shared Add/Edit meal modal contains the meal name and full ingredient/quantity editor, replacing the separate recipe editor. Existing recipe links open the corresponding meal editor.
- A recipe remains ingredients and quantities only; cooking instructions and servings are outside this revamp.
- Meal name and recipe changes are staged and saved together with one Save. Cancel discards the changes; closing a changed modal prompts for discard confirmation.
- Both list pages use a page title, search, Add button, and compact entries with Edit and Delete. Entries stack on mobile. Ingredient entries display package amount/unit and price, and use a shared Add/Edit ingredient modal.
- Package prices use GBP, accept non-negative values with at most two decimal places, and distinguish blank (unknown) from zero (free). Retailer fields and price history are outside this revamp.
- Confirming the ingredient's own Add modal saves it independently. It remains in Ingredients even if the surrounding meal edit is cancelled; the UI must make this save explicit.
- Ingredient search filters by name and displays package size. Selecting a result exposes quantity entry alongside the ingredient's unit; an Add action places it in the staged recipe.
- Ingredients already in the recipe are marked "Already added" and cannot be added twice. Their quantities can be edited in the recipe list.
- Recipe quantities remain expressed in ingredient units, not packages; for example, 250 grams from a 500-gram package.
- Custom-unit creation is available in the ingredient modal both on Ingredients and inside the meal editor.
- Meals can be saved with only a name. An empty recipe displays "No ingredients yet".

## Existing rules to preserve

- All meal, ingredient, and unit operations retain household ownership and membership checks.
- Meal and ingredient names remain unique within a household.
- Package amounts and recipe quantities remain positive decimals with the existing precision limits; excess precision is rejected rather than silently rounded.
- Referenced ingredients cannot be deleted or have their units changed. Stale recipe saves after unit changes remain rejected.
- Meal deletion removes its recipe and planner references while preserving the ingredient catalogue.
- Existing ingredient prices remain unknown until entered; migration must not turn missing prices into zero.

## Verification targets

- Both lists offer search and modal Add/Edit with consistent desktop and mobile layouts.
- Existing recipe links open the correct meal editor.
- Saving a meal persists its name and recipe together; failure leaves no partial meal/recipe update and retains the draft for correction.
- Cancel and confirmed discard leave the stored meal and recipe unchanged.
- Ingredient creation from a meal saves, selects the new ingredient, and preserves the surrounding meal draft. Cancelling that meal retains the saved ingredient.
- Search supports click/tap and keyboard selection, shows useful empty results, and prevents duplicate recipe ingredients.
- Custom units can be created through either ingredient-entry flow.
- Missing prices, zero prices, valid GBP values, negative values, and excess decimal precision are handled distinctly.
- Existing household isolation, reference protection, quantity validation, and meal-planner behavior continue to pass focused checks.

## Remaining interview decisions

None. The user confirmed the overall shared understanding on 15 October 2026.
