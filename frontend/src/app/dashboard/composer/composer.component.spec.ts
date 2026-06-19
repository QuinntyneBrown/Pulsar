import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MessageDetail } from '../../core/models';
import { FakeStore, createFakeStore, provideFakeStore } from '../../../testing/fake-store';
import { ComposerComponent } from './composer.component';

const DETAIL: MessageDetail = {
  key: 'k', displayName: 'My Message', category: 'Event', defaultChannel: 'events.k',
  messageType: 'My.Type', templateJson: '{"a":1}', hasSchema: false,
};

describe('ComposerComponent', () => {
  let fixture: ComponentFixture<ComposerComponent>;
  let component: ComposerComponent;
  let store: FakeStore;

  beforeEach(async () => {
    store = createFakeStore();
    await TestBed.configureTestingModule({
      imports: [ComposerComponent],
      providers: [provideFakeStore(store)],
    }).compileComponents();
    fixture = TestBed.createComponent(ComposerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  function selectDetail(detail: MessageDetail = DETAIL) {
    store.selectedDetail.set(detail);
    fixture.detectChanges(); // flush the seeding effect
  }

  it('seeds the editor from the selected message', () => {
    selectDetail();
    expect(component.channel()).toBe('events.k');
    expect(component.json()).toBe('{"a":1}');
    expect(component.intervalMs()).toBe(1000);
  });

  it('shows the "no plugin" empty state when nothing is selected', () => {
    expect(fixture.nativeElement.querySelector('[data-testid="composer-empty-noplugin"]')).toBeTruthy();
  });

  it('publishes once with the trimmed channel and current payload', () => {
    selectDetail();
    component.channel.set('  custom  ');
    component.json.set('{"x":true}');
    component.send();
    expect(store.publishOnce).toHaveBeenCalledWith('k', 'custom', '{"x":true}');
  });

  it('treats a blank channel as null (use the default)', () => {
    selectDetail();
    component.channel.set('   ');
    component.send();
    expect(store.publishOnce).toHaveBeenCalledWith('k', null, expect.any(String));
  });

  it('refuses to send invalid JSON and reports the error', () => {
    selectDetail();
    component.json.set('{ not json');
    component.send();
    expect(store.publishOnce).not.toHaveBeenCalled();
    expect(component.jsonError()).toMatch(/Invalid JSON/);
  });

  it('formats (pretty-prints) valid JSON', () => {
    selectDetail();
    component.json.set('{"a":1,"b":2}');
    component.format();
    expect(component.json()).toBe('{\n  "a": 1,\n  "b": 2\n}');
  });

  it('rejects a cyclic interval below 10ms', () => {
    selectDetail();
    component.intervalMs.set(5);
    component.startCyclic();
    expect(store.startCyclic).not.toHaveBeenCalled();
    expect(component.jsonError()).toMatch(/at least 10 ms/);
  });

  it('starts a cyclic job with valid input', () => {
    selectDetail();
    component.intervalMs.set(500);
    component.startCyclic();
    expect(store.startCyclic).toHaveBeenCalledWith('k', 'events.k', 500, '{"a":1}');
  });

  it('reset restores the channel and template from the selection', () => {
    selectDetail();
    component.channel.set('changed');
    component.json.set('garbage');
    component.reset();
    expect(component.channel()).toBe('events.k');
    expect(component.json()).toBe('{"a":1}');
  });

  it('shows an advisory schema warning when the store reports issues', () => {
    selectDetail();
    store.schemaIssues.set(['$.status: bad value']);
    fixture.detectChanges();
    const warn = fixture.nativeElement.querySelector('[data-testid="composer-schema-warning"]');
    expect(warn).toBeTruthy();
    expect(warn.textContent).toContain('$.status: bad value');
  });

  it('shows no schema warning when there are no issues', () => {
    selectDetail();
    store.schemaIssues.set([]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="composer-schema-warning"]')).toBeFalsy();
  });

  it('still sends when schema issues are present (advisory only)', () => {
    selectDetail();
    store.schemaIssues.set(['something is off']);
    component.json.set('{"x":true}');
    component.send();
    expect(store.publishOnce).toHaveBeenCalledWith('k', 'events.k', '{"x":true}');
  });

  it('debounces an advisory re-validation after editing a schema-backed message', () => {
    jest.useFakeTimers();
    try {
      selectDetail({ ...DETAIL, hasSchema: true });
      component.onJson('{"a":2}');
      expect(store.validatePayload).not.toHaveBeenCalled(); // debounced
      jest.advanceTimersByTime(250);
      expect(store.validatePayload).toHaveBeenCalledWith('k', '{"a":2}');
    } finally {
      jest.useRealTimers();
    }
  });

  it('does not validate a message that has no schema', () => {
    jest.useFakeTimers();
    try {
      selectDetail(); // DETAIL.hasSchema === false
      component.onJson('{"a":2}');
      jest.advanceTimersByTime(250);
      expect(store.validatePayload).not.toHaveBeenCalled();
    } finally {
      jest.useRealTimers();
    }
  });
});
