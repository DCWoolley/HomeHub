const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const path = require('node:path');

module.exports = async function verifyCatalogue(baseUri) {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let diagnosticPage;
    try {
        const owner = await (await browser.newContext()).newPage();
        const member = await (await browser.newContext()).newPage();
        const stranger = await (await browser.newContext()).newPage();
        const anonymous = await (await browser.newContext()).newPage();
        const errors = [];
        for (const page of [owner, member, stranger]) page.on('pageerror', e => errors.push(e.message));
        const card = (page, name) => page.locator('.card').filter({ has: page.getByText(name, { exact: true }) });
        const ready = async page => { diagnosticPage = page; await page.locator('.household-item-controls:not([disabled])').waitFor(); };
        const status = (page, text) => page.getByRole('status').filter({ hasText: text }).waitFor();
        async function register(page, name, code) {
            await page.goto(baseUri + '/Account/Register');
            await page.locator('#name').fill(name);
            await page.locator('#email').fill(name + '@example.invalid');
            await page.locator('#password').fill('Test!8aA_household');
            await page.locator('#confirm').fill('Test!8aA_household');
            if (code) { await page.locator('#choice').selectOption('Join'); await page.locator('#code').fill(code); }
            else await page.locator('#household').fill(name + ' household');
            await page.getByRole('button', { name: 'Create account', exact: true }).click();
            await page.waitForURL('**/Household');
        }
        await register(owner, 'catalogue-owner');
        await owner.goto(baseUri + '/HouseholdItems');
        await ready(owner);
        await owner.getByText('No household items yet.', { exact: false }).waitFor();
        console.log('Catalogue empty state reached.');
        for (const label of ['rolls', 'bottles', 'tablets', 'bags', 'ml', 'litres'])
            assert.equal(await owner.locator('datalist option').filter({ hasText: label }).count() > 0 || await owner.locator(`datalist option[value="${label}"]`).count() > 0, true);
        await owner.locator('#household-item-name').fill('Toilet roll');
        await owner.locator('#household-item-unit').fill('rolls');
        await owner.getByRole('button', { name: 'Save household item', exact: true }).click();
        await status(owner, 'Household item saved.');
        await card(owner, 'Toilet roll').getByText('Usage unit: rolls', { exact: true }).waitFor();
        await owner.reload(); await ready(owner);
        await card(owner, 'Toilet roll').getByRole('button', { name: 'Edit', exact: true }).click();
        await owner.getByText('Edit household item name', { exact: true }).waitFor();
        await owner.locator('#household-item-name').fill('Soft toilet roll');
        await owner.locator('#household-item-unit').fill('sheets');
        await owner.getByRole('button', { name: 'Save household item', exact: true }).click();
        await status(owner, 'Household item saved.');
        await owner.goto(baseUri + '/Dashboard');
        await owner.getByRole('link', { name: 'Household items', exact: true }).click();
        await ready(owner);
        await card(owner, 'Soft toilet roll').getByText('Usage unit: sheets', { exact: true }).waitFor();
        await card(owner, 'Soft toilet roll').getByRole('button', { name: 'Edit', exact: true }).click();
        await owner.getByText('Edit household item name', { exact: true }).waitFor();
        await owner.locator('#household-item-name').fill('Discarded name');
        await owner.getByRole('button', { name: 'Cancel edit', exact: true }).click();
        await status(owner, 'Edit cancelled.');
        await card(owner, 'Soft toilet roll').waitFor();
        await owner.locator('#household-item-name').fill(' soft toilet roll ');
        await owner.locator('#household-item-unit').fill('sheets');
        await owner.getByRole('button', { name: 'Save household item', exact: true }).click();
        await owner.getByRole('alert').filter({ hasText: 'already exists' }).waitFor();
        await owner.locator('#household-item-name').fill('Shampoo');
        await owner.locator('#household-item-unit').fill(' ');
        await owner.getByRole('button', { name: 'Save household item', exact: true }).click();
        await owner.getByText('Enter a usage unit', { exact: false }).waitFor();
        await owner.reload(); await ready(owner);
        if (process.env.HOMEHUB_BROWSER_ARTIFACTS) await owner.screenshot({ path: path.join(process.env.HOMEHUB_BROWSER_ARTIFACTS, 'household-items-desktop.png'), fullPage: true });
        await owner.setViewportSize({ width: 390, height: 844 });
        assert.equal(await owner.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'Catalogue overflows narrow viewport');
        if (process.env.HOMEHUB_BROWSER_ARTIFACTS) await owner.screenshot({ path: path.join(process.env.HOMEHUB_BROWSER_ARTIFACTS, 'household-items-mobile.png'), fullPage: true });
        await owner.setViewportSize({ width: 1280, height: 900 });
        console.log('Catalogue create/edit/reload/custom units/cancel/validation/mobile passed.');
        await owner.goto(baseUri + '/Household/Invitations');
        await owner.getByRole('button', { name: 'Create invitation', exact: true }).click();
        await owner.locator('main code').waitFor();
        const code = await owner.locator('main code').innerText();
        await register(member, 'catalogue-member', code);
        await member.goto(baseUri + '/HouseholdItems'); await ready(member);
        await card(member, 'Soft toilet roll').getByRole('button', { name: 'Edit', exact: true }).click();
        await member.getByText('Edit household item name', { exact: true }).waitFor();
        await member.locator('#household-item-name').fill('Shared toilet roll');
        await member.getByRole('button', { name: 'Save household item', exact: true }).click();
        await status(member, 'Household item saved.');
        await owner.goto(baseUri + '/HouseholdItems'); await ready(owner);
        await card(owner, 'Shared toilet roll').waitFor();
        await register(stranger, 'catalogue-stranger');
        await stranger.goto(baseUri + '/HouseholdItems'); await ready(stranger);
        assert.equal(await card(stranger, 'Shared toilet roll').count(), 0);
        await stranger.getByText('No household items yet.', { exact: false }).waitFor();
        await anonymous.goto(baseUri + '/HouseholdItems');
        await anonymous.waitForURL('**/Account/Login?**');
        console.log('Catalogue sharing, household isolation and authentication passed.');
        // Keep an edit form open in the member's existing server circuit during revocation.
        await card(member, 'Shared toilet roll').getByRole('button', { name: 'Edit', exact: true }).click();
        await member.getByText('Edit household item name', { exact: true }).waitFor();
        await member.locator('#household-item-name').fill('Revoked edit');
        await owner.goto(baseUri + '/Household/Accounts');
        await card(owner, 'catalogue-member').getByRole('button', { name: 'Remove access', exact: true }).click();
        await owner.getByRole('button', { name: 'Confirm removal', exact: true }).click();
        await card(owner, 'catalogue-member').waitFor({ state: 'detached' });
        // Either the operation is explicitly rejected or the layout already redirected the revoked circuit.
        if (!member.url().includes('/Onboarding')) {
            await member.getByRole('button', { name: 'Save household item', exact: true }).click().catch(() => {});
            await Promise.race([
                member.getByRole('alert').filter({ hasText: 'Create or join a household' }).waitFor(),
                member.waitForURL('**/Household/Onboarding')
            ]);
        }
        await member.goto(baseUri + '/HouseholdItems');
        await member.waitForURL('**/Household/Onboarding');
        await owner.goto(baseUri + '/HouseholdItems'); await ready(owner);
        await card(owner, 'Shared toilet roll').waitFor();
        assert.equal(await card(owner, 'Revoked edit').count(), 0);
        assert.deepEqual(errors, []);
        console.log('Already-open member session cannot save or reread after removal.');
    } catch (error) {
        if (diagnosticPage) console.error('Browser failure page: ' + await diagnosticPage.locator('body').innerText());
        throw error;
    } finally { await browser.close(); }
};
