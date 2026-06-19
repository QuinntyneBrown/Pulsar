/**
 * jsdom does not implement EventSource, and {@link ActivityService} talks to the
 * native one directly. This fake stands in for it: it records the URL, exposes
 * the registered handlers, and lets a test synchronously drive `open`, `error`,
 * and named server-sent events.
 */
export class FakeEventSource {
  static last: FakeEventSource | undefined;

  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  readonly url: string;
  closed = false;
  private readonly listeners = new Map<string, ((e: MessageEvent) => void)[]>();

  constructor(url: string) {
    this.url = url;
    FakeEventSource.last = this;
  }

  addEventListener(type: string, fn: (e: MessageEvent) => void): void {
    const list = this.listeners.get(type) ?? [];
    list.push(fn);
    this.listeners.set(type, list);
  }

  close(): void {
    this.closed = true;
  }

  /** Test helper: fire a named SSE carrying `data` (serialised to JSON). */
  emit(type: string, data: unknown): void {
    const event = { data: JSON.stringify(data) } as MessageEvent;
    for (const fn of this.listeners.get(type) ?? []) fn(event);
  }
}

/** Installs {@link FakeEventSource} as the global EventSource; returns a restore fn. */
export function installFakeEventSource(): () => void {
  const original = (globalThis as { EventSource?: unknown }).EventSource;
  (globalThis as { EventSource?: unknown }).EventSource = FakeEventSource as unknown;
  FakeEventSource.last = undefined;
  return () => {
    (globalThis as { EventSource?: unknown }).EventSource = original;
  };
}
