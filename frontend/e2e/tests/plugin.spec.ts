import { test, expect } from '../fixtures/test';

test.describe('Plugin lifecycle', () => {
  test('loading a plugin populates the catalog and updates every status surface', async ({ mock, app }) => {
    mock.withNoPlugin();
    await app.goto();

    const settings = await app.openSettings();
    await expect(settings.pluginStatus).toContainText('No plugin loaded');

    await settings.loadPlugin('plugins/Pulsar.SampleMessages.dll');

    await app.expectOkToast(/Loaded plugin "Pulsar\.SampleMessages" \(5 messages\)/);

    // Settings now shows the loaded plugin with an Unload affordance.
    await expect(settings.pluginStatus).toContainText('Pulsar.SampleMessages');
    await expect(settings.unloadButton).toBeVisible();

    // Header + catalog reflect the load.
    await expect(app.header.pluginStatus).toContainText('Pulsar.SampleMessages');
    await expect(app.catalog.items).toHaveCount(5);
  });

  test('unloading a plugin clears the catalog and resets status surfaces', async ({ app }) => {
    await app.goto();
    await expect(app.catalog.items).toHaveCount(5);

    const settings = await app.openSettings();
    await settings.unloadPlugin();

    await app.expectOkToast('Plugin unloaded.');
    await expect(settings.pluginStatus).toContainText('No plugin loaded');
    await expect(app.header.pluginStatus).toContainText('No plugin');
    await expect(app.catalog.emptyNoPlugin).toBeVisible();
  });

  test('a failed plugin load surfaces an error toast and leaves the catalog empty', async ({ mock, app }) => {
    mock.withNoPlugin();
    mock.failOn('POST plugin/load', 400, { error: 'Assembly not found.' });
    await app.goto();

    const settings = await app.openSettings();
    await settings.loadPlugin('plugins/Missing.dll');

    await app.expectErrorToast('Assembly not found.');
    await expect(settings.pluginStatus).toContainText('No plugin loaded');
  });
});
