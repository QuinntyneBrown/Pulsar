/**
 * Canonical fixture data for the mocked Pulsar backend.
 *
 * Types are imported from the real app models so the mock can never drift from
 * the contract the UI actually consumes — if a model field changes, these
 * fixtures stop type-checking.
 */
import type {
  CyclicJob, MessageDetail, MessageSummary, PluginInfo, PluginState,
  PublishActivity, TransportStatus,
} from '../../src/app/core/models';

export type {
  CyclicJob, MessageDetail, MessageSummary, PluginInfo, PluginState,
  PublishActivity, TransportStatus,
};

/** The plugin descriptor returned once a plugin is "loaded". */
export const SAMPLE_PLUGIN: PluginInfo = {
  name: 'Pulsar.SampleMessages',
  sourcePath: 'plugins/Pulsar.SampleMessages.dll',
  loadedAt: '2026-06-19T12:00:00.000Z',
  messageCount: 5,
};

/**
 * Five messages spanning four categories. Deliberately ordered so the catalog's
 * category grouping (Telemetry → Event → Fault → Command) is observable, and so
 * the first message (Heartbeat) is the one auto-selected on load.
 */
export const SAMPLE_MESSAGES: MessageSummary[] = [
  { key: 'heartbeat',    displayName: 'Heartbeat',       category: 'Telemetry', defaultChannel: 'telemetry.heartbeat' },
  { key: 'position',     displayName: 'Position Report', category: 'Telemetry', defaultChannel: 'telemetry.position' },
  { key: 'system-start', displayName: 'System Start',    category: 'Event',     defaultChannel: 'events.system' },
  { key: 'overheat',     displayName: 'Overheat Alarm',  category: 'Fault',     defaultChannel: 'faults.overheat' },
  { key: 'reboot',       displayName: 'Reboot Command',  category: 'Command',   defaultChannel: 'commands.reboot' },
];

export const SAMPLE_DETAILS: Record<string, MessageDetail> = {
  heartbeat: {
    ...SAMPLE_MESSAGES[0],
    messageType: 'Pulsar.SampleMessages.Heartbeat',
    templateJson: '{\n  "deviceId": "device-001",\n  "uptimeSeconds": 0\n}',
  },
  position: {
    ...SAMPLE_MESSAGES[1],
    messageType: 'Pulsar.SampleMessages.PositionReport',
    templateJson: '{\n  "lat": 0,\n  "lon": 0\n}',
  },
  'system-start': {
    ...SAMPLE_MESSAGES[2],
    messageType: 'Pulsar.SampleMessages.SystemStart',
    templateJson: '{\n  "node": "alpha"\n}',
  },
  overheat: {
    ...SAMPLE_MESSAGES[3],
    messageType: 'Pulsar.SampleMessages.OverheatAlarm',
    templateJson: '{\n  "celsius": 95\n}',
  },
  reboot: {
    ...SAMPLE_MESSAGES[4],
    messageType: 'Pulsar.SampleMessages.RebootCommand',
    templateJson: '{\n  "delaySeconds": 5\n}',
  },
};

export const DISCONNECTED: TransportStatus = { isConnected: false, endpoint: null, error: null };

export function connected(endpoint: string): TransportStatus {
  return { isConnected: true, endpoint, error: null };
}

/** Build a CyclicJob with sensible Running defaults; override any field. */
export function makeJob(overrides: Partial<CyclicJob> = {}): CyclicJob {
  return {
    id: 'job-1',
    messageKey: 'heartbeat',
    displayName: 'Heartbeat',
    channel: 'telemetry.heartbeat',
    intervalMs: 1000,
    state: 'Running',
    publishedCount: 0,
    failureCount: 0,
    lastError: null,
    startedAt: '2026-06-19T12:00:00.000Z',
    stoppedAt: null,
    lastPublishedAt: null,
    ...overrides,
  };
}

/** Build a PublishActivity (server-push shape) with successful manual defaults. */
export function makeActivity(overrides: Partial<PublishActivity> = {}): PublishActivity {
  return {
    source: 'manual',
    messageKey: 'heartbeat',
    displayName: 'Heartbeat',
    channel: 'telemetry.heartbeat',
    byteCount: 42,
    success: true,
    error: null,
    timestamp: '2026-06-19T12:00:00.000Z',
    jobId: null,
    ...overrides,
  };
}
