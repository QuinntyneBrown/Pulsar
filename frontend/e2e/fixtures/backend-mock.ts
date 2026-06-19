import { Page, Route } from '@playwright/test';
import {
  CyclicJob, MessageDetail, MessageSummary, PluginState, PublishActivity, TransportStatus,
  SAMPLE_DETAILS, SAMPLE_MESSAGES, SAMPLE_PLUGIN, DISCONNECTED,
} from './data';

/** Mutable server state the mock serves over the HTTP routes. */
interface BackendState {
  plugin: PluginState;
  messages: MessageSummary[];
  details: Record<string, MessageDetail>;
  connection: TransportStatus;
  jobs: CyclicJob[];
}

interface ErrorSpec { status: number; body: unknown; once: boolean; }

/** What a POST /api/connection should do next. */
type ConnectBehavior = { kind: 'success' } | { kind: 'fail'; error: string };

/**
 * MockBackend stands in for the entire Pulsar backend at the transport boundary:
 *
 *   - Every `/api/**` HTTP call is intercepted via `page.route` and answered from
 *     in-memory state, mirroring the real controller semantics (load/unload,
 *     publish, cyclic CRUD, connection).
 *   - The Server-Sent Events stream is replaced by swapping `window.EventSource`
 *     for a controllable mock, so the test drives the live feed deterministically
 *     (`emitPublished`, `emitJobChanged`, `goLive`, `dropLive`).
 *
 * No real backend, Redis, or network is involved — the UI is exercised purely
 * through the contract it speaks.
 */
export class MockBackend {
  /** Live, mutable server state. Tests may read/tweak it directly when handy. */
  readonly state: BackendState;

  private readonly errors = new Map<string, ErrorSpec>();
  private connectBehavior: ConnectBehavior = { kind: 'success' };
  private jobSeq = 0;

  constructor(private readonly page: Page) {
    this.state = {
      plugin: { isLoaded: true, plugin: SAMPLE_PLUGIN },
      messages: clone(SAMPLE_MESSAGES),
      details: clone(SAMPLE_DETAILS),
      connection: { ...DISCONNECTED },
      jobs: [],
    };
  }

  /** Install the SSE replacement and HTTP interception. Call before navigation. */
  async install(): Promise<void> {
    await this.installSse();
    await this.installRoutes();
  }

  // ---- pre-navigation configuration --------------------------------------

  /** Start the app with no plugin loaded (empty catalog / composer prompts). */
  withNoPlugin(): this {
    this.state.plugin = { isLoaded: false, plugin: null };
    this.state.messages = [];
    return this;
  }

  /** Start with a loaded plugin that exposes zero messages. */
  withNoMessages(): this {
    this.state.messages = [];
    this.state.details = {};
    return this;
  }

  /** Seed the connection status the app sees on load. */
  withConnection(status: TransportStatus): this {
    this.state.connection = { ...status };
    return this;
  }

  /** Seed pre-existing cyclic jobs. */
  withJobs(jobs: CyclicJob[]): this {
    this.state.jobs = clone(jobs);
    return this;
  }

  // ---- error / behaviour injection ---------------------------------------

  /** Make the matching route fail until reset. Key e.g. 'POST publish'. */
  failOn(routeKey: string, status = 400, body: unknown = { error: 'Mocked failure.' }): this {
    this.errors.set(routeKey, { status, body, once: false });
    return this;
  }

  /** Make the matching route fail exactly once, then behave normally. */
  failOnce(routeKey: string, status = 400, body: unknown = { error: 'Mocked failure.' }): this {
    this.errors.set(routeKey, { status, body, once: true });
    return this;
  }

  /** Configure the outcome of the next POST /api/connection. */
  setConnectResult(behavior: ConnectBehavior): this {
    this.connectBehavior = behavior;
    return this;
  }

  // ---- server-push (SSE) drivers -----------------------------------------

  /** Push a `published` activity event over the live feed. */
  async emitPublished(activity: PublishActivity): Promise<void> {
    await this.emit('published', activity);
  }

  /**
   * Push a `jobChanged` event AND keep the polled job list consistent, so a
   * background refresh can't silently revert the pushed counters.
   */
  async emitJobChanged(job: CyclicJob): Promise<void> {
    this.upsertJob(job);
    await this.emit('jobChanged', job);
  }

