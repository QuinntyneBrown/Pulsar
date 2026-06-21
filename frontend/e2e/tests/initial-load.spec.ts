import { test, expect } from '../fixtures/test';

test.describe('Initial load', () => {
  test('renders the catalog grouped, auto-selects the first message, and seeds the composer', async ({ mock, app }) => {
    await app.goto();

    // Catalog: all six messages, grouped in category order.
    await expect(app.catalog.items).toHaveCount(6);
    expect(await app.catalog.groupOrder()).toEqual(['Telemetry', 'Event', 'Fault']);

    // First message is auto-selected...
    expect(await app.catalog.isActive('HeartbeatTelemetry')).toBe(true);

    // ...and the composer is seeded from its detail.
    await expect(app.composer.title).toHaveText('Heartbeat');
    await expect(app.composer.messageType).toHaveText('Heartbeat');
    await expect(app.composer.channel).toHaveValue('telemetry.heartbeat');
    await expect(app.composer.payload).toHaveValue(/deviceId/);
    await expect(app.composer.category).toHaveText('Telemetry');
  });

  test('header reflects the loaded plugin and offline Redis', async ({ app }) => {
    await app.goto();

    await expect(app.header.pluginStatus).toContainText('Sample Messages');
    await expect(app.header.pluginStatus).toContainText('6 msgs');
    await expect(app.header.redisStatus).toContainText('Redis offline');
  });

  test('with no plugin loaded, catalog and composer show empty prompts', async ({ mock, app }) => {
    mock.withNoPlugin();
    await app.goto();

    await expect(app.catalog.emptyNoPlugin).toBeVisible();
    await expect(app.composer.emptyNoPlugin).toBeVisible();
    await expect(app.catalog.items).toHaveCount(0);
    await expect(app.header.pluginStatus).toContainText('No plugin');
  });

  test('with a plugin that exposes no messages, catalog shows the empty-messages state', async ({ mock, app }) => {
    mock.withNoMessages();
    await app.goto();

    await expect(app.catalog.emptyNoMessages).toBeVisible();
    await expect(app.composer.emptyNoSelection).toBeVisible();
  });
});
