import { Locator, Page, expect } from '@playwright/test';
import { ActivityFeed } from './activity-feed.page';
import { Catalog } from './catalog.page';
import { Composer } from './composer.page';
import { HeaderBar } from './header-bar.page';
import { Jobs } from './jobs.page';
import { SettingsDialog } from './settings.page';

/**
 * Root page object for the single-page dashboard. Aggregates the per-panel page
 * objects and owns cross-cutting concerns (navigation, the toast).
 */
export class DashboardPage {
  readonly header: HeaderBar;
  readonly catalog: Catalog;
  readonly composer: Composer;
  readonly jobs: Jobs;
  readonly activity: ActivityFeed;
  readonly settings: SettingsDialog;
  readonly toast: Locator;

  constructor(readonly page: Page) {
    this.header = new HeaderBar(page);
    this.catalog = new Catalog(page);
    this.composer = new Composer(page);
    this.jobs = new Jobs(page);
    this.activity = new ActivityFeed(page);
    this.settings = new SettingsDialog(page);
    this.toast = page.getByTestId('toast');
  }

  /** Navigate to the app and wait until the shell and initial data have settled. */
  async goto(): Promise<void> {
    await this.page.goto('/');
    await expect(this.header.root).toBeVisible();
    await expect(this.catalog.root).toBeVisible();
    await expect(this.composer.root).toBeVisible();
    await this.waitForInitialData();
  }

  /** Open the Settings modal and wait for it to appear. */
  async openSettings(): Promise<SettingsDialog> {
    await this.header.openSettings();
    await expect(this.settings.dialog).toBeVisible();
    return this.settings;
  }

  /** Assert the current toast is a success toast containing `text`. */
  async expectOkToast(text: string | RegExp): Promise<void> {
    await expect(this.toast).toHaveAttribute('data-kind', 'ok');
    await expect(this.toast).toContainText(text);
  }

  /** Assert the current toast is an error toast containing `text`. */
  async expectErrorToast(text: string | RegExp): Promise<void> {
    await expect(this.toast).toHaveAttribute('data-kind', 'error');
    await expect(this.toast).toContainText(text);
  }

  private async waitForInitialData(): Promise<void> {
    await expect(this.page.locator([
      '[data-testid="catalog-item"]',
      '[data-testid="catalog-empty-noplugin"]',
      '[data-testid="catalog-empty-nomessages"]',
    ].join(', ')).first()).toBeVisible();

    await expect(this.page.locator([
      '[data-testid="composer-payload"]',
      '[data-testid="composer-empty-noplugin"]',
      '[data-testid="composer-empty-noselection"]',
    ].join(', ')).first()).toBeVisible();
  }
}
