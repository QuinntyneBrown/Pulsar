import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { interval } from 'rxjs';
import { ActivityService } from './activity.service';
import { ApiService } from './api.service';
import {
  CyclicJob, MessageCategory, MessageDetail, MessageSummary, PluginState,
  PublishActivity, TransportStatus,
} from './models';

export interface Toast { id: number; kind: 'ok' | 'error'; text: string; }
export interface MessageGroup { category: MessageCategory; messages: MessageSummary[]; }

const CATEGORY_ORDER: MessageCategory[] = ['Telemetry', 'Event', 'Fault', 'Command', 'Other'];
const MAX_ACTIVITY = 200;

/**
 * Single source of truth for the UI. Holds all view state as signals and is the
 * only place that talks to the API and activity services, so components stay
 * presentational.
 */
@Injectable({ providedIn: 'root' })
export class PulsarStore {
  private readonly api = inject(ApiService);
  private readonly activity = inject(ActivityService);

  readonly plugin = signal<PluginState>({ isLoaded: false, plugin: null });
  readonly messages = signal<MessageSummary[]>([]);
  readonly selectedKey = signal<string | null>(null);
  readonly selectedDetail = signal<MessageDetail | null>(null);
  readonly loadingDetail = signal(false);
  /** Advisory schema-mismatch messages for the current payload; empty when it matches (or no schema). */
  readonly schemaIssues = signal<string[]>([]);
  readonly connection = signal<TransportStatus>({ isConnected: false, endpoint: null, error: null });
  readonly jobs = signal<CyclicJob[]>([]);
  readonly activityLog = signal<PublishActivity[]>([]);
  readonly toast = signal<Toast | null>(null);
  readonly liveConnected = this.activity.connected;

  readonly groups = computed<MessageGroup[]>(() => {
    const all = this.messages();
    return CATEGORY_ORDER
      .map(category => ({ category, messages: all.filter(m => m.category === category) }))
      .filter(g => g.messages.length > 0);
  });

  readonly runningCount = computed(() => this.jobs().filter(j => j.state === 'Running').length);

  private toastSeq = 0;
  private activitySeq = 0;
  private started = false;

  /** Boots the store: opens the live feed and loads initial state. Idempotent. */
  start(): void {
    if (this.started) return;
    this.started = true;

    this.activity.start();
    this.activity.published$.subscribe(a => this.onPublished(a));
    this.activity.jobChanged$.subscribe(j => this.upsertJob(j));

    this.refreshPlugin();
    this.refreshConnection();
    this.refreshJobs();
    interval(5000).subscribe(() => this.refreshConnection());
    // Server is the single source of truth for job counters; poll while any job
    // runs so counts stay authoritative (and self-heal after an SSE reconnect).
    interval(1000).subscribe(() => { if (this.runningCount() > 0) this.refreshJobs(); });
  }

  // ---- plugin & messages --------------------------------------------------

  refreshPlugin(): void {
    this.api.getPlugin().subscribe({
      next: state => {
        this.plugin.set(state);
        if (state.isLoaded) this.refreshMessages();
        else { this.messages.set([]); this.selectedKey.set(null); this.selectedDetail.set(null); }
      },
      error: err => this.error(err),
    });
  }

  private refreshMessages(): void {
    this.api.getMessages().subscribe({
      next: messages => {
        this.messages.set(messages);
        const current = this.selectedKey();
        if (current && messages.some(m => m.key === current)) return;
        if (messages.length > 0) this.select(messages[0].key);
        else { this.selectedKey.set(null); this.selectedDetail.set(null); }
      },
      error: err => this.error(err),
    });
  }

  select(key: string): void {
    // No-op re-selection would re-fetch and hand the composer a fresh detail
    // object, whose effect would discard the user's unsaved edits.
    if (this.selectedKey() === key && this.selectedDetail()?.key === key) return;
    this.selectedKey.set(key);
    this.loadingDetail.set(true);
    this.api.getMessage(key).subscribe({
      next: detail => { this.selectedDetail.set(detail); this.loadingDetail.set(false); },
      error: err => { this.loadingDetail.set(false); this.error(err); },
    });
  }

  loadPlugin(path: string): void {
    this.api.loadPlugin(path).subscribe({
      next: info => {
        this.ok(`Loaded plugin "${info.name}" (${info.messageCount} messages).`);
        this.selectedKey.set(null);
        this.jobs.set([]);
        this.refreshPlugin();
      },
      error: err => this.error(err),
    });
  }

