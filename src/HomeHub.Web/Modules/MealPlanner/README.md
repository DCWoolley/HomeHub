# Meal Planner

Household account members can manage a shared meal list at `/Meals`. A meal requires only a name and may optionally have recipe items. Names are trimmed and compared using invariant uppercase normalization; a household-specific unique index prevents duplicate records. All writes serialize on the household and revalidate current membership.

Meal controls stay disabled during static prerendering and enable after the interactive component first renders. This prevents input being entered or forms being posted before the Blazor circuit is ready.

Day cards use compact spacing and keep touch controls at least 44 pixels high. The current weekday has a violet border, tinted background, and “Today” badge. The initial server-rendered highlight uses the server's weekday; after hydration and when refreshing the schedule, the browser's local weekday is used.

Deleting a meal requires confirmation. If used in the schedule, the confirmation warns that it will be removed from every slot that uses it. Deletion cascades atomically to planned occurrences and preserves other meals in each slot. If an unused meal becomes used after preview, deletion requires a new warning and confirmation.

The planner at `/MealPlanner` is one reusable Monday–Sunday schedule with breakfast, lunch, and dinner. There are no calendar dates, previous/next weeks, or automatic weekly resets. Each slot accepts multiple distinct meals. The same meal can appear in other slots. Members can add, replace, and remove individual occurrences and refresh the shared schedule. A composite foreign key enforces household isolation. Each change is saved immediately; other members see changes on refresh or when opening the planner.

Only dinner is enabled by default, for existing households as well as new ones. At `/MealPlanner/Settings`, any household account member can enable or disable breakfast, lunch, and dinner for the entire seven-day schedule. There are no per-day overrides. Settings are persisted per household. Disabling a slot hides its entries without deleting them; enabling it restores those selections. All slots may be disabled, in which case the planner links to settings. Reads hide disabled slots, and add/replace/remove and inline meal creation recheck current settings inside the household write transaction so stale pages cannot edit disabled slots. Meal deletion still removes all references, including hidden selections.

The SingleMealSchedule migration replaces the dated PlannedMeals table with MealScheduleEntries and starts the schedule empty. The user authorised discarding the existing test plans; household meal lists remain intact. Rolling back also starts with an empty dated-plan table.

Use “Create a meal” inside a slot to enter a name without leaving the planner. Saving creates the meal and its occurrence in one transaction and preserves other slot selections. Matching names reuse the existing meal, including concurrent creations; an existing occurrence is not added twice. Cancelling changes nothing. Recipes are optional and do not change planner selections or settings.

## Ingredients and recipes

At `/Ingredients`, any current household account member can create, list, edit, and delete reusable ingredients with a name, unit, and amount per package. Names are required, trimmed, limited to 100 characters, and unique per household using the same invariant uppercase normalization as meals. Ingredient IDs remain stable through renames. Deletion requires confirmation and is blocked while any recipe references the ingredient; remove it from those recipes first.

The unit pick list uses shared household units. The Add a unit form accepts any required name up to 50 characters, such as grams, millilitres, packets, or scoops. Trimmed names match by invariant uppercase; adding a duplicate, including concurrent additions, reuses the existing unit. New households add their first unit from this form. Unit lists and selections are isolated to the current household; composite foreign keys enforce this in persistence.

The Recipe link on each meal opens `/Meals/{mealId}/Recipe`. Select an ingredient, enter a quantity, and add it; edit an existing item's quantity or remove it from that recipe. A meal has one composition and at most one row per ingredient. Duplicate additions report that the existing quantity should be edited, without overwriting or adding quantities. A stale edit of a removed item is rejected. Empty recipes and existing name-only meals remain valid. Refresh loads other members' changes. Renaming a meal preserves its recipe; deleting a meal cascades to its recipe items and scheduled occurrences, preserving reusable ingredients and other meals.

For Pasta with unit grams and amount 500, the amount describes a 500-gram packet. A recipe quantity of 250 means 250 grams, not 250 packets or a fraction multiplier. The recipe editor shows the selected unit and package amount; saved rows show quantity and unit. A recipe may use more than one package's amount. Editing the package amount does not scale existing recipe quantities. Changing a referenced ingredient's unit is blocked until it is removed from those recipes, and stale recipe forms reject saves if the selected unit changed. There are no automatic conversions between units.

C# decimal and SQL `decimal(12,4)` preserve positive amounts and recipe quantities from 0.0001 to 99,999,999.9999. Server validation rejects zero, negatives, out-of-range values, and unsupported precision rather than rounding. Text entry/display uses the server's current culture consistently; examples show the expected decimal separator, and grouping separators or exponent notation are not accepted. Quantities remain fractional for future aggregation.

Every ingredient/recipe operation rechecks membership. Household write locking serializes duplicate creation, recipe additions, deletion, and membership changes. Composite foreign keys prevent linking a recipe to another household's meal or ingredient; the recipe's composite primary key prevents duplicate ingredients. Ingredient deletion is restricted and meal deletion cascades. The additive MealRecipes migration preserves existing meals, schedules, and slot settings.

The IngredientUnitsAndAmounts migration creates a packets unit per existing household and assigns existing ingredients amount 1 and that unit. Existing recipe quantities retain their packet meaning without scaling. Meals, schedules, slot settings, ingredient IDs, and recipe items are preserved.

Controls remain disabled until the Interactive Server circuit renders. New pages use the existing AppLayout provider and native household navigation. Shopping-list generation, purchasing rounding, inventory, prices, conversion between units, cooking instructions, servings, and recipe variants remain future work. Stable ingredient references, package amounts, and unrounded decimal quantities support later shopping-list calculations across planned meal occurrences.
