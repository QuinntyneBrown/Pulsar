import { test, expect } from '../fixtures/test';

test.describe('Composer editing', () => {
  test('Format pretty-prints valid JSON', async ({ app }) => {
    await app.goto();

    await app.composer.setPayload('{"a":1,"b":2}');
    await app.composer.format();

    await expect(app.composer.payload).toHaveValue('{\n  "a": 1,\n  "b": 2\n}');
    await expect(app.composer.jsonError).toBeHidden();
  });

  test('Format on invalid JSON surfaces an inline error and leaves the text alone', async ({ app }) => {
    await app.goto();

    await app.composer.setPayload('{ not valid');
    await app.composer.format();

    await expect(app.composer.jsonError).toBeVisible();
    await expect(app.composer.jsonError).toContainText('Invalid JSON');
    await expect(app.composer.payload).toHaveValue('{ not valid');
  });

  test('Reset template restores the original payload and channel', async ({ app }) => {
    await app.goto();

    await app.composer.setChannel('custom.channel');
    await app.composer.setPayload('{ "changed": 1 }');

    await app.composer.resetTemplate();

    await expect(app.composer.channel).toHaveValue('telemetry.heartbeat');
    await expect(app.composer.payload).toHaveValue(/deviceId/);
  });

  test('a clearing inline error disappears once the JSON is valid again', async ({ app }) => {
    await app.goto();

    await app.composer.setPayload('nope');
    await app.composer.format();
    await expect(app.composer.jsonError).toBeVisible();

    await app.composer.setPayload('{ "ok": true }');
    await app.composer.format();
    await expect(app.composer.jsonError).toBeHidden();
  });
});
