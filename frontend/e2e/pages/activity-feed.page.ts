import { Locator, Page } from '@playwright/test';

/** A single live-activity row, scoped to one row's locators. */
export class ActivityRow {
  readonly source: Locator;
  readonly name: Locator;
  readonly channel: Locator;
  readonly bytes: Locator;
  readonly error: Locator;

  constructor(readonly root: Locator) {
    this.source = root.getByTestId('activity-source');
    this.name = root.getByTestId('activity-name');
    this.channel = root.getByTestId('activity-channel');
    this.bytes = root.getByTestId('activity-bytes');
    this.error = root.getByTestId('activity-error');
  }

  /** True when this row is rendered as a failure. */
  async isFailure(): Promise<boolean> {
    const cls = (await this.root.getAttribute('class')) ?? '';
    return cls.includes('row--fail');
  }
}

/** The live activity feed (bottom-right). */
export class ActivityFeed {
  readonly root: Locator;
  readonly clearButton: Locator;
  readonly empty: Locator;
  readonly rows: Locator;

  constructor(private readonly page: Page) {
    this.root = page.getByTestId('activity-feed');
    this.clearButton = page.getByTestId('activity-clear');
    this.empty = page.getByTestId('activity-empty');
    this.rows = page.getByTestId('activity-row');
  }

  /** Row by zero-based position (newest first). */
  row(index: number): ActivityRow {
    return new ActivityRow(this.rows.nth(index));
  }

  async clear(): Promise<void> {
    await this.clearButton.click();
  }
}