  /** Simulate the live feed connecting (or reconnecting). */
  async goLive(): Promise<void> {
    await this.waitForSse();
    await this.page.evaluate(() => (window as unknown as SseWindow).__pulsarSse.open());
  }

  /** Simulate the live feed dropping. */
  async dropLive(): Promise<void> {
    await this.waitForSse();
    await this.page.evaluate(() => (window as unknown as SseWindow).__pulsarSse.fail());
  }

  /** Allocate a server-side id and register a new running job (test helper). */
  registerJob(job: CyclicJob): CyclicJob {
    const withId = { ...job, id: `job-${++this.jobSeq}` };
    this.state.jobs.push(withId);
    return withId;
  }

  // ---- internals ----------------------------------------------------------

  private async emit(type: 'published' | 'jobChanged', payload: unknown): Promise<void> {
    await this.waitForSse();
    await this.page.evaluate(
      ([t, json]) => (window as unknown as SseWindow).__pulsarSse.emit(t, json),
      [type, JSON.stringify(payload)] as const,
    );
  }

  private async waitForSse(): Promise<void> {
    await this.page.waitForFunction(
      () => Boolean((window as unknown as SseWindow).__pulsarSse?.instances.length),
    );
  }

  private async installSse(): Promise<void> {
    await this.page.addInitScript(() => {
      type Listener = (e: { data: string }) => void;
      const registry = {
        instances: [] as MockEventSource[],
        emit(type: string, json: string) { for (const es of registry.instances) es.__dispatch(type, json); },
        open() { for (const es of registry.instances) es.__open(); },
        fail() { for (const es of registry.instances) es.__error(); },
      };

      class MockEventSource {
        url: string;
        readyState = 0;
        onopen: ((e: unknown) => void) | null = null;
        onerror: ((e: unknown) => void) | null = null;
        onmessage: ((e: { data: string }) => void) | null = null;
        private listeners: Record<string, Listener[]> = {};

        constructor(url: string) {
          this.url = url;
          registry.instances.push(this);
          // Mimic a connection that succeeds on the next tick.
          setTimeout(() => this.__open(), 0);
        }
        addEventListener(type: string, handler: Listener) {
          (this.listeners[type] ||= []).push(handler);
        }
        removeEventListener(type: string, handler: Listener) {
          const arr = this.listeners[type];
          if (arr) this.listeners[type] = arr.filter(h => h !== handler);
        }
        close() { this.readyState = 2; }
        __open() { this.readyState = 1; this.onopen?.({ type: 'open' }); }
        __error() { this.readyState = 0; this.onerror?.({ type: 'error' }); }
        __dispatch(type: string, json: string) {
          const evt = { data: json };
          if (type === 'message') this.onmessage?.(evt);
          for (const h of this.listeners[type] || []) h(evt);
        }
      }

      (window as unknown as { EventSource: unknown }).EventSource = MockEventSource;
      (window as unknown as SseWindow).__pulsarSse = registry;
    });
  }

  private async installRoutes(): Promise<void> {
    await this.page.route('**/api/**', route => this.handle(route));
  }

  private async handle(route: Route): Promise<void> {
    const req = route.request();
    const method = req.method();
    const path = new URL(req.url()).pathname;

    // The live stream is served by the EventSource mock, never the network.
    if (path.endsWith('/api/activity/stream')) return route.fulfill({ status: 200, body: '' });

    const key = routeKey(method, path);

    const injected = this.errors.get(key);
    if (injected) {
      if (injected.once) this.errors.delete(key);
      return this.json(route, injected.status, injected.body);
    }

    const body = this.parseBody(req.postData());
    return this.dispatch(route, key, path, body);
  }

