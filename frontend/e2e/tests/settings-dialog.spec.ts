import { test, expect } from '../fixtures/test';

test.describe('Settings dialog', () => {
  test('opens from the header and closes via the ✕ button', async ({ app }) => {
    await app.goto();
    await expect(app.settings.dialog).toBeHidden();

    await app.openSettings();
    await expect(app.settings.dialog).toBeVisible();

    await app.settings.close();
    await expect(app.settings.dialog).toBeHidden();
  });

  test('closes when clicking the backdrop', async ({ app }) => {
    await app.goto();
    await app.openSettings();

    await app.settings.dismissByBackdrop();
    await expect(app.settings.dialog).toBeHidden();
  });

  test('stays open when clicking inside the dialog', async ({ app }) => {
    await app.goto();
    await app.openSettings();

    await app.settings.dialog.click();
    await expect(app.settings.dialog).toBeVisible();
  });
});