  unloadPlugin(): void {
    this.api.unloadPlugin().subscribe({
      next: () => { this.ok('Plugin unloaded.'); this.jobs.set([]); this.refreshPlugin(); },
      error: err => this.error(err),
    });
  }

  // ---- advisory validation ------------------------------------------------

  /**
   * Checks a payload against its message's schema and records any mismatches. This
   * is purely advisory — it NEVER prevents publishing; the composer surfaces the
   * result as a badge and always lets the user send anyway.
   */
  validatePayload(key: string, payloadJson: string): void {
    this.api.validateMessage(key, payloadJson).subscribe({
      next: result => this.schemaIssues.set(result.matches ? [] : result.messages),
      error: () => this.schemaIssues.set([]), // advisory: a failed check is silent
    });
  }

  clearSchemaIssues(): void { this.schemaIssues.set([]); }

  // ---- publishing ---------------------------------------------------------

  publishOnce(key: string, channel: string | null, payloadJson: string): void {
    this.api.publish({ key, channel, payloadJson }).subscribe({
      next: result => this.ok(`Published to ${result.channel} (${result.byteCount} bytes).`),
      error: err => this.error(err),
    });
  }

  startCyclic(key: string, channel: string | null, intervalMs: number, payloadJson: string): void {
    this.api.startCyclic({ key, channel, intervalMs, payloadJson }).subscribe({
      next: job => { this.upsertJob(job); this.ok(`Cyclic publishing started on ${job.channel} every ${job.intervalMs} ms.`); },
      error: err => this.error(err),
    });
  }

  // ---- cyclic jobs --------------------------------------------------------

  refreshJobs(): void {
    this.api.getJobs().subscribe({ next: jobs => this.jobs.set(jobs), error: () => {} });
  }

  stopJob(id: string): void {
    this.api.stopJob(id).subscribe({ next: job => this.upsertJob(job), error: err => this.error(err) });
  }

  removeJob(id: string): void {
    this.api.removeJob(id).subscribe({
      next: () => this.jobs.update(jobs => jobs.filter(j => j.id !== id)),
      error: err => this.error(err),
    });
  }

  // ---- connection ---------------------------------------------------------

  refreshConnection(): void {
    this.api.getConnection().subscribe({ next: status => this.connection.set(status), error: () => {} });
  }

  setConnection(connectionString: string): void {
    this.api.setConnection(connectionString).subscribe({
      next: status => {
        this.connection.set(status);
        if (status.isConnected) this.ok(`Connected to Redis at ${status.endpoint}.`);
        else this.error(status.error ?? 'Could not connect to Redis.');
      },
      error: err => this.error(err),
    });
  }

  // ---- live feed ----------------------------------------------------------

  clearActivity(): void { this.activityLog.set([]); }

  private onPublished(activity: PublishActivity): void {
    // Stamp a stable client id so the activity feed can track rows across head
    // inserts. Counters are NOT updated here — the server owns them (see start()).
    this.activityLog.update(log => [{ ...activity, _id: ++this.activitySeq }, ...log].slice(0, MAX_ACTIVITY));
  }

  private upsertJob(job: CyclicJob): void {
    this.jobs.update(jobs => {
      const idx = jobs.findIndex(j => j.id === job.id);
      if (idx === -1) return [...jobs, job];
      const next = jobs.slice();
      next[idx] = job;
      return next;
    });
  }

  // ---- toasts -------------------------------------------------------------

  private ok(text: string): void { this.pushToast('ok', text); }

  private error(err: unknown): void { this.pushToast('error', this.errorText(err)); }

  private pushToast(kind: 'ok' | 'error', text: string): void {
    const id = ++this.toastSeq;
    this.toast.set({ id, kind, text });
    setTimeout(() => { if (this.toast()?.id === id) this.toast.set(null); }, 4500);
  }

  private errorText(err: unknown): string {
    if (typeof err === 'string') return err;
    if (err instanceof HttpErrorResponse) {
      const body = err.error;
      if (body && typeof body === 'object' && 'error' in body) return String((body as { error: unknown }).error);
      if (typeof body === 'string' && body.length > 0) return body;
      return err.message;
    }
    return 'Unexpected error.';
  }
}
