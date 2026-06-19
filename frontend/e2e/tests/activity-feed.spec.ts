import { test, expect } from '../fixtures/test';
import { makeActivity } from '../fixtures/data';

test.describe('Live activity feed', () => {
  test('starts empty', async ({ app }) => {
    await app.goto();
    await expect(app.activity.empty).toBeVisible();
    await expect(app.activity.rows).toHaveCount(0);
  });

  test('a published server event appears as a successful row', async ({ mock, app }) => {
    await app.goto();

    await mock.emitPublished(makeActivity({
      source: 'manual', displayName: 'Heartbeat', channel: 'telemetry.heartbeat', byteCount: 42,
    }));

    const row = app.activity.row(0);
    await expect(row.root).toBeVisible();
    await expect(row.source).toHaveText('manual');
    await expect(row.name).toHaveText('Heartbeat');
    await expect(row.channel).toHaveText('telemetry.heartbeat');
    await expect(row.bytes).toContainText('42B');
    expect(await row.isFailure()).toBe(false);
  });

  test('a failed publish event renders as a failure row with the error', async ({ mock, app }) => {
    await app.goto();

    await mock.emitPublished(makeActivity({
      success: false, error: 'Channel unavailable', byteCount: 0,
    }));

    const row = app.activity.row(0);
    expect(await row.isFailure()).toBe(true);
    await expect(row.error).toContainText('Channel unavailable');
  });

  test('newest events are prepended (most recent first)', async ({ mock, app }) => {
    await app.goto();

    await mock.emitPublished(makeActivity({ displayName: 'First', source: 'manual' }));
    await mock.emitPublished(makeActivity({ displayName: 'Second', source: 'cyclic' }));

    await expect(app.activity.rows).toHaveCount(2);
    await expect(app.activity.row(0).name).toHaveText('Second');
    await expect(app.activity.row(1).name).toHaveText('First');
    // cyclic-sourced rows are visually distinguished.
    await expect(app.activity.row(0).source).toHaveClass(/cyclic/);
  });

  test('Clear empties the feed', async ({ mock, app }) => {
    await app.goto();

    await mock.emitPublished(makeActivity());
    await expect(app.activity.rows).toHaveCount(1);

    await app.activity.clear();

    await expect(app.activity.rows).toHaveCount(0);
    await expect(app.activity.empty).toBeVisible();
  });
});