  private async dispatch(route: Route, key: string, path: string, body: any): Promise<void> {
    switch (key) {
      case 'GET plugin':
        return this.json(route, 200, this.state.plugin);

      case 'POST plugin/load': {
        const sourcePath = String(body?.path ?? SAMPLE_PLUGIN.sourcePath);
        const info = { ...SAMPLE_PLUGIN, sourcePath };
        this.state.plugin = { isLoaded: true, plugin: info };
        this.state.messages = clone(SAMPLE_MESSAGES);
        this.state.details = clone(SAMPLE_DETAILS);
        return this.json(route, 200, info);
      }

      case 'POST plugin/unload':
        this.state.plugin = { isLoaded: false, plugin: null };
        this.state.messages = [];
        this.state.jobs = [];
        return route.fulfill({ status: 200, body: '' });

      case 'GET messages':
        return this.json(route, 200, this.state.messages);

      case 'GET messages/:key': {
        const detail = this.state.details[lastSegment(path)];
        return detail
          ? this.json(route, 200, detail)
          : this.json(route, 404, { error: 'Message not found.' });
      }

      case 'POST publish': {
        const detail = this.state.details[body?.key];
        const channel = trimOr(body?.channel, detail?.defaultChannel ?? 'unknown');
        return this.json(route, 200, {
          channel,
          byteCount: byteLength(body?.payloadJson ?? ''),
          timestamp: new Date().toISOString(),
        });
      }

      case 'GET cyclic':
        return this.json(route, 200, this.state.jobs);

      case 'POST cyclic': {
        const detail = this.state.details[body?.key];
        const job: CyclicJob = {
          id: `job-${++this.jobSeq}`,
          messageKey: body?.key,
          displayName: detail?.displayName ?? body?.key,
          channel: trimOr(body?.channel, detail?.defaultChannel ?? 'unknown'),
          intervalMs: Number(body?.intervalMs) || 1000,
          state: 'Running',
          publishedCount: 0,
          failureCount: 0,
          lastError: null,
          startedAt: new Date().toISOString(),
          stoppedAt: null,
          lastPublishedAt: null,
        };
        this.state.jobs.push(job);
        return this.json(route, 200, job);
      }

      case 'POST cyclic/:id/stop': {
        const id = path.split('/').slice(-2)[0];
        const job = this.state.jobs.find(j => j.id === id);
        if (!job) return this.json(route, 404, { error: 'Job not found.' });
        job.state = 'Stopped';
        job.stoppedAt = new Date().toISOString();
        return this.json(route, 200, job);
      }

      case 'DELETE cyclic/:id': {
        const id = lastSegment(path);
        this.state.jobs = this.state.jobs.filter(j => j.id !== id);
        return route.fulfill({ status: 200, body: '' });
      }

      case 'GET connection':
        return this.json(route, 200, this.state.connection);

      case 'POST connection': {
        const cs = String(body?.connectionString ?? '');
        this.state.connection = this.connectBehavior.kind === 'success'
          ? { isConnected: true, endpoint: cs, error: null }
          : { isConnected: false, endpoint: null, error: this.connectBehavior.error };
        return this.json(route, 200, this.state.connection);
      }

      default:
        return this.json(route, 404, { error: `Unhandled route: ${key}` });
    }
  }

  private upsertJob(job: CyclicJob): void {
    const idx = this.state.jobs.findIndex(j => j.id === job.id);
    if (idx === -1) this.state.jobs.push(job);
    else this.state.jobs[idx] = job;
  }

  private parseBody(raw: string | null): any {
    if (!raw) return undefined;
    try { return JSON.parse(raw); } catch { return undefined; }
  }

  private json(route: Route, status: number, data: unknown): Promise<void> {
    return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) });
  }
}

interface SseWindow {
  __pulsarSse: {
    instances: unknown[];
    emit(type: string, json: string): void;
    open(): void;
    fail(): void;
  };
}

/** Collapse a concrete `/api/...` path + method into a stable route key. */
function routeKey(method: string, path: string): string {
  const segments = path.replace(/^\/api\/?/, '').split('/').filter(Boolean);
  let pattern: string;
  switch (segments[0]) {
    case 'messages':
      pattern = segments.length > 1 ? 'messages/:key' : 'messages';
      break;
    case 'cyclic':
      if (segments.length === 1) pattern = 'cyclic';
      else if (segments[2] === 'stop') pattern = 'cyclic/:id/stop';
      else pattern = 'cyclic/:id';
      break;
    default:
      pattern = segments.join('/');
  }
  return `${method} ${pattern}`;
}

function lastSegment(path: string): string {
  return decodeURIComponent(path.split('/').filter(Boolean).pop() ?? '');
}

function trimOr(value: unknown, fallback: string): string {
  const s = typeof value === 'string' ? value.trim() : '';
  return s.length > 0 ? s : fallback;
}

function byteLength(s: string): number {
  return new TextEncoder().encode(s).length;
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value));
}
