# Meal Planner

Household account members can manage a shared, name-only meal list at `/Meals`. Names are trimmed and compared using invariant uppercase normalization; a household-specific unique index prevents duplicate records. All writes serialize on the household and revalidate current membership.

Deleting a meal requires confirmation. If used in a plan, the confirmation warns that all occurrences, including past and future weeks, will be removed. Deletion cascades atomically to planned occurrences and preserves other meals in each slot. If an unused meal becomes used after preview, deletion requires a new warning and confirmation.

The weekly planner at `/MealPlanner` shows dated Monday–Sunday weeks with breakfast, lunch, and dinner. Each slot accepts multiple distinct meals. The same meal can appear in other slots or weeks. Members can add, replace, and remove individual occurrences, navigate between weeks, choose a date, and refresh the shared plan. A composite foreign key enforces household isolation. Each change is saved immediately; other members see changes on refresh or when loading a week.

Use “Create a meal” inside a slot to enter a name without leaving the planner. Saving creates the meal and its occurrence in one transaction and preserves other slot selections. Matching names reuse the existing meal, including concurrent creations; an existing occurrence is not added twice. Cancelling changes nothing. Navigating to another week closes the unsaved form. Meals contain names only; recipes, ingredients, and shopping lists are not implemented.
