# Revamp meals, recipes, and ingredients

Status: product design, test boundaries, GitHub publication, and default triage labels confirmed on 15 October 2026. Ready for implementation. This is planned behavior, not a description of completed implementation.

## Problem Statement

Managing meals and recipes works, but feels clunky. Persistent forms take space above the lists, editing a meal and editing its recipe are separate activities, and a native ingredient dropdown makes selection slow. Ingredients need a consistent independent management experience and optional package prices.

## Solution

Give Meals and Ingredients matching searchable lists with compact entries and an Add button. Add and Edit open the same modal for each entity. The meal modal edits the meal name and its complete recipe together, using searchable ingredient selection and one Save. Ingredients can be created without leaving the meal editor and have optional GBP package prices.

## User Stories

1. As a household member, I want a meal list without a permanent entry form, so that I can browse my meals easily.
2. As a household member, I want to search meals by name, so that I can quickly find one to edit.
3. As a household member, I want Add to open a meal modal, so that I can create a meal without leaving the list.
4. As a household member, I want Edit to open that same modal with existing values, so that creation and editing feel consistent.
5. As a household member, I want to edit the meal name and recipe together, so that I can finish the whole change in one place.
6. As a household member, I want to save a meal with no ingredients, so that I can record takeaway meals or fill in recipes later.
7. As a household member, I want an empty recipe to say “No ingredients yet”, so that its state is clear.
8. As a household member, I want to search ingredient names in the meal editor, so that I can avoid scrolling through a dropdown.
9. As a household member, I want ingredient results to show package amount and unit, so that I can identify the correct item.
10. As a household member, I want to click, tap, or use the keyboard to select a result, so that selection works on my device.
11. As a household member, I want to enter a quantity beside the selected ingredient's unit before adding it, so that I know what quantity means.
12. As a household member, I want to see already selected ingredients marked “Already added”, so that I do not duplicate recipe entries.
13. As a household member, I want to edit quantities and remove ingredients in the draft recipe, so that I can correct it before saving.
14. As a household member, I want one Save for the meal and recipe, so that my changes succeed together.
15. As a household member, I want Cancel to discard my meal draft, so that exploration does not change a stored recipe.
16. As a household member, I want confirmation before discarding a changed meal modal, so that I do not lose work accidentally.
17. As a household member, I want failed saves to retain my draft and show an actionable error, so that I can correct it without re-entering everything.
18. As a household member, I want to create a missing ingredient inside the meal editor, so that I keep my place.
19. As a household member, I want the newly created ingredient selected automatically, so that I can enter its recipe quantity next.
20. As a household member, I want an explicitly saved ingredient to remain in my Ingredients list if I cancel the meal, so that the ingredient is reusable independently.
21. As a household member, I want custom-unit creation in both ingredient-entry flows, so that I can enter ingredients such as slices without navigating elsewhere.
22. As a household member, I want a directly accessible Ingredients page, so that I can manage the catalogue independently of meals.
23. As a household member, I want Ingredients to share the meal list's search, compact entries, and modal Add/Edit pattern, so that the interface is predictable.
24. As a household member, I want to see package amount, unit, and optional package price on ingredient entries, so that purchase information is easy to scan.
25. As a household member, I want to record or clear an ingredient's GBP package price, so that unknown prices are allowed.
26. As a household member, I want a free package to display £0.00 distinctly from an unknown price, so that price information is accurate.
27. As a mobile user, I want compact entries and usable modals on a narrow screen, so that I can manage meals while shopping or cooking.
28. As a household member, I want existing recipe links to open the corresponding meal editor, so that saved links still work.
29. As a household member, I want referenced ingredients protected against deletion and unit changes, so that existing recipes remain meaningful.
30. As a household member, I want meal deletion to remove its recipe and planner references while preserving ingredients, so that shared catalogue items remain available.
31. As a household member, I want household access rules enforced on every operation, so that other households cannot read or change our data.

## Implementation Decisions

