import { expect, test } from '@playwright/test';

test.describe('Market and AI audit navigation/state', () => {
  test('sidebar navigation reaches opportunities and ai audit', async ({ page }) => {
    await page.goto('/jobs');

    await page.getByRole('link', { name: 'Opportunities' }).click();
    await expect(page).toHaveURL(/\/opportunities/);
    await expect(page.getByRole('heading', { level: 1, name: 'Market Opportunities' })).toBeVisible();

    await page.getByRole('link', { name: 'AI Audit' }).click();
    await expect(page).toHaveURL(/\/ai-audit/);
    await expect(page.getByRole('heading', { level: 1, name: 'AI Audit' })).toBeVisible();
  });

  test('opportunities restores and updates state from query params', async ({ page }) => {
    await page.goto('/opportunities?source=upwork&minOpportunityScore=40&minUrgencyScore=3&page=2');

    await expect(page.getByRole('textbox', { name: 'Opportunity source' })).toHaveValue('upwork');
    await expect(page.getByRole('spinbutton', { name: 'Min opportunity score' })).toHaveValue('40');
    await expect(page.getByRole('spinbutton', { name: 'Min urgency score' })).toHaveValue('3');
    await expect(page.getByText('Page 2 /')).toBeVisible();

    await page.getByRole('button', { name: 'Prev' }).click();

    await expect(page).toHaveURL(/\/opportunities\?source=upwork&minOpportunityScore=40&minUrgencyScore=3$/);
    await expect(page.getByText('Page 1 /')).toBeVisible();
  });

  test('ai audit restores and updates state from query params', async ({ page }) => {
    await page.goto('/ai-audit?provider=openai&status=success&windowDays=14&page=3');

    await expect(page.getByRole('textbox', { name: 'Audit provider' })).toHaveValue('openai');
    await expect(page.getByRole('textbox', { name: 'Audit status' })).toHaveValue('success');
    await expect(page.getByRole('spinbutton', { name: 'Audit window days' })).toHaveValue('14');
    await expect(page.getByText('Page 3 /')).toBeVisible();

    await page.getByRole('button', { name: 'Prev' }).click();
    await expect(page).toHaveURL(/\/ai-audit\?page=2&provider=openai&status=success&windowDays=14$/);

    await page.getByRole('button', { name: 'Prev' }).click();
    await expect(page).toHaveURL(/\/ai-audit\?provider=openai&status=success&windowDays=14$/);
    await expect(page.getByText('Page 1 /')).toBeVisible();
  });

  test('opportunities opens leads by selected pain point', async ({ page }) => {
    await page.route('**/api/market/opportunities**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              painPoint: 'API Scaling',
              painCategory: 'Scaling',
              opportunityCount: 12,
              avgOpportunityScore: 82,
              avgUrgencyScore: 7,
              topTechStack: '.NET, PostgreSQL',
              suggestedMvp: 'Performance Booster'
            }
          ],
          page: 1,
          pageSize: 12,
          totalCount: 1,
          totalPages: 1,
          sortBy: 'opportunityCount',
          sortDirection: 'desc'
        })
      });
    });

    await page.route('**/api/market/trends**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            painCategory: 'Scaling',
            currentCount: 12,
            previousCount: 8,
            trendPercentage: 50
          }
        ])
      });
    });

    await page.route('**/api/market/leads**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              jobId: 1,
              company: 'Acme',
              title: 'Senior .NET Engineer',
              painPoint: 'API Scaling',
              opportunityScore: 90,
              urgencyScore: 8,
              suggestedSolution: 'Performance MVP',
              leadMessage: 'Reach out message',
              source: 'upwork',
              url: 'https://example.com/job/1',
              capturedAt: '2026-04-10T00:00:00Z'
            }
          ],
          page: 1,
          pageSize: 8,
          totalCount: 1,
          totalPages: 1,
          sortBy: 'opportunityScore',
          sortDirection: 'desc'
        })
      });
    });

    await page.goto('/opportunities');

    const firstViewLeadsButton = page.getByRole('button', { name: 'Ver leads' }).first();
    await expect(firstViewLeadsButton).toBeVisible();
    await firstViewLeadsButton.click();

    await expect(page.getByText('Pain point activo:')).toBeVisible();
    await expect(page).toHaveURL(/\/opportunities\?.*painPoint=/);

    await page.getByRole('link', { name: 'Detalle interno' }).first().click();
    await expect(page).toHaveURL(/\/jobs\/1\?returnUrl=/);
    await expect(page.getByText('Vacante #1')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Volver a opportunities' })).toBeVisible();
  });
});
