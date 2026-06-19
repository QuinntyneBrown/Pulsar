import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiService } from './api.service';
import { CyclicJob, MessageDetail, PluginInfo, PluginState, PublishResult, TransportStatus } from './models';

describe('ApiService', () => {
  let service: ApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ApiService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('is created', () => {
    expect(service).toBeTruthy();
  });

  it('getPlugin GETs /api/plugin', () => {
    const expected: PluginState = { isLoaded: false, plugin: null };
    let actual: PluginState | undefined;
    service.getPlugin().subscribe(v => (actual = v));

    const req = http.expectOne('/api/plugin');
    expect(req.request.method).toBe('GET');
    req.flush(expected);

    expect(actual).toEqual(expected);
  });

  it('loadPlugin POSTs the path to /api/plugin/load', () => {
    const info: PluginInfo = { name: 'Sample', sourcePath: 'x.dll', loadedAt: 't', messageCount: 3 };
    service.loadPlugin('x.dll').subscribe();

    const req = http.expectOne('/api/plugin/load');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ path: 'x.dll' });
    req.flush(info);
  });

  it('unloadPlugin POSTs an empty body to /api/plugin/unload', () => {
    service.unloadPlugin().subscribe();
    const req = http.expectOne('/api/plugin/unload');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(null);
  });

  it('getMessage URL-encodes the key', () => {
    const detail = { key: 'a/b', displayName: 'A', category: 'Event', defaultChannel: 'c', messageType: 't', templateJson: '{}' } as MessageDetail;
    service.getMessage('a/b').subscribe();
    const req = http.expectOne('/api/messages/a%2Fb');
    expect(req.request.method).toBe('GET');
    req.flush(detail);
  });

  it('publish POSTs the request body to /api/publish', () => {
    const result: PublishResult = { channel: 'c', byteCount: 12, timestamp: 't' };
    const request = { key: 'k', channel: 'c', payloadJson: '{}' };
    service.publish(request).subscribe();
    const req = http.expectOne('/api/publish');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(result);
  });

  it('stopJob POSTs to /api/cyclic/:id/stop', () => {
    service.stopJob('job-1').subscribe();
    const req = http.expectOne('/api/cyclic/job-1/stop');
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'job-1' } as CyclicJob);
  });

  it('removeJob DELETEs /api/cyclic/:id', () => {
    service.removeJob('job-1').subscribe();
    const req = http.expectOne('/api/cyclic/job-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('setConnection POSTs the connection string', () => {
    const status: TransportStatus = { isConnected: true, endpoint: 'localhost:6379', error: null };
    service.setConnection('localhost:6379').subscribe();
    const req = http.expectOne('/api/connection');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ connectionString: 'localhost:6379' });
    req.flush(status);
  });
});
