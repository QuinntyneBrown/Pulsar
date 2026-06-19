import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PublishActivity } from '../../core/models';
import { FakeStore, createFakeStore, provideFakeStore } from '../../../testing/fake-store';
import { ActivityFeedComponent } from './activity-feed.component';

function activity(over: Partial<PublishActivity> = {}): PublishActivity {
  return {
    source: 'manual', messageKey: 'k', displayName: 'Ping', channel: 'c', byteCount: 12,
    success: true, error: null, timestamp: '2026-01-01T00:00:00.000Z', jobId: null, _id: 1, ...over,
  };
}

describe('ActivityFeedComponent', () => {
  let fixture: ComponentFixture<ActivityFeedComponent>;
  let store: FakeStore;
  const all = (sel: string) => fixture.nativeElement.querySelectorAll(sel);

  beforeEach(async () => {
    store = createFakeStore();
    await TestBed.configureTestingModule({
      imports: [ActivityFeedComponent],
      providers: [provideFakeStore(store)],
    }).compileComponents();
    fixture = TestBed.createComponent(ActivityFeedComponent);
    fixture.detectChanges();
  });

  it('shows the empty state with no activity', () => {
    expect(fixture.nativeElement.querySelector('[data-testid="activity-empty"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="activity-clear"]')).toBeNull();
  });

  it('renders a row per activity entry', () => {
    store.activityLog.set([activity({ _id: 1 }), activity({ _id: 2, success: false, error: 'boom' })]);
    fixture.detectChanges();
    expect(all('[data-testid="activity-row"]').length).toBe(2);
    expect(fixture.nativeElement.querySelector('[data-testid="activity-error"]').textContent).toContain('boom');
  });

  it('shows byte count for successful entries', () => {
    store.activityLog.set([activity({ byteCount: 99 })]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="activity-bytes"]').textContent).toContain('99B');
  });

  it('clears the feed when Clear is clicked', () => {
    store.activityLog.set([activity()]);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="activity-clear"]') as HTMLButtonElement).click();
    expect(store.clearActivity).toHaveBeenCalled();
  });
});
