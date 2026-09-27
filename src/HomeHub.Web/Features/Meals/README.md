# Meals and meal planner

All current household members can manage meals at `/Meals` and the shared plan at `/MealPlanner`. Both routes require authentication and use static SSR forms with antiforgery protection and the shared app sidebar.

- Meals contain a required name (up to 100 characters). Recipes, ingredients and servings are deferred.
- The repeating plan covers Monday–Friday, with multiple meals allowed on each day. Entries reference saved meals, so renaming a meal updates every occurrence without changing its assignments.
- `/MealPlanner/Entry/{id}` edits member exceptions or removes the entry. No selected members means everyone. Selections belong to that entry, not to the saved meal.
- Member choices come from `IHomeMembers.GetMembersAsync`. Retired selections appear as “Former member” until explicitly cleared; they do not silently expand an exception meal to everyone. Only active household members can be selected when saving.
- Meal deletion opens a confirmation dialog listing affected weekdays. Confirming deletes the meal, all planner entries and their member selections through database cascades. Cancelling does not change data.
- Application contracts live in `Features/Meals/ManageMeals` and `Features/Meals/PlanMeals`; Infrastructure implements them with current membership checks on every request. Domain validates meal names and weekday limits.

Verification includes service tests for persistence, member exceptions, household isolation and revoked access; HTTP tests exercise forms, deletion confirmation, antiforgery and protected routes. The SQL Server migration creates only the three new meal tables and leaves existing household and Identity data intact.
