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
  name: 'Sample Messages',
  sourcePath: 'plugins/manifest/pulsar.plugin.json',
  loadedAt: '2026-06-19T12:00:00.000Z',
  messageCount: 6,
};

/**
 * Six messages spanning three categories. Deliberately ordered so the catalog's
 * category grouping (Telemetry -> Event -> Fault) is observable, and so
 * the first message (Heartbeat) is the one auto-selected on load.
 */
export const SAMPLE_MESSAGES: MessageSummary[] = [
  { key: 'HeartbeatTelemetry',  displayName: 'Heartbeat',           category: 'Telemetry', defaultChannel: 'telemetry.heartbeat' },
  { key: 'TemperatureReading',  displayName: 'Temperature Reading', category: 'Telemetry', defaultChannel: 'telemetry.temperature' },
  { key: 'BatteryTelemetry',    displayName: 'Battery Status',      category: 'Telemetry', defaultChannel: 'telemetry.battery' },
  { key: 'OperatorAlert',       displayName: 'Operator Alert',      category: 'Event',     defaultChannel: 'events.alert' },
  { key: 'ModeChangedEvent',    displayName: 'Mode Changed',        category: 'Event',     defaultChannel: 'events.mode-changed' },
  { key: 'SubsystemFault',      displayName: 'Subsystem Fault',     category: 'Fault',     defaultChannel: 'faults.subsystem' },
];

export const SAMPLE_DETAILS: Record<string, MessageDetail> = {
  HeartbeatTelemetry: {
    ...SAMPLE_MESSAGES[0],
    messageType: 'Heartbeat',
    templateJson: '{\n  "deviceId": "device-001",\n  "sequenceNumber": 0,\n  "status": "Nominal",\n  "uptimeSeconds": 3600\n}',
  },
  TemperatureReading: {
    ...SAMPLE_MESSAGES[1],
    messageType: 'Temperature Reading',
    templateJson: '{\n  "sensorId": "sensor-temp-1",\n  "celsius": 21.5,\n  "recentSamples": [21.3, 21.4, 21.5]\n}',
  },
  BatteryTelemetry: {
    ...SAMPLE_MESSAGES[2],
    messageType: 'Battery Status',
    templateJson: '{\n  "packId": "pack-a",\n  "percentage": 87.5,\n  "voltage": 28.4,\n  "charging": false\n}',
  },
  OperatorAlert: {
    ...SAMPLE_MESSAGES[3],
    messageType: 'Operator Alert',
    templateJson: '{\n  "code": "INFO-100",\n  "message": "Routine status update.",\n  "severity": "Info"\n}',
  },
  ModeChangedEvent: {
    ...SAMPLE_MESSAGES[4],
    messageType: 'Mode Changed',
    templateJson: '{\n  "from": "Idle",\n  "to": "Active",\n  "reason": "Operator command"\n}',
  },
  SubsystemFault: {
    ...SAMPLE_MESSAGES[5],
    messageType: 'Subsystem Fault',
    templateJson: '{\n  "subsystem": "power",\n  "faultCode": "F-205",\n  "description": "Voltage out of expected range.",\n  "severity": "Critical"\n}',
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
    messageKey: 'HeartbeatTelemetry',
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
    messageKey: 'HeartbeatTelemetry',
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
