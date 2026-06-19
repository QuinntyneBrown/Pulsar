import { Locator, Page } from '@playwright/test';

/** The Settings modal: Redis connection + plugin load/unload. */
export class SettingsDialog {
  readonly backdrop: Locator;
  readonly dialog: Locator;
  readonly closeButton: Locator;
  readonly connectionInput: Locator;
  readonly connectButton: Locator;
  readonly connectionStatus: Locator;
  readonly pluginInput: Locator;
  readonly loadButton: Locator;
  readonly unloadButton: Locator;
  readonly pluginStatus: Locator;

  constructor(private readonly page: Page) {
    this.backdrop = page.getByTestId('settings-backdrop');
    this.dialog = page.getByTestId('settings-dialog');
    this.closeButton = page.getByTestId('settings-close');
    this.connectionInput = page.getByTestId('connection-input');
    this.connectButton = page.getByTestId('connection-connect');
    this.connectionStatus = page.getByTestId('connection-status');
    this.pluginInput = page.getByTestId('plugin-input');
    this.loadButton = page.getByTestId('plugin-load');
    this.unloadButton = page.getByTestId('plugin-unload');
    this.pluginStatus = page.getByTestId('plugin-settings-status');
  }

  async connect(connectionString: string): Promise<void> {
    await this.connectionInput.fill(connectionString);
    await this.connectButton.click();
  }

  async loadPlugin(path: string): Promise<void> {
    await this.pluginInput.fill(path);
    await this.loadButton.click();
  }

  async unloadPlugin(): Promise<void> {
    await this.unloadButton.click();
  }

  async close(): Promise<void> {
    await this.closeButton.click();
  }

  /** Dismiss by clicking the backdrop outside the dialog. */
  async dismissByBackdrop(): Promise<void> {
    // Click near the top-left corner of the backdrop, away from the centred dialog.
    await this.backdrop.click({ position: { x: 5, y: 5 } });
  }
}
