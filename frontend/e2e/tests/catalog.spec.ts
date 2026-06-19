import { test, expect } from '../fixtures/test';

test.describe('Catalog selection', () => {
  test('selecting a message updates the composer and the active highlight', async ({ app }) => {
    await app.goto();

    await app.catalog.select('overheat');

    await expect(app.composer.title).toHaveText('Overheat Alarm');
    await expect(app.composer.channel).toHaveValue('faults.overheat');
    await expect(app.composer.payload).toHaveValue(/celsius/);
    await expect(app.composer.category).toHaveText('Fault');

    expect(await app.catalog.isActive('overheat')).toBe(true);
    expect(await app.catalog.isActive('heartbeat')).toBe(false);
  });

  test('groups expose the right messages under each category', async ({ app }) => {
    await app.goto();

    const telemetry = app.catalog.group('Telemetry');
    await expect(telemetry.getByTestId('catalog-item')).toHaveCount(2);

    const command = app.catalog.group('Command');
    await expect(command.getByTestId('catalog-item')).toHaveCount(1);
    await expect(command).toContainText('Reboot Command');
  });

  test('re-selecting the active message preserves unsaved composer edits', async ({ app }) => {
    await app.goto();

    // Edit the payload, then click the already-active message again.
    await app.composer.setPayload('{ "edited": true }');
    await app.catalog.select('heartbeat');

    // The store guards no-op re-selection, so the edit is NOT discarded.
    await expect(app.composer.payload).toHaveValue('{ "edited": true }');
  });
});
