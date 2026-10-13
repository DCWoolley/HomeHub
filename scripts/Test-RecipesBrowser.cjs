// Run from Test-Deployment.ps1 -VerifyRegistration -VerifyBrowser.
// Input carries only the synthetic smoke-test account's cookies; never print it.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const path = require('node:path');

async function main() {
    let input = '';
    for await (const chunk of process.stdin) input += chunk;
    const settings = JSON.parse(input);
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let page;
    try {
        const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
        await context.addCookies(settings.cookies);
        page = await context.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        const card = name => page.locator('.card').filter({ has: page.getByText(name, { exact: true }) });
        async function contains(locator, text) { await locator.filter({ hasText: text }).waitFor(); }
        await page.goto(settings.baseUri + '/Ingredients');
        await page.locator('.ingredient-controls:not([disabled])').waitFor();
        await page.locator('#ingredient-name').waitFor({ state: 'visible' });
        await page.getByText('Add a unit', { exact: true }).click();
        await page.locator('#unit-name').fill('scoops');
        await page.getByRole('button', { name: 'Save unit', exact: true }).click();
        await contains(page.getByRole('status'), 'Unit ready to use.');
        console.log('Browser custom unit creation verified.');
        await page.locator('#ingredient-unit').selectOption({ label: 'grams' });
        await page.locator('#ingredient-name').fill('Browser pasta');
        await page.locator('#ingredient-amount').fill('500');
        await page.getByRole('button', { name: 'Save ingredient', exact: true }).click();
        await contains(page.getByRole('status'), 'Ingredient saved.');
        console.log('Browser ingredient creation verified.');
        await contains(card('Browser pasta'), 'Amount per package: 500 grams');
        await page.locator('#ingredient-name').fill(' browser pasta ');
        await page.locator('#ingredient-unit').selectOption({ label: 'grams' });
        await page.locator('#ingredient-amount').fill('500');
        await page.getByRole('button', { name: 'Save ingredient', exact: true }).click();
        await contains(page.getByRole('alert'), 'already exists');
        console.log('Browser duplicate ingredient validation verified.');
        await card('Browser pasta').getByRole('button', { name: 'Edit', exact: true }).click();
        await page.getByText('Edit ingredient name', { exact: true }).waitFor();
        await page.locator('#ingredient-name').fill('Browser wholemeal pasta');
        await page.getByRole('button', { name: 'Save ingredient', exact: true }).click();
        await contains(page.getByRole('status'), 'Ingredient saved.');
        await card('Browser wholemeal pasta').waitFor();
        console.log('Browser ingredient rename verified.');

        await page.goto(settings.baseUri + '/Meals');
        await page.locator('#meal-name').fill('Browser recipe meal');
        await page.getByRole('button', { name: 'Save meal', exact: true }).click();
        await card('Browser recipe meal').getByRole('link', { name: 'Recipe', exact: true }).click();
        await page.waitForURL('**/Recipe');
        const recipeUrl = page.url();
        await page.locator('#recipe-ingredient').selectOption({ label: 'Browser wholemeal pasta' });
        await page.getByText('Quantity (grams)', { exact: true }).waitFor();
        await contains(page.locator('#quantity-help'), 'Amount per package: 500 grams');
        await page.locator('#recipe-quantity').fill('250');
        await page.getByRole('button', { name: 'Add ingredient', exact: true }).click();
        await contains(page.getByRole('status'), 'Recipe saved.');
        await contains(card('Browser wholemeal pasta'), 'Quantity: 250 grams');
        console.log('Browser recipe addition verified.');
        await page.reload();
        await contains(card('Browser wholemeal pasta'), 'Quantity: 250 grams');
        await card('Browser wholemeal pasta').getByRole('button', { name: 'Edit quantity', exact: true }).click();
        await page.getByRole('button', { name: 'Save quantity', exact: true }).waitFor();
        await page.locator('#recipe-quantity').fill('125.5');
        await page.getByRole('button', { name: 'Save quantity', exact: true }).click();
        await contains(page.getByRole('status'), 'Recipe saved.');
        await contains(card('Browser wholemeal pasta'), 'Quantity: 125.5 grams');
        await page.locator('#recipe-ingredient').selectOption({ label: 'Browser wholemeal pasta' });
        await page.locator('#recipe-quantity').fill('0.5');
        await page.getByRole('button', { name: 'Add ingredient', exact: true }).click();
        await contains(page.getByRole('alert'), 'already in the recipe');
        await card('Browser wholemeal pasta').getByRole('button', { name: 'Edit quantity', exact: true }).click();
        await page.getByRole('button', { name: 'Save quantity', exact: true }).waitFor();
        await page.locator('#recipe-quantity').fill('0.00001');
        await page.getByRole('button', { name: 'Save quantity', exact: true }).click();
        await contains(page.getByRole('alert'), 'at most four decimal places');
        await contains(card('Browser wholemeal pasta'), 'Quantity: 125.5 grams');

        await page.goto(settings.baseUri + '/Ingredients');
        await card('Browser wholemeal pasta').getByRole('button', { name: 'Edit', exact: true }).click();
        await page.getByText('Edit ingredient name', { exact: true }).waitFor();
        await page.locator('#ingredient-unit').selectOption({ label: 'scoops' });
        await page.getByRole('button', { name: 'Save ingredient', exact: true }).click();
        await contains(page.getByRole('alert'), 'before changing its unit');
        await page.getByRole('button', { name: 'Cancel edit', exact: true }).click();
        await page.getByText('New ingredient name', { exact: true }).waitFor();
        await card('Browser wholemeal pasta').getByRole('button', { name: 'Delete', exact: true }).click();
        await page.getByRole('button', { name: 'Confirm deletion', exact: true }).click();
        await contains(page.getByRole('alert'), 'used in a recipe');
        await page.goto(recipeUrl);
        await page.locator('.recipe-controls:not([disabled])').waitFor();
        await page.getByRole('heading', { name: 'Browser recipe meal recipe', exact: true }).waitFor();
        await contains(card('Browser wholemeal pasta'), 'Quantity: 125.5 grams');
        if (settings.artifacts) await page.screenshot({ path: path.join(settings.artifacts, 'recipe-desktop.png'), fullPage: true });
        await page.setViewportSize({ width: 390, height: 844 });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true, 'Mobile recipe overflows horizontally');
        if (settings.artifacts) await page.screenshot({ path: path.join(settings.artifacts, 'recipe-mobile.png'), fullPage: true });
        await card('Browser wholemeal pasta').getByRole('button', { name: 'Remove', exact: true }).click();
        await contains(page.getByRole('status'), 'Ingredient removed from recipe.');
        await page.getByText('This meal has no recipe items yet.', { exact: false }).waitFor();
        await page.goto(settings.baseUri + '/Ingredients');
        await card('Browser wholemeal pasta').getByRole('button', { name: 'Delete', exact: true }).click();
        await page.getByRole('button', { name: 'Confirm deletion', exact: true }).click();
        await contains(page.getByRole('status'), 'Ingredient deleted.');
        assert.equal(await card('Browser wholemeal pasta').count(), 0);
        await page.goto(settings.baseUri + '/Meals');
        await card('Browser recipe meal').getByRole('button', { name: 'Delete', exact: true }).click();
        await page.getByRole('button', { name: 'Confirm deletion', exact: true }).click();
        await card('Browser recipe meal').waitFor({ state: 'detached' });
        assert.deepEqual(errors, [], 'Browser reported an application error');
        console.log('Browser custom unit creation, ingredient amounts, unit-based recipe add/edit/reload/remove, duplicate/precision validation, unit-change/deletion guards, and mobile layout verified.');
    } catch (error) {
        if (page && settings.artifacts) await page.screenshot({ path: path.join(settings.artifacts, 'recipe-failure.png'), fullPage: true });
        if (page) console.error('Browser test page: ' + await page.locator('main').innerText());
        throw error;
    } finally { await browser.close(); }
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
