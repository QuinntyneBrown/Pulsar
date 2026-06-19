import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { ActivityService } from './activity.service';
import { ApiService } from './api.service';
import { CyclicJob, MessageDetail, MessageSummary, PluginState, PublishActivity, PublishResult, TransportStatus } from './models';
import { PulsarStore } from './pulsar.store';

type ApiMock = { [K in keyof ApiService]: jest.Mock };

function createApiMock(): ApiMock {
  return {
    getPlugin: jest.fn(), loadPlugin: jest.fn(), unloadPlugin: jest.fn(),
    getMessages: jest.fn(), getMessage: jest.fn(), validateMessage: jest.fn(), publish: jest.fn(),
    getJobs: jest.fn(), startCyclic: jest.fn(), stopJob: jest.fn(),
    removeJob: jest.fn(), getConnection: jest.fn(), setConnection: jest.fn(),
  } as unknown as ApiMock;
}

function msg(key: string, category: MessageSummary['category']): MessageSummary {
  return { key, displayName: key, category, defaultChannel: `chan.${key}` };
}

function job(id: string, state: CyclicJob['state'] = 'Running'): CyclicJob {
  return {
    id, messageKey: 'k', displayName: id, channel: 'c', intervalMs: 1000, state,
    publishedCount: 0, failureCount: 0, lastError: null, startedAt: 't',
    stoppedAt: null, lastPublishedAt: null,
  };
}

