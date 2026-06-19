import { Locator, Page } from '@playwright/test';

/** The top status bar: plugin / Redis / live-feed / cyclic indicators + Settings. */
export class HeaderBar {
  readonly root: Locator;
  readonly pluginStatus: Locator;
  readonly redisStatus: Locator;
  readonly liveStatus: Locator;
  readonly liveDot: Locator;
  readonly cyclicStatus: Locator;
  readonly settingsButton: Locator;

  constructor(private readonly page: Page) {
    this.root = page.getByTestId('header-bar');
    this.pluginStatus = page.getByTestId('plugin-status');
    this.redisStatus = page.getByTestId('redis-status');
    this.liveStatus = page.getByTestId('live-status');
    this.liveDot = page.getByTestId('live-dot');
    this.cyclicStatus = page.getByTestId('cyclic-status');
    this.settingsButton = page.getByRole('button', { name: 'Settings' });
  }

  async openSettings(): Promise<void> {
    await this.settingsButton.click();
  }

  /** True when the live-feed dot is showing the connected (ok) state. */
  async isLive(): Promise<boolean> {
    const cls = (await this.liveDot.getAttribute('class')) ?? '';
    return cls.includes('dot--ok');
  }
}
