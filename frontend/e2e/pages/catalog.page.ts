import { Locator, Page } from '@playwright/test';

/** The left-hand message catalog: grouped, selectable message list. */
export class Catalog {
  readonly root: Locator;
  readonly emptyNoPlugin: Locator;
  readonly emptyNoMessages: Locator;
  readonly items: Locator;
  readonly groups: Locator;

  constructor(private readonly page: Page) {
    this.root = page.getByTestId('catalog');
    this.emptyNoPlugin = page.getByTestId('catalog-empty-noplugin');
    this.emptyNoMessages = page.getByTestId('catalog-empty-nomessages');
    this.items = page.getByTestId('catalog-item');
    this.groups = page.getByTestId('catalog-group');
  }

  /** A single catalog entry by its message key. */
  item(key: string): Locator {
    return this.page.locator(`[data-testid="catalog-item"][data-key="${key}"]`);
  }

  group(category: string): Locator {
    return this.page.locator(`[data-testid="catalog-group"][data-category="${category}"]`);
  }

  /** Ordered list of group category names as rendered. */
  async groupOrder(): Promise<string[]> {
    return this.groups.evaluateAll(els => els.map(e => e.getAttribute('data-category') ?? ''));
  }

  async select(key: string): Promise<void> {
    await this.item(key).click();
  }

  /** Whether the given message is the active (highlighted) one. */
  async isActive(key: string): Promise<boolean> {
    const cls = (await this.item(key).getAttribute('class')) ?? '';
    return cls.includes('item--active');
  }
}
