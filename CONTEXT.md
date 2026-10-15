# HomeHub

HomeHub helps a household manage its meals and reusable ingredients.

## Language

**Ingredient**:
A household-owned food item that can be reused across recipes, with a name, unit, and quantity per package.

**Package amount**:
The quantity contained in one purchased package of an ingredient, expressed in that ingredient's unit.

**Package price**:
The optional GBP purchase price of one package of an ingredient, corresponding to its package amount and unit. An unknown price is distinct from a free package.
_Avoid_: Recipe cost, unit price

**Meal**:
A household-owned named food choice that can be scheduled in the meal planner and may have a recipe.

**Recipe**:
The ingredients and quantities associated with one meal. It does not include cooking instructions or servings.

**Recipe quantity**:
The amount of an ingredient used in a recipe, expressed directly in that ingredient's unit rather than as a number of packages.
