// Input contains only synthetic test cookies; never print it. Local registration uses a disposable loopback host.
const { chromium, expect } = require('playwright/test');
const assert = require('node:assert/strict');
const path = require('node:path');
async function main() {
    let input = ''; for await (const chunk of process.stdin) input += chunk;
    const settings = JSON.parse(input);
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let page;
    try {
        const context = await browser.newContext({ viewport: { width: 1280, height: 1000 } });
        await context.addCookies(settings.cookies || []); page = await context.newPage();
        const errors = []; page.on('pageerror', error => errors.push(error.message));
        const entry = name => page.getByRole('article').filter({ has: page.getByRole('heading', { name, exact: true }) });
        const dialog = name => page.getByRole('dialog', { name, exact: true });
        const click = (root, name) => root.getByRole('button', { name, exact: true }).click();
        const suffix = Date.now().toString(36);
        const pasta = `Browser pasta ${suffix}`, meal = `Browser recipe meal ${suffix}`, slices = `Browser slices ${suffix}`;
        if (settings.register) {
            assert.match(settings.baseUri, /^http:\/\/(127\.0\.0\.1|localhost):\d+$/, 'Local registration requires a loopback host');
            await page.goto(settings.baseUri + '/Account/Register');
            await page.locator('#name').fill('Browser tester'); await page.locator('#email').fill(`browser-${suffix}@example.invalid`);
            await page.locator('#password').fill('Browser!9aA' + suffix); await page.locator('#confirm').fill('Browser!9aA' + suffix);
            await page.locator('#household').fill(`Browser household ${suffix}`); await click(page, 'Create account'); await page.waitForURL('**/Household');
        }
        if (settings.recipePath) {
            await page.goto(settings.baseUri + settings.recipePath);
            await expect(dialog('Edit meal').locator('#meal-name')).toHaveValue('Verification pasta bake');
            await expect(dialog('Edit meal').getByRole('textbox', { name: 'Quantity for Verification pasta (grams)', exact: true })).toHaveValue('250');
            await click(dialog('Edit meal'), 'Cancel');
            await page.waitForURL('**/Meals');
        }
        await page.goto(settings.baseUri + '/Ingredients');
        await expect(page.getByRole('button', { name: 'Add ingredient', exact: true })).toBeEnabled();
        assert.equal(await page.locator('#ingredient-name').count(), 0);
        await click(page, 'Add ingredient'); let ingredientDialog = dialog('Add ingredient');
        await expect(ingredientDialog).toBeVisible();
        const details = ingredientDialog.locator('details');
        if (await details.getAttribute('open') === null) await details.locator('summary').click();
        await ingredientDialog.locator('#unit-name').fill('grams'); await click(ingredientDialog, 'Save unit');
        await expect(ingredientDialog.getByRole('status')).toContainText('Unit ready to use.');
        await ingredientDialog.locator('#ingredient-name').fill(pasta); await ingredientDialog.locator('#ingredient-amount').fill('500');
        await ingredientDialog.locator('#ingredient-price').fill('1.001'); await click(ingredientDialog, 'Save ingredient');
        await expect(ingredientDialog.getByRole('alert')).toContainText('two decimal places');
        await ingredientDialog.locator('#ingredient-price').fill('1.50'); await click(ingredientDialog, 'Save ingredient');
        await expect(entry(pasta)).toContainText('500 grams'); await expect(entry(pasta)).toContainText('£1.50');
        for (const [value, text] of [['', 'Price unknown'], ['0.00', '£0.00']]) {
            await click(entry(pasta), 'Edit'); await dialog('Edit ingredient').locator('#ingredient-price').fill(value);
            await click(dialog('Edit ingredient'), 'Save ingredient'); await expect(entry(pasta)).toContainText(text);
        }
        await page.locator('#ingredient-search').fill('no-such-ingredient'); await expect(page.getByText('No ingredients match your search.', { exact: true })).toBeVisible();
        await page.locator('#ingredient-search').fill('');
        console.log('Ingredient modal, custom unit, package size and optional GBP prices verified.');

        await page.goto(settings.baseUri + '/Meals');
        await expect(page.getByRole('button', { name: 'Add meal', exact: true })).toBeEnabled(); assert.equal(await page.locator('#meal-name').count(), 0);
        await click(page, 'Add meal'); let mealDialog = dialog('Add meal');
        const row = name => mealDialog.locator('.recipe-row').filter({ has: page.getByText(name, { exact: true }) });
        await mealDialog.locator('#meal-name').fill(meal); await mealDialog.locator('#recipe-search').fill(pasta);
        await mealDialog.getByRole('button').filter({ hasText: pasta }).focus(); await page.keyboard.press('Enter');
        await expect(mealDialog.locator('#recipe-quantity')).toBeFocused(); await mealDialog.locator('#recipe-quantity').fill('250');
        await click(mealDialog, 'Add to recipe'); await expect(row(pasta)).toBeVisible();
        await expect(mealDialog.getByRole('button').filter({ hasText: pasta })).toBeDisabled();
        await click(mealDialog, 'Save meal'); await expect(entry(meal)).toBeVisible(); await page.reload();
        await click(entry(meal), 'Edit'); mealDialog = dialog('Edit meal'); await expect(row(pasta).getByRole('textbox')).toHaveValue('250');
        await row(pasta).getByRole('textbox').fill('0.00001'); await mealDialog.locator('#meal-name').fill('Invalid draft name');
        await click(mealDialog, 'Save meal'); await expect(mealDialog.getByRole('alert')).toContainText('four decimal places');
        await click(mealDialog, 'Cancel'); await click(dialog('Discard meal changes?'), 'Keep editing');
        await expect(mealDialog.locator('#meal-name')).toHaveValue('Invalid draft name'); await page.keyboard.press('Escape');
        await click(dialog('Discard meal changes?'), 'Discard changes'); await page.reload(); await click(entry(meal), 'Edit');
        await expect(row(pasta).getByRole('textbox')).toHaveValue('250'); await expect(mealDialog.locator('#meal-name')).toHaveValue(meal);
        await row(pasta).getByRole('textbox').fill('125.5'); await click(mealDialog, 'Save meal'); await expect(entry(meal)).toBeVisible();
        console.log('Meal editor, keyboard selection, duplicate guard, atomic validation, save/reload and discard verified.');

        await click(entry(meal), 'Edit'); await mealDialog.locator('#recipe-search').fill(slices); await click(mealDialog, 'Add new ingredient');
        ingredientDialog = dialog('Add ingredient'); await expect(ingredientDialog).toContainText('even if you cancel the meal');
        await click(ingredientDialog, 'Cancel'); await expect(mealDialog.locator('#meal-name')).toHaveValue(meal);
        await click(mealDialog, 'Add new ingredient'); await ingredientDialog.locator('summary').click();
        await ingredientDialog.locator('#unit-name').fill('slices'); await click(ingredientDialog, 'Save unit');
        await expect(ingredientDialog.getByRole('status')).toContainText('Unit ready to use.');
        await ingredientDialog.locator('#ingredient-amount').fill('10'); await click(ingredientDialog, 'Save ingredient');
        await expect(mealDialog.locator('#recipe-quantity')).toBeFocused(); await expect(mealDialog.locator('#quantity-help')).toContainText(slices);
        await expect(row(pasta).getByRole('textbox')).toHaveValue('125.5'); await click(mealDialog, 'Cancel');
        await click(dialog('Discard meal changes?'), 'Discard changes'); await page.goto(settings.baseUri + '/Ingredients');
        await expect(entry(slices)).toContainText('10 slices'); await click(entry(pasta), 'Delete'); await click(page, 'Confirm deletion');
        await expect(page.getByRole('alert')).toContainText('used in a recipe'); await click(page, 'Cancel deletion');
        await click(entry(pasta), 'Edit'); await dialog('Edit ingredient').locator('#ingredient-unit').selectOption({ label: 'slices' });
        await click(dialog('Edit ingredient'), 'Save ingredient'); await expect(dialog('Edit ingredient').getByRole('alert')).toContainText('before changing its unit');
        await click(dialog('Edit ingredient'), 'Cancel');
        console.log('Inline creation, custom units, independent save, draft preservation and reference guards verified.');

        await page.goto(settings.baseUri + '/Meals'); await click(entry(meal), 'Edit');
        await expect(row(pasta).getByRole('textbox')).toHaveValue('125.5');
        await mealDialog.evaluate(el => Promise.all(el.getAnimations({ subtree: true }).map(animation => animation.finished)));
        if (settings.artifacts) await page.screenshot({ path: path.join(settings.artifacts, 'recipe-desktop.png'), fullPage: true });
        await page.setViewportSize({ width: 390, height: 844 }); await expect(mealDialog).toBeVisible();
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Mobile page overflows');
        await mealDialog.evaluate(el => Promise.all(el.getAnimations({ subtree: true }).map(animation => animation.finished)));
        const bounds = await mealDialog.boundingBox(); assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= 391, 'Mobile modal overflows');
        if (settings.artifacts) await page.screenshot({ path: path.join(settings.artifacts, 'recipe-mobile.png'), fullPage: true });
        await row(pasta).getByRole('button', { name: `Remove ${pasta}`, exact: true }).click();
        await expect(mealDialog.getByText('No ingredients yet', { exact: true })).toBeVisible(); await click(mealDialog, 'Save meal');
        await expect(entry(meal)).toBeVisible(); await click(entry(meal), 'Edit'); await expect(mealDialog.getByText('No ingredients yet', { exact: true })).toBeVisible(); await click(mealDialog, 'Cancel');
        await page.locator('#meal-search').fill('no-such-meal'); await expect(page.getByText('No meals match your search.', { exact: true })).toBeVisible(); await page.locator('#meal-search').fill('');
        if (settings.artifacts) await page.screenshot({ path: path.join(settings.artifacts, 'meals-mobile.png'), fullPage: true });
        await click(entry(meal), 'Delete'); await click(page, 'Confirm deletion'); await expect(entry(meal)).toHaveCount(0);
        await page.goto(settings.baseUri + '/Ingredients');
        for (const name of [pasta, slices]) { await click(entry(name), 'Delete'); await click(page, 'Confirm deletion'); await expect(entry(name)).toHaveCount(0); }
        assert.deepEqual(errors, [], 'Browser reported an application error'); console.log('Mobile layout, name-only recipes, search and deletion verified.');
    } catch (error) {
        if (page && settings.artifacts) await page.screenshot({ path: path.join(settings.artifacts, 'recipe-failure.png'), fullPage: true });
        if (page) console.error('Browser test UI: ' + await page.locator('body').innerText()); throw error;
    } finally { await browser.close(); }
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
