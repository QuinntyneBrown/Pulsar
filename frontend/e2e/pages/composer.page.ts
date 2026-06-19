import { Locator, Page } from '@playwright/test';

/** The centre compose-and-publish panel. */
export class Composer {
  readonly root: Locator;
  readonly title: Locator;
  readonly category: Locator;
  readonly messageType: Locator;
  readonly channel: Locator;
  readonly payload: Locator;
  readonly jsonError: Locator;
  readonly formatButton: Locator;
  readonly resetButton: Locator;
  readonly sendButton: Locator;
  readonly interval: Locator;
  readonly startCyclicButton: Locator;
  readonly emptyNoPlugin: Locator;
  readonly emptyNoSelection: Locator;
  readonly loading: Locator;

  constructor(private readonly page: Page) {
    this.root = page.getByTestId('composer');
    this.title = page.getByTestId('composer-title');
    this.category = page.getByTestId('composer-category');
    this.messageType = page.getByTestId('composer-type');
    this.channel = page.getByTestId('composer-channel');
    this.payload = page.getByTestId('composer-payload');
    this.jsonError = page.getByTestId('composer-json-error');
    this.formatButton = page.getByTestId('composer-format');
    this.resetButton = page.getByTestId('composer-reset');
    this.sendButton = page.getByTestId('composer-send');
    this.interval = page.getByTestId('composer-interval');
    this.startCyclicButton = page.getByTestId('composer-start-cyclic');
    this.emptyNoPlugin = page.getByTestId('composer-empty-noplugin');
    this.emptyNoSelection = page.getByTestId('composer-empty-noselection');
    this.loading = page.getByTestId('composer-loading');
  }

  preset(ms: number): Locator {
    return this.page.locator(`[data-testid="composer-preset"][data-ms="${ms}"]`);
  }

  async setChannel(value: string): Promise<void> {
    await this.channel.fill(value);
  }

  async setPayload(value: string): Promise<void> {
    await this.payload.fill(value);
  }

  async setInterval(ms: number): Promise<void> {
    await this.interval.fill(String(ms));
  }

  async format(): Promise<void> {
    await this.formatButton.click();
  }

  async resetTemplate(): Promise<void> {
    await this.resetButton.click();
  }

  async send(): Promise<void> {
    await this.sendButton.click();
  }

  async clickPreset(ms: number): Promise<void> {
    await this.preset(ms).click();
  }

  async startCyclic(): Promise<void> {
    await this.startCyclicButton.click();
  }
}
