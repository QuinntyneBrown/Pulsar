import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CyclicJob, MessageDetail, MessageSummary, PluginInfo, PluginState,
  PublishRequest, PublishResult, StartCyclicRequest, TransportStatus, ValidationResult,
} from './models';

/** Thin, typed wrapper over the Pulsar HTTP API. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  getPlugin(): Observable<PluginState> {
    return this.http.get<PluginState>(`${this.base}/plugin`);
  }

  loadPlugin(path: string): Observable<PluginInfo> {
    return this.http.post<PluginInfo>(`${this.base}/plugin/load`, { path });
  }

  unloadPlugin(): Observable<void> {
    return this.http.post<void>(`${this.base}/plugin/unload`, {});
  }

  getMessages(): Observable<MessageSummary[]> {
    return this.http.get<MessageSummary[]>(`${this.base}/messages`);
  }

  getMessage(key: string): Observable<MessageDetail> {
    return this.http.get<MessageDetail>(`${this.base}/messages/${encodeURIComponent(key)}`);
  }

  /** Advisory schema check; returns matches=false with messages rather than erroring. */
  validateMessage(key: string, payloadJson: string): Observable<ValidationResult> {
    return this.http.post<ValidationResult>(
      `${this.base}/messages/${encodeURIComponent(key)}/validate`, { payloadJson });
  }

  publish(request: PublishRequest): Observable<PublishResult> {
    return this.http.post<PublishResult>(`${this.base}/publish`, request);
  }

  getJobs(): Observable<CyclicJob[]> {
    return this.http.get<CyclicJob[]>(`${this.base}/cyclic`);
  }

  startCyclic(request: StartCyclicRequest): Observable<CyclicJob> {
    return this.http.post<CyclicJob>(`${this.base}/cyclic`, request);
  }

  stopJob(id: string): Observable<CyclicJob> {
    return this.http.post<CyclicJob>(`${this.base}/cyclic/${id}/stop`, {});
  }

  removeJob(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/cyclic/${id}`);
  }

  getConnection(): Observable<TransportStatus> {
    return this.http.get<TransportStatus>(`${this.base}/connection`);
  }

  setConnection(connectionString: string): Observable<TransportStatus> {
    return this.http.post<TransportStatus>(`${this.base}/connection`, { connectionString });
  }
}
