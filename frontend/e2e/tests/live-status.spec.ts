import { test, expect } from '../fixtures/test';

test.describe('Live feed status indicator', () => {
  test('shows connected once the SSE stream opens', async ({ app }) => {
    await app.goto();
    await expect(app.header.liveDot).toHaveClass(/dot--ok/);
  });

  test('drops to offline when the stream errors, and recovers on reconnect', async ({ mock, app }) => {
    await app.goto();
    await expect(app.header.liveDot).toHaveClass(/dot--ok/);

    await mock.dropLive();
    await expect(app.header.liveDot).toHaveClass(/dot--off/);
    await expect(app.header.liveDot).not.toHaveClass(/dot--ok/);

    await mock.goLive();
    await expect(app.header.liveDot).toHaveClass(/dot--ok/);
  });
});