- Use the existing MealPlanner module and its ManageMeals, ManageRecipe, ManageIngredients, and ManageIngredientUnits use cases. Keep cohesive module-shared UI within MealPlanner and retain the established vertical-slice architecture, direct EF access, and Blazor Blueprint conventions.
- Implement against the latest verified develop baseline containing the existing meals feature, in an isolated branch. The currently attached main baseline contains only the starter application; preserve the confirmed design documents when establishing the implementation baseline.
- A Meal has a name and an optional Recipe. A Recipe remains the ingredients and quantities associated with one Meal; no separate recipe catalogue is introduced.
- Both list pages have a title, name search, an Add button, and compact entries with Edit and Delete actions. Entries stack on mobile. Preserve deletion confirmation and useful loading, empty-list, no-search-results, and error feedback.
- Retain the independent Ingredients navigation entry. Ingredient entries display package amount/unit and package price; unknown prices must not look like zero.
- Share one meal modal between Add and Edit. Replace the separate recipe editor with this experience, and resolve existing recipe URLs to the corresponding editor while retaining authentication and household access checks.
- Hold meal-name and recipe changes in a draft. Expose one logical save operation accepting the meal name and the complete intended ingredient/quantity collection. It must validate and persist the entire change atomically, including new-meal creation, rather than chaining the current independently committed recipe mutations.
- Cancel leaves stored meal/recipe data unchanged. Closing a changed meal modal, including Cancel or Escape, offers discard confirmation. Failed saves leave the modal and draft available for correction. Prevent duplicate submissions while a save is in progress.
- Ingredient search filters by name. Results show package size. Selection leads to quantity entry alongside the ingredient unit and an explicit Add action. Already selected ingredients are marked “Already added” and cannot be added again. Existing draft rows support quantity editing and removal.
- Recipe quantities use the ingredient's unit directly: 250 grams from a 500-gram package means 250 grams, not 250 packages. Preserve positive SQL decimal(12,4) package amounts and recipe quantities, rejecting excess precision rather than rounding.
- Use the same ingredient Add/Edit modal from the independent list and for ingredient creation inside the meal editor. Allow custom-unit creation in both contexts. Keep the meal draft intact when moving into and back from ingredient creation, including keyboard focus and selection state.
- Saving an ingredient is an independent action, made explicit in the ingredient modal. After successful creation, select that ingredient in the meal editor for quantity entry. Do not add a recipe row until its quantity is supplied and Add is used. Cancelling the ingredient modal returns to the meal draft without creating an ingredient; cancelling the surrounding meal after an ingredient save does not undo that save.
- Add a nullable decimal package-price field to Ingredient and its read/write contracts. Store exact GBP amounts with two decimal places, validate before persistence, accept zero, and reject negative values or more than two decimal places. A blank value remains null. Existing ingredients migrate with null prices and retain their other data. Use a suitable bounded decimal money column and matching server-side bounds; never use floating-point storage or silently round invalid values.
- Display known prices with £ and two decimal places. The price corresponds to one package with the stated amount and unit. Do not derive unit prices or recipe costs.
- Preserve existing household membership revalidation, household-scoped normalized name uniqueness, one recipe row per ingredient, and reference protections. Reject stale saves after unit changes and saves containing unavailable or inaccessible ingredients without partially updating the meal. Preserve the existing household write-serialization guarantees.
- Referenced ingredients cannot be deleted or have units changed. Meal deletion removes its recipe and planner references and preserves ingredients. Existing planner operations, including quick meal creation, must continue to work.
- Keep overlays within the existing interactive provider boundary. Give dialogs and picker controls accessible names, keyboard operation, sensible focus movement/return, and touch-friendly controls without a new UI framework.
- Keep the module's behavior documentation current, clearly separating implemented behavior from future work. No new architectural decision record is needed for these changes to established module conventions.

## Testing Decisions

- Confirmed test boundaries: verify complete user journeys through the existing authenticated browser test harness; verify atomicity, validation, membership, and persistence at the existing MealPlanner use-case integration boundary. These complement each other without adding a new testing abstraction.
- A good test asserts externally observable state or behavior: what the user sees, what a save persists, and what remains unchanged after a failed or cancelled operation. Avoid tests tied to component internals, private methods, CSS structure, or implementation order.
- Follow the existing recipe browser smoke tests for interactive selection and dialogs, and the RecipeTests, MealTests, IngredientUnitTests, and their SQL Server counterparts for module behavior and persistence. Adapt obsolete expectations for the old dropdown and immediate-save editor.
- Browser acceptance: create and edit meals through the shared modal; search/select ingredients; edit and remove draft rows; reject duplicate selection; save and reload; discard changes and reload; open an existing recipe link; and save a name-only meal.
- Browser acceptance: create a missing ingredient and a custom unit inside the meal editor, retain the draft, automatically select the ingredient, then cancel the meal and verify the ingredient still exists. Verify cancelling ingredient creation leaves the meal draft intact.
- Browser acceptance: search both lists; add and edit ingredients through the common modal; distinguish unknown price from £0.00; verify desktop and narrow-screen usability, keyboard selection, dialog focus, discard confirmation, and actionable validation errors.
- Integration acceptance: save the meal name and complete recipe atomically for both Add and Edit. Inject an invalid quantity, duplicate ingredient, missing/inaccessible ingredient, or stale unit and verify no partial name or recipe changes and no orphan new meal.
- Integration acceptance: enforce name uniqueness, quantity/package precision, household isolation, revoked membership, referenced-ingredient protections, and existing meal/planner deletion behavior. Preserve existing concurrency guarantees without broadening them into a new conflict-resolution feature.
- Persistence acceptance: round-trip null, zero, and valid GBP prices; reject negative, excess-precision, and out-of-range prices; preserve existing records through the migration with null prices. Use isolated test databases for SQL-backed checks.
- Run the project build, focused relevant tests, pending-model-change check for the migration, and whitespace/document-link checks. Verify affected routes and authenticated rendering. Do not apply production migrations merely to test them; report any environment-blocked checks honestly.

## Out of Scope

- Automatic recipe costs, meal-plan costs, unit-price calculations, or shopping-list generation.
- Cooking instructions, servings, recipe variants, or an independent recipe catalogue.
- Retailers, multiple currencies, price history, or automatic price updates.
- Recipe quantity entry in packages, automatic unit conversion, or changes to existing decimal-quantity rules.
- A new UI framework, generic repository/mediator layers, or unrelated planner/account redesign.
- Implementing or deploying the feature as part of producing this specification.

## Further Notes

- The product decisions were confirmed after the design interview. The glossary defines Meal, Recipe, Ingredient, Package amount, Package price, and Recipe quantity; use that terminology consistently.
- Confirmed publication destination: GitHub Issues in DCWoolley/HomeHub with the ready-for-agent label.
- Confirmed default triage vocabulary: needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix. Apply only ready-for-agent to this specification; the other labels describe alternative triage states.
