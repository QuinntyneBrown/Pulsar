import { Injectable, NgZone, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import { CyclicJob, PublishActivity } from './models';

/**
 * Subscribes to the server's Server-Sent Events activity stream and re-exposes
 * its events as RxJS subjects plus a live-connection signal. Uses the native
 * browser EventSource — no third-party client, and automatic reconnection.
 */
@Injectable({ providedIn: 'root' })
export class ActivityService {
  private readonly zone = inject(NgZone);
  private source?: EventSource;

  readonly published$ = new Subject<PublishActivity>();
  readonly jobChanged$ = new Subject<CyclicJob>();
  readonly connected = signal(false);

  start(): void {
    if (this.source) return;

    const source = new EventSource('/api/activity/stream');

    source.onopen = () => this.zone.run(() => this.connected.set(true));
    source.onerror = () => this.zone.run(() => this.connected.set(false));

    source.addEventListener('published', e =>
      this.zone.run(() => this.published$.next(JSON.parse((e as MessageEvent).data))));
    source.addEventListener('jobChanged', e =>
      this.zone.run(() => this.jobChanged$.next(JSON.parse((e as MessageEvent).data))));

    this.source = source;
  }
}
