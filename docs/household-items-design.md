# Household Items and Expected Weekly Usage

Status: Design approved and ticket breakdown published as #25–#28. Implementation authorised.

## Agreed scope

- Provide a reusable Household Items Catalogue for non-food consumables such as toilet roll, washing-up liquid, bin bags, shampoo, and laundry detergent.
- Keep food outside this feature. Reuse the existing develop household foundation and preserve its separate Ingredients feature; the original interview checkout predated that work.
- Give each item a default usage unit and allow fractional quantities.
- Maintain a recurring Expected Weekly Usage baseline until it is changed, rather than requiring a new list each week.
- Show expected usage totals for a chosen number of weeks: weekly usage multiplied by the number of weeks.
- Users check existing supplies and decide what to purchase themselves.
- Defer stock tracking and rounding purchases to pack sizes.
- Share the catalogue and usage amounts within a Household.
- Each user belongs to at most one Household. The creator is the initial Household Manager, who manages membership.
- Joining by invitation requires acceptance; sending an invitation does not grant access.
- All Household members can add items and edit shared usage amounts.
- A catalogue item may have no Expected Weekly Usage amount. Such items contribute nothing to planning totals.
- Changing a usage unit requires a replacement weekly quantity when an amount is present; do not reinterpret the old numeric amount in the new unit.
- Removing an item archives it and excludes it from planning. Archived items can be restored.
- Restoring an item restores its previous weekly usage baseline, which remains editable.
- The Household Manager must transfer management to another member before leaving a Household with other members. A sole member can retain the Household without inviting others.
- Leaving or being removed immediately revokes access to that Household.
- Offer suggested usage-unit labels (rolls, bottles, tablets, bags, ml, litres) and allow custom labels. Do not perform automatic unit conversion.
- Default the Planning Period to one week and accept positive whole numbers of weeks.
- Keep fractional planning totals visible without rounding up; totals represent expected consumption, not a purchase order.

## Intended experience

1. Create a Household or accept an invitation to join one.
2. Maintain the shared Household Items Catalogue, including each item's usage unit.
3. Set or edit optional Expected Weekly Usage amounts for catalogue items.
4. Choose a Planning Period to see expected usage totals for active items with a baseline.
5. Check existing supplies and decide what to purchase.
6. Archive items no longer needed and restore them when needed again.

## Example

Toilet roll at 6 rolls per week totals 18 rolls over 3 weeks. Shampoo at 0.25 bottles per week totals 0.75 bottles over 3 weeks. Oven cleaner without a baseline stays in the catalogue and contributes nothing to these totals. Archiving shampoo excludes it from planning; restoring it reinstates its 0.25 bottles per week baseline.

## Final confirmation

The user approved the four-ticket breakdown and requested implementation using implement-spec. Household Manager maps to the existing owner role; preserve that existing role and its membership workflows.

## Documentation approach

Terms are captured in the root GLOSSARY.md. No architectural trade-off requiring an ADR has been agreed yet.

## Follow-up: Add/Edit modal

Status: Modal design approved and implemented; authenticated browser verification and regression checks passed.

The user requested a create/edit modal matching the Ingredients editor instead of the permanent form at the top of the Household Items Catalogue.

### Confirmed decisions

- Use one shared Add/Edit Household Item modal containing name, default usage unit, optional Expected Weekly Usage, and the conditional replacement quantity when changing the unit of an existing baseline.
- After a successful save, close the modal and refresh the catalogue.
- Keep validation errors inside the open modal and retain the entered values.

### Confirmed interactions and reference

The modal reference is the Ingredients editor on the separate ingredients-revamp branch. This Household Items branch still has the older inline Ingredients editor. The reference has Save and Cancel, Escape cancellation, no close icon, no dismissal on overlay click, and dismissal disabled during a save. Its dialog scrolls on narrow screens and provides keyboard focus management.

The user confirmed Cancel and Escape discard unsaved edits, overlay clicks keep the modal open, and dismissal is blocked while saving. Keep the existing catalogue cards, with an Add household item button and each card's Edit action opening the shared modal. Keyboard focus is contained within the dialog and returns to the invoking action on dismissal; the modal scrolls within a narrow viewport. This is a presentation change and introduces no domain terms or architectural decision requiring an ADR.
