import { Locator, Page } from '@playwright/test';

/** A single cyclic-job card, scoped to one job's locators. */
export class JobCard {
  readonly name: Locator;
  readonly state: Locator;
  readonly meta: Locator;
  readonly sent: Locator;
  readonly failed: Locator;
  readonly error: Locator;
  readonly stopButton: Locator;
  readonly removeButton: Locator;

  constructor(readonly root: Locator) {
    this.name = root.getByTestId('job-name');
    this.state = root.getByTestId('job-state');
    this.meta = root.getByTestId('job-meta');
    this.sent = root.getByTestId('job-sent');
    this.failed = root.getByTestId('job-failed');
    this.error = root.getByTestId('job-error');
    this.stopButton = root.getByTestId('job-stop');
    this.removeButton = root.getByTestId('job-remove');
  }

  async stop(): Promise<void> {
    await this.stopButton.click();
  }

  async remove(): Promise<void> {
    await this.removeButton.click();
  }
}

/** The cyclic-jobs panel (top-right). */
export class Jobs {
  readonly root: Locator;
  readonly runningChip: Locator;
  readonly empty: Locator;
  readonly cards: Locator;

  constructor(private readonly page: Page) {
    this.root = page.getByTestId('jobs');
    this.runningChip = page.getByTestId('jobs-running');
    this.empty = page.getByTestId('jobs-empty');
    this.cards = page.getByTestId('job');
  }

  /** Job card by server id (data-job-id). */
  card(id: string): JobCard {
    return new JobCard(this.page.locator(`[data-testid="job"][data-job-id="${id}"]`));
  }

  /** Job card by zero-based position. */
  at(index: number): JobCard {
    return new JobCard(this.cards.nth(index));
  }
}
