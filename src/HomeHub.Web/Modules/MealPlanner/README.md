# Meal Planner

Household account members can manage a shared, name-only meal list at `/Meals`. Names are trimmed and compared using invariant uppercase normalization; a household-specific unique index prevents duplicate records. All writes serialize on the household and revalidate current membership.

Meal controls stay disabled during static prerendering and enable after the interactive component first renders. This prevents input being entered or forms being posted before the Blazor circuit is ready.

Deleting a meal requires confirmation. If used in the schedule, the confirmation warns that it will be removed from every slot that uses it. Deletion cascades atomically to planned occurrences and preserves other meals in each slot. If an unused meal becomes used after preview, deletion requires a new warning and confirmation.

The planner at `/MealPlanner` is one reusable Monday–Sunday schedule with breakfast, lunch, and dinner. There are no calendar dates, previous/next weeks, or automatic weekly resets. Each slot accepts multiple distinct meals. The same meal can appear in other slots. Members can add, replace, and remove individual occurrences and refresh the shared schedule. A composite foreign key enforces household isolation. Each change is saved immediately; other members see changes on refresh or when opening the planner.

The SingleMealSchedule migration replaces the dated PlannedMeals table with MealScheduleEntries and starts the schedule empty. The user authorised discarding the existing test plans; household meal lists remain intact. Rolling back also starts with an empty dated-plan table.

Use “Create a meal” inside a slot to enter a name without leaving the planner. Saving creates the meal and its occurrence in one transaction and preserves other slot selections. Matching names reuse the existing meal, including concurrent creations; an existing occurrence is not added twice. Cancelling changes nothing. Meals contain names only; recipes, ingredients, and shopping lists are not implemented.
