import { expect, test } from '@playwright/test';

test.describe('Jobs UI E2E', () => {
  test('loads jobs page and renders cards', async ({ page }) => {
    await page.goto('/jobs');

    await expect(page.getByRole('heading', { level: 2 })).toContainText('search result');
    await expect(page.locator('article').first()).toBeVisible();
  });

  test('search by text updates visible results', async ({ page }) => {
    await page.goto('/jobs');

    const searchInput = page.getByRole('textbox', { name: 'Buscar por texto' });
    await searchInput.fill('react');
    await page.getByRole('button', { name: 'Search' }).click();

    await expect(page.getByRole('heading', { level: 2 })).toContainText("'react'");
    await expect(page.locator('article')).toHaveCount(7);
  });

  test('provider filter upwork limits source', async ({ page }) => {
    await page.goto('/jobs');

    await page.getByRole('radio', { name: 'Upwork' }).check();

    await expect(page.locator('article').first()).toBeVisible();

    await expect
      .poll(async () => {
        const cards = await page.locator('article').allTextContents();
        if (!cards.length) {
          return false;
        }

        return cards.every((text) => text.toLowerCase().includes('upwork'));
      })
      .toBe(true);
  });

  test('pagination next changes page indicator', async ({ page }) => {
    await page.goto('/jobs');

    await page.getByRole('button', { name: 'Search' }).click();
    const nextButton = page.getByRole('button', { name: 'Next' });

    await expect(nextButton).toBeEnabled();
    await nextButton.click();

    await expect(page.getByText('Showing page 2')).toBeVisible();
  });

  test('back from detail restores previous list page', async ({ page }) => {
    await page.goto('/jobs');

    const nextButton = page.getByRole('button', { name: 'Next' });
    await expect(nextButton).toBeEnabled();

    await nextButton.click();
    await expect(page.getByText('Showing page 2')).toBeVisible();

    await nextButton.click();
    await expect(page.getByText('Showing page 3')).toBeVisible();

    await nextButton.click();
    await expect(page.getByText('Showing page 4')).toBeVisible();

    await page.getByRole('link', { name: 'Details' }).first().click();
    await expect(page).toHaveURL(/\/jobs\/\d+$/);

    await page.goBack();
    await expect(page).toHaveURL(/\/jobs\?page=4$/);
    await expect(page.getByText('Showing page 4')).toBeVisible();
  });

  test('back from detail restores page 8 explicitly', async ({ page }) => {
    await page.goto('/jobs?page=8');

    await expect(page.getByText('Showing page 8')).toBeVisible();

    await page.getByRole('link', { name: 'Details' }).first().click();
    await expect(page).toHaveURL(/\/jobs\/\d+$/);

    await page.goBack();
    await expect(page).toHaveURL(/\/jobs\?page=8$/);
    await expect(page.getByText('Showing page 8')).toBeVisible();
  });
});
