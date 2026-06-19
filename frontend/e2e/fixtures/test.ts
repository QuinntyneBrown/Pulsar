import { test as base } from '@playwright/test';
import { DashboardPage } from '../pages/dashboard.page';
import { MockBackend } from './backend-mock';

/**
 * Shared test fixtures:
 *
 *   - `mock` — the transport-boundary backend mock. It is fully installed (HTTP
 *     interception + SSE replacement) BEFORE the test body runs, so a test only
 *     needs to tweak state (`withNoPlugin()`, `failOn(...)`, etc.) and then call
 *     `app.goto()`. Route handlers read state live, so configuration before
 *     navigation is honoured.
 *   - `app` — the root Dashboard page object. It DEPENDS on `mock`, so the
 *     backend is always mocked even for tests that never reference `mock`
 *     directly (Playwright only instantiates fixtures a test mentions, and a
 *     test that hit the real, dead proxy would silently see an empty app).
 */
export const test = base.extend<{ mock: MockBackend; app: DashboardPage }>({
  mock: async ({ page }, use) => {
    const mock = new MockBackend(page);
    await mock.install();
    await use(mock);
  },
  app: async ({ mock, page }, use) => {
    void mock; // ensure the backend mock is installed before any navigation
    await use(new DashboardPage(page));
  },
});

export { expect } from '@playwright/test';
