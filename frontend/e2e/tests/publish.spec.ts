import { test, expect } from '../fixtures/test';

test.describe('Publish once', () => {
  test('publishes to the default channel and shows a success toast', async ({ app }) => {
    await app.goto();

    await app.composer.send();

    await app.expectOkToast(/Published to telemetry\.heartbeat/);
    await app.expectOkToast(/bytes/);
  });

  test('publishes to a user-edited channel', async ({ app }) => {
    await app.goto();

    await app.composer.setChannel('custom.events');
    await app.composer.send();

    await app.expectOkToast(/Published to custom\.events/);
  });

  test('a server-side publish failure surfaces an error toast', async ({ mock, app }) => {
    mock.failOn('POST publish', 400, { error: 'Adapter rejected payload.' });
    await app.goto();

    await app.composer.send();

    await app.expectErrorToast('Adapter rejected payload.');
  });

  test('invalid JSON blocks the publish and shows an inline error (no request sent)', async ({ app }) => {
    await app.goto();

    await app.composer.setPayload('{ broken');
    await app.composer.send();

    await expect(app.composer.jsonError).toContainText('Invalid JSON');
    await expect(app.toast).toBeHidden();
  });
});
