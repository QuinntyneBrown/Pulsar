import { test, expect } from '../fixtures/test';

test.describe('Redis connection', () => {
  test('connecting successfully updates settings status, header, and toast', async ({ app }) => {
    await app.goto();
    const settings = await app.openSettings();

    await expect(settings.connectionStatus).toContainText('Not connected');

    await settings.connect('redis-host:6379');

    await app.expectOkToast(/Connected to Redis at redis-host:6379/);
    await expect(settings.connectionStatus).toContainText('Connected');
    await expect(settings.connectionStatus).toContainText('redis-host:6379');
    await expect(app.header.redisStatus).toContainText('redis-host:6379');
  });

  test('a failed connection shows the error in settings and header stays offline', async ({ mock, app }) => {
    mock.setConnectResult({ kind: 'fail', error: 'Connection refused' });
    await app.goto();
    const settings = await app.openSettings();

    await settings.connect('bad-host:1234');

    await app.expectErrorToast('Connection refused');
    await expect(settings.connectionStatus).toContainText('Connection refused');
    await expect(app.header.redisStatus).toContainText('Redis offline');
  });

  test('the Connect button is disabled until a connection string is entered', async ({ app }) => {
    await app.goto();
    const settings = await app.openSettings();

    await settings.connectionInput.fill('');
    await expect(settings.connectButton).toBeDisabled();

    await settings.connectionInput.fill('localhost:6379');
    await expect(settings.connectButton).toBeEnabled();
  });
});
