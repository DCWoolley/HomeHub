# Meal Planner

Household account members can manage a shared, name-only meal list at `/Meals`. Names are trimmed and compared using invariant uppercase normalization; a household-specific unique index prevents duplicate records. All writes serialize on the household and revalidate current membership.

Deleting a meal requires confirmation. If used in a plan, the confirmation warns that all occurrences, including past and future weeks, will be removed. Deletion cascades atomically to planned occurrences and preserves other meals in each slot. If an unused meal becomes used after preview, deletion requires a new warning and confirmation.

The planned-occurrence schema supports multiple meals per date and breakfast/lunch/dinner slot, with a composite foreign key enforcing household isolation. The weekly planner and creating meals within it are the next implementation steps (#11 and #12).
