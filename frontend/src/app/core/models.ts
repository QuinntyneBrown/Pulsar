export type MessageCategory = 'Telemetry' | 'Event' | 'Fault' | 'Command' | 'Other';

export interface MessageSummary {
  key: string;
  displayName: string;
  category: MessageCategory;
  defaultChannel: string;
}

export interface MessageDetail extends MessageSummary {
  messageType: string;
  templateJson: string;
}

export interface PluginInfo {
  name: string;
  sourcePath: string;
  loadedAt: string;
  messageCount: number;
}

export interface PluginState {
  isLoaded: boolean;
  plugin: PluginInfo | null;
}

export type CyclicJobState = 'Running' | 'Stopped';

export interface CyclicJob {
  id: string;
  messageKey: string;
  displayName: string;
  channel: string;
  intervalMs: number;
  state: CyclicJobState;
  publishedCount: number;
  failureCount: number;
  lastError: string | null;
  startedAt: string;
  stoppedAt: string | null;
  lastPublishedAt: string | null;
}

export interface TransportStatus {
  isConnected: boolean;
  endpoint: string | null;
  error: string | null;
}

export interface PublishActivity {
  source: 'manual' | 'cyclic' | string;
  messageKey: string;
  displayName: string;
  channel: string;
  byteCount: number;
  success: boolean;
  error: string | null;
  timestamp: string;
  jobId: string | null;
  /** Client-assigned stable id for list tracking (not from the server). */
  _id?: number;
}

export interface PublishResult {
  channel: string;
  byteCount: number;
  timestamp: string;
}

export interface PublishRequest {
  key: string;
  channel: string | null;
  payloadJson: string;
}

export interface StartCyclicRequest {
  key: string;
  channel: string | null;
  intervalMs: number;
  payloadJson: string;
}
