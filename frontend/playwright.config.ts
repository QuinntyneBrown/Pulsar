import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright config for the Pulsar frontend.
 *
 * The backend is fully mocked at the transport boundary (see e2e/fixtures), so
 * the webServer only needs the Angular dev server — no .NET API or Redis. The
 * dev-server proxy to /api is never exercised because every /api call is
 * intercepted in the browser before it leaves the page.
 */
export default defineConfig({
  testDir: './e2e/tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 1,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  // Headroom for the Vite-backed dev server: the first request to a route can
  // trigger an on-demand transform, and parallel workers share one server.
  timeout: 60_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    command: 'npm start -- --port 4200',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    env: { NG_CLI_ANALYTICS: 'false' },
  },
});
