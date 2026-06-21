import { test, expect } from '../fixtures/test';

test.describe('Catalog selection', () => {
  test('selecting a message updates the composer and the active highlight', async ({ app }) => {
    await app.goto();

    await app.catalog.select('SubsystemFault');

    await expect(app.composer.title).toHaveText('Subsystem Fault');
    await expect(app.composer.channel).toHaveValue('faults.subsystem');
    await expect(app.composer.payload).toHaveValue(/faultCode/);
    await expect(app.composer.category).toHaveText('Fault');

    expect(await app.catalog.isActive('SubsystemFault')).toBe(true);
    expect(await app.catalog.isActive('HeartbeatTelemetry')).toBe(false);
  });

  test('groups expose the right messages under each category', async ({ app }) => {
    await app.goto();

    const telemetry = app.catalog.group('Telemetry');
    await expect(telemetry.getByTestId('catalog-item')).toHaveCount(3);

    const event = app.catalog.group('Event');
    await expect(event.getByTestId('catalog-item')).toHaveCount(2);
    await expect(event).toContainText('Mode Changed');
  });

  test('re-selecting the active message preserves unsaved composer edits', async ({ app }) => {
    await app.goto();

    // Edit the payload, then click the already-active message again.
    await app.composer.setPayload('{ "edited": true }');
    await app.catalog.select('HeartbeatTelemetry');

    // The store guards no-op re-selection, so the edit is NOT discarded.
    await expect(app.composer.payload).toHaveValue('{ "edited": true }');
  });
});