describe('PulsarStore', () => {
  let store: PulsarStore;
  let api: ApiMock;
  let activity: { published$: Subject<PublishActivity>; jobChanged$: Subject<CyclicJob>; connected: ReturnType<typeof signal<boolean>>; start: jest.Mock };

  beforeEach(() => {
    api = createApiMock();
    activity = {
      published$: new Subject<PublishActivity>(),
      jobChanged$: new Subject<CyclicJob>(),
      connected: signal(false),
      start: jest.fn(),
    };
    // Safe defaults so start() can run without undefined observables.
    api.getPlugin.mockReturnValue(of<PluginState>({ isLoaded: false, plugin: null }));
    api.getMessages.mockReturnValue(of<MessageSummary[]>([]));
    api.getConnection.mockReturnValue(of<TransportStatus>({ isConnected: false, endpoint: null, error: null }));
    api.getJobs.mockReturnValue(of<CyclicJob[]>([]));

    TestBed.configureTestingModule({
      providers: [
        PulsarStore,
        { provide: ApiService, useValue: api },
        { provide: ActivityService, useValue: activity },
      ],
    });
    store = TestBed.inject(PulsarStore);
  });

  describe('derived state', () => {
    it('groups messages by category order and drops empty categories', () => {
      store.messages.set([msg('a', 'Command'), msg('b', 'Telemetry'), msg('c', 'Telemetry')]);
      const groups = store.groups();
      expect(groups.map(g => g.category)).toEqual(['Telemetry', 'Command']);
      expect(groups[0].messages.map(m => m.key)).toEqual(['b', 'c']);
    });

    it('counts only running jobs', () => {
      store.jobs.set([job('1', 'Running'), job('2', 'Stopped'), job('3', 'Running')]);
      expect(store.runningCount()).toBe(2);
    });

    it('exposes the activity service connection signal as liveConnected', () => {
      expect(store.liveConnected()).toBe(false);
      activity.connected.set(true);
      expect(store.liveConnected()).toBe(true);
    });
  });

  describe('plugin & messages', () => {
    it('refreshPlugin loads messages when a plugin is present', () => {
      api.getPlugin.mockReturnValue(of<PluginState>({ isLoaded: true, plugin: { name: 'P', sourcePath: 'p.dll', loadedAt: 't', messageCount: 1 } }));
      api.getMessages.mockReturnValue(of([msg('a', 'Event')]));
      api.getMessage.mockReturnValue(of({ ...msg('a', 'Event'), messageType: 'T', templateJson: '{}', hasSchema: false } as MessageDetail));

      store.refreshPlugin();

      expect(store.plugin().isLoaded).toBe(true);
      expect(store.messages().map(m => m.key)).toEqual(['a']);
      expect(store.selectedKey()).toBe('a');
    });

    it('refreshPlugin clears state when no plugin is loaded', () => {
      store.messages.set([msg('a', 'Event')]);
      store.selectedKey.set('a');
      api.getPlugin.mockReturnValue(of<PluginState>({ isLoaded: false, plugin: null }));

      store.refreshPlugin();

      expect(store.messages()).toEqual([]);
      expect(store.selectedKey()).toBeNull();
      expect(store.selectedDetail()).toBeNull();
    });

    it('refreshPlugin surfaces errors as a toast', () => {
      api.getPlugin.mockReturnValue(throwError(() => new HttpErrorResponse({ error: { error: 'nope' }, status: 500 })));
      store.refreshPlugin();
      expect(store.toast()).toMatchObject({ kind: 'error', text: 'nope' });
    });

    it('select fetches the detail and clears the loading flag', () => {
      const detail = { ...msg('a', 'Event'), messageType: 'T', templateJson: '{}', hasSchema: false } as MessageDetail;
      api.getMessage.mockReturnValue(of(detail));

      store.select('a');

      expect(api.getMessage).toHaveBeenCalledWith('a');
      expect(store.selectedDetail()).toEqual(detail);
      expect(store.loadingDetail()).toBe(false);
    });

    it('select is a no-op when the same key is already loaded', () => {
      const detail = { ...msg('a', 'Event'), messageType: 'T', templateJson: '{}', hasSchema: false } as MessageDetail;
      store.selectedKey.set('a');
      store.selectedDetail.set(detail);

      store.select('a');

      expect(api.getMessage).not.toHaveBeenCalled();
    });

    it('loadPlugin reports success and re-reads plugin state', () => {
      api.loadPlugin.mockReturnValue(of({ name: 'Sample', sourcePath: 'x.dll', loadedAt: 't', messageCount: 3 }));
      store.loadPlugin('x.dll');
      expect(store.toast()).toMatchObject({ kind: 'ok' });
      expect(api.getPlugin).toHaveBeenCalled();
    });
  });

  describe('advisory validation', () => {
    it('validatePayload records schema mismatch messages', () => {
      api.validateMessage.mockReturnValue(of({ matches: false, messages: ['$.status: bad'] }));
      store.validatePayload('a', '{"status":"x"}');
      expect(store.schemaIssues()).toEqual(['$.status: bad']);
    });

    it('validatePayload clears issues when the payload matches', () => {
      store.schemaIssues.set(['stale']);
      api.validateMessage.mockReturnValue(of({ matches: true, messages: [] }));
      store.validatePayload('a', '{}');
      expect(store.schemaIssues()).toEqual([]);
    });

    it('a failed validate call is silent (issues cleared, no toast)', () => {
      store.schemaIssues.set(['stale']);
      api.validateMessage.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
      store.validatePayload('a', '{}');
      expect(store.schemaIssues()).toEqual([]);
      expect(store.toast()).toBeNull();
    });

    it('clearSchemaIssues empties the list', () => {
      store.schemaIssues.set(['x']);
      store.clearSchemaIssues();
      expect(store.schemaIssues()).toEqual([]);
    });
  });

  describe('publishing', () => {
    it('publishOnce toasts the resulting channel and byte count', () => {
      api.publish.mockReturnValue(of<PublishResult>({ channel: 'demo', byteCount: 42, timestamp: 't' }));
      store.publishOnce('k', 'demo', '{}');
      expect(store.toast()).toMatchObject({ kind: 'ok', text: 'Published to demo (42 bytes).' });
    });

    it('startCyclic upserts the returned job', () => {
      api.startCyclic.mockReturnValue(of(job('j1')));
      store.startCyclic('k', 'c', 1000, '{}');
      expect(store.jobs().map(j => j.id)).toEqual(['j1']);
    });
  });

  describe('cyclic jobs', () => {
    it('refreshJobs replaces the jobs list', () => {
      api.getJobs.mockReturnValue(of([job('1'), job('2')]));
      store.refreshJobs();
      expect(store.jobs().map(j => j.id)).toEqual(['1', '2']);
    });

    it('stopJob updates the matching job in place', () => {
      store.jobs.set([job('1', 'Running')]);
      api.stopJob.mockReturnValue(of(job('1', 'Stopped')));
      store.stopJob('1');
      expect(store.jobs()[0].state).toBe('Stopped');
    });

    it('removeJob drops the job locally on success', () => {
      store.jobs.set([job('1'), job('2')]);
      api.removeJob.mockReturnValue(of(undefined));
      store.removeJob('1');
      expect(store.jobs().map(j => j.id)).toEqual(['2']);
    });
  });

  describe('connection', () => {
    it('setConnection toasts success when connected', () => {
      api.setConnection.mockReturnValue(of<TransportStatus>({ isConnected: true, endpoint: 'localhost:6379', error: null }));
      store.setConnection('localhost:6379');
      expect(store.connection().isConnected).toBe(true);
      expect(store.toast()).toMatchObject({ kind: 'ok', text: 'Connected to Redis at localhost:6379.' });
    });

    it('setConnection toasts the failure reason when not connected', () => {
      api.setConnection.mockReturnValue(of<TransportStatus>({ isConnected: false, endpoint: null, error: 'refused' }));
      store.setConnection('localhost:6379');
      expect(store.toast()).toMatchObject({ kind: 'error', text: 'refused' });
    });
  });

  describe('live feed', () => {
    beforeEach(() => jest.useFakeTimers());
    afterEach(() => jest.useRealTimers());

    it('prepends published activity with a stable client id', () => {
      store.start();
      const a = { source: 'manual', messageKey: 'k', displayName: 'A', channel: 'c', byteCount: 1, success: true, error: null, timestamp: 't', jobId: null } as PublishActivity;
      const b = { ...a, displayName: 'B' };

      activity.published$.next(a);
      activity.published$.next(b);

      const log = store.activityLog();
      expect(log.map(x => x.displayName)).toEqual(['B', 'A']);
      expect(log[0]._id).toBe(2);
      expect(log[1]._id).toBe(1);
    });

    it('caps the activity log at 200 entries', () => {
      store.start();
      const base = { source: 'manual', messageKey: 'k', displayName: 'x', channel: 'c', byteCount: 1, success: true, error: null, timestamp: 't', jobId: null } as PublishActivity;
      for (let i = 0; i < 250; i++) activity.published$.next({ ...base });
      expect(store.activityLog().length).toBe(200);
    });

    it('upserts jobs arriving on the jobChanged stream', () => {
      store.start();
      activity.jobChanged$.next(job('j1', 'Running'));
      activity.jobChanged$.next(job('j1', 'Stopped'));
      expect(store.jobs().length).toBe(1);
      expect(store.jobs()[0].state).toBe('Stopped');
    });

    it('start is idempotent', () => {
      store.start();
      store.start();
      expect(activity.start).toHaveBeenCalledTimes(1);
    });
  });

  describe('toasts', () => {
    beforeEach(() => jest.useFakeTimers());
    afterEach(() => jest.useRealTimers());

    it('auto-dismisses a toast after 4.5s', () => {
      api.publish.mockReturnValue(of<PublishResult>({ channel: 'c', byteCount: 1, timestamp: 't' }));
      store.publishOnce('k', 'c', '{}');
      expect(store.toast()).not.toBeNull();
      jest.advanceTimersByTime(4500);
      expect(store.toast()).toBeNull();
    });
  });

  it('clearActivity empties the log', () => {
    store.activityLog.set([{ source: 'manual', messageKey: 'k', displayName: 'A', channel: 'c', byteCount: 1, success: true, error: null, timestamp: 't', jobId: null, _id: 1 }]);
    store.clearActivity();
    expect(store.activityLog()).toEqual([]);
  });
});
