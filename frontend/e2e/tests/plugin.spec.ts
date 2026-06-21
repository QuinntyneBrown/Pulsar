import { test, expect } from '../fixtures/test';

test.describe('Plugin lifecycle', () => {
  test('loading a plugin populates the catalog and updates every status surface', async ({ mock, app }) => {
    mock.withNoPlugin();
    await app.goto();

    const settings = await app.openSettings();
    await expect(settings.pluginStatus).toContainText('No plugin loaded');

    await settings.loadPlugin('plugins/manifest/pulsar.plugin.json');

    await app.expectOkToast(/Loaded plugin "Sample Messages" \(6 messages\)/);

    // Settings now shows the loaded plugin with an Unload affordance.
    await expect(settings.pluginStatus).toContainText('Sample Messages');
    await expect(settings.unloadButton).toBeVisible();

    // Header + catalog reflect the load.
    await expect(app.header.pluginStatus).toContainText('Sample Messages');
    await expect(app.catalog.items).toHaveCount(6);
  });

  test('unloading a plugin clears the catalog and resets status surfaces', async ({ app }) => {
    await app.goto();
    await expect(app.catalog.items).toHaveCount(6);

    const settings = await app.openSettings();
    await settings.unloadPlugin();

    await app.expectOkToast('Plugin unloaded.');
    await expect(settings.pluginStatus).toContainText('No plugin loaded');
    await expect(app.header.pluginStatus).toContainText('No plugin');
    await expect(app.catalog.emptyNoPlugin).toBeVisible();
  });

  test('a failed plugin load surfaces an error toast and leaves the catalog empty', async ({ mock, app }) => {
    mock.withNoPlugin();
    mock.failOn('POST plugin/load', 400, { error: 'Manifest not found.' });
    await app.goto();

    const settings = await app.openSettings();
    await settings.loadPlugin('plugins/missing/pulsar.plugin.json');

    await app.expectErrorToast('Manifest not found.');
    await expect(settings.pluginStatus).toContainText('No plugin loaded');
  });
});
