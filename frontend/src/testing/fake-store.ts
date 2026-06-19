import { WritableSignal, signal } from '@angular/core';
import {
  CyclicJob, MessageDetail, MessageSummary, PluginState, PublishActivity, TransportStatus,
} from '../app/core/models';
import { MessageGroup, PulsarStore, Toast } from '../app/core/pulsar.store';

/**
 * A fully-stubbed {@link PulsarStore} for component tests. Every piece of view
 * state is an independently writable signal so a test can drive the component
 * into any state, and every action is a jest mock so a test can assert it was
 * called. Real `computed` members (`groups`, `runningCount`) and the
 * `activity`-backed `liveConnected` are flattened to plain writable signals,
 * since components only ever read them.
 */
export interface FakeStore {
  plugin: WritableSignal<PluginState>;
  messages: WritableSignal<MessageSummary[]>;
  selectedKey: WritableSignal<string | null>;
  selectedDetail: WritableSignal<MessageDetail | null>;
  loadingDetail: WritableSignal<boolean>;
  connection: WritableSignal<TransportStatus>;
  jobs: WritableSignal<CyclicJob[]>;
  activityLog: WritableSignal<PublishActivity[]>;
  toast: WritableSignal<Toast | null>;
  liveConnected: WritableSignal<boolean>;
  groups: WritableSignal<MessageGroup[]>;
  runningCount: WritableSignal<number>;

  start: jest.Mock;
  refreshPlugin: jest.Mock;
  refreshConnection: jest.Mock;
  refreshJobs: jest.Mock;
  select: jest.Mock<void, [string]>;
  loadPlugin: jest.Mock<void, [string]>;
  unloadPlugin: jest.Mock;
  publishOnce: jest.Mock;
  startCyclic: jest.Mock;
  stopJob: jest.Mock<void, [string]>;
  removeJob: jest.Mock<void, [string]>;
  setConnection: jest.Mock<void, [string]>;
  clearActivity: jest.Mock;
}

export function createFakeStore(): FakeStore {
  return {
    plugin: signal<PluginState>({ isLoaded: false, plugin: null }),
    messages: signal<MessageSummary[]>([]),
    selectedKey: signal<string | null>(null),
    selectedDetail: signal<MessageDetail | null>(null),
    loadingDetail: signal(false),
    connection: signal<TransportStatus>({ isConnected: false, endpoint: null, error: null }),
    jobs: signal<CyclicJob[]>([]),
    activityLog: signal<PublishActivity[]>([]),
    toast: signal<Toast | null>(null),
    liveConnected: signal(false),
    groups: signal<MessageGroup[]>([]),
    runningCount: signal(0),

    start: jest.fn(),
    refreshPlugin: jest.fn(),
    refreshConnection: jest.fn(),
    refreshJobs: jest.fn(),
    select: jest.fn(),
    loadPlugin: jest.fn(),
    unloadPlugin: jest.fn(),
    publishOnce: jest.fn(),
    startCyclic: jest.fn(),
    stopJob: jest.fn(),
    removeJob: jest.fn(),
    setConnection: jest.fn(),
    clearActivity: jest.fn(),
  };
}

/** Provider that swaps the real store for a fake one in a component test. */
export function provideFakeStore(fake: FakeStore) {
  return { provide: PulsarStore, useValue: fake as unknown as PulsarStore };
}
