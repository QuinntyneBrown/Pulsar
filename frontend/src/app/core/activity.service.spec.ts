import { TestBed } from '@angular/core/testing';
import { FakeEventSource, installFakeEventSource } from '../../testing/fake-event-source';
import { ActivityService } from './activity.service';
import { CyclicJob, PublishActivity } from './models';

describe('ActivityService', () => {
  let service: ActivityService;
  let restore: () => void;

  beforeEach(() => {
    restore = installFakeEventSource();
    TestBed.configureTestingModule({});
    service = TestBed.inject(ActivityService);
  });

  afterEach(() => restore());

  it('opens the SSE stream on start', () => {
    service.start();
    expect(FakeEventSource.last?.url).toBe('/api/activity/stream');
  });

  it('is idempotent — a second start reuses the open stream', () => {
    service.start();
    const first = FakeEventSource.last;
    service.start();
    expect(FakeEventSource.last).toBe(first);
  });

  it('reflects the connection lifecycle in the connected signal', () => {
    service.start();
    expect(service.connected()).toBe(false);

    FakeEventSource.last!.onopen!();
    expect(service.connected()).toBe(true);

    FakeEventSource.last!.onerror!();
    expect(service.connected()).toBe(false);
  });

  it('re-emits "published" events as parsed activity', () => {
    service.start();
    const seen: PublishActivity[] = [];
    service.published$.subscribe(a => seen.push(a));

    const activity: PublishActivity = {
      source: 'manual', messageKey: 'k', displayName: 'K', channel: 'c',
      byteCount: 7, success: true, error: null, timestamp: 't', jobId: null,
    };
    FakeEventSource.last!.emit('published', activity);

    expect(seen).toEqual([activity]);
  });

  it('re-emits "jobChanged" events as parsed jobs', () => {
    service.start();
    const seen: CyclicJob[] = [];
    service.jobChanged$.subscribe(j => seen.push(j));

    const job = { id: 'j1', state: 'Running' } as CyclicJob;
    FakeEventSource.last!.emit('jobChanged', job);

    expect(seen).toEqual([job]);
  });
});
