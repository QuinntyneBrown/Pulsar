import { test, expect } from '../fixtures/test';
import { makeJob } from '../fixtures/data';

test.describe('Cyclic jobs', () => {
  test('a preset sets the interval and highlights the active preset', async ({ app }) => {
    await app.goto();

    await app.composer.clickPreset(5000);

    await expect(app.composer.interval).toHaveValue('5000');
    await expect(app.composer.preset(5000)).toHaveClass(/preset--on/);
  });

  test('starting a cyclic job adds a running card and updates the header count', async ({ app }) => {
    await app.goto();

    await app.composer.clickPreset(500);
    await app.composer.startCyclic();

    await app.expectOkToast(/Cyclic publishing started on telemetry\.heartbeat every 500 ms/);

    const card = app.jobs.at(0);
    await expect(card.root).toBeVisible();
    await expect(card.name).toHaveText('Heartbeat');
    await expect(card.state).toHaveText('Running');
    await expect(card.meta).toContainText('telemetry.heartbeat · every 500 ms');
    await expect(card.sent).toContainText('0');

    await expect(app.jobs.runningChip).toContainText('1 running');
    await expect(app.header.cyclicStatus).toContainText('1 cyclic');
    await expect(app.jobs.empty).toBeHidden();
  });

  test('an interval below the minimum is rejected before any request', async ({ app }) => {
    await app.goto();

    await app.composer.setInterval(5);
    await app.composer.startCyclic();

    await expect(app.composer.jsonError).toContainText('at least 10 ms');
    await expect(app.jobs.empty).toBeVisible();
    await expect(app.jobs.cards).toHaveCount(0);
  });

  test('stopping a running job flips it to Stopped and hides the running indicators', async ({ mock, app }) => {
    mock.withJobs([makeJob({ id: 'job-1', state: 'Running' })]);
    await app.goto();

    const card = app.jobs.card('job-1');
    await expect(card.state).toHaveText('Running');
    await expect(card.stopButton).toBeVisible();

    await card.stop();

    await expect(card.state).toHaveText('Stopped');
    await expect(card.stopButton).toBeHidden();
    await expect(app.header.cyclicStatus).toBeHidden();
  });

  test('removing a job deletes its card', async ({ mock, app }) => {
    mock.withJobs([makeJob({ id: 'job-1' })]);
    await app.goto();

    await expect(app.jobs.card('job-1').root).toBeVisible();

    await app.jobs.card('job-1').remove();

    await expect(app.jobs.card('job-1').root).toBeHidden();
    await expect(app.jobs.empty).toBeVisible();
  });

  test('a jobChanged server event updates the job counters in place', async ({ mock, app }) => {
    mock.withJobs([makeJob({ id: 'job-1', publishedCount: 0 })]);
    await app.goto();

    await mock.emitJobChanged(makeJob({
      id: 'job-1',
      publishedCount: 12,
      failureCount: 3,
      lastError: 'Redis timeout',
      lastPublishedAt: '2026-06-19T12:05:00.000Z',
    }));

    const card = app.jobs.card('job-1');
    await expect(card.sent).toContainText('12');
    await expect(card.failed).toContainText('3');
    await expect(card.error).toContainText('Redis timeout');
  });

  test('a failed start surfaces an error toast and creates no card', async ({ mock, app }) => {
    mock.failOn('POST cyclic', 400, { error: 'Cannot start cyclic job.' });
    await app.goto();

    await app.composer.startCyclic();

    await app.expectErrorToast('Cannot start cyclic job.');
    await expect(app.jobs.cards).toHaveCount(0);
  });
});
