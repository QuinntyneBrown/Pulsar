import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CyclicJob } from '../../core/models';
import { FakeStore, createFakeStore, provideFakeStore } from '../../../testing/fake-store';
import { JobsComponent } from './jobs.component';

function job(over: Partial<CyclicJob> = {}): CyclicJob {
  return {
    id: 'j1', messageKey: 'k', displayName: 'Heartbeat', channel: 'c', intervalMs: 1000,
    state: 'Running', publishedCount: 5, failureCount: 0, lastError: null,
    startedAt: 't', stoppedAt: null, lastPublishedAt: null, ...over,
  };
}

describe('JobsComponent', () => {
  let fixture: ComponentFixture<JobsComponent>;
  let store: FakeStore;
  const all = (sel: string) => fixture.nativeElement.querySelectorAll(sel);

  beforeEach(async () => {
    store = createFakeStore();
    await TestBed.configureTestingModule({
      imports: [JobsComponent],
      providers: [provideFakeStore(store)],
    }).compileComponents();
    fixture = TestBed.createComponent(JobsComponent);
    fixture.detectChanges();
  });

  it('shows the empty state with no jobs', () => {
    expect(fixture.nativeElement.querySelector('[data-testid="jobs-empty"]')).toBeTruthy();
  });

  it('renders a row per job and a running chip', () => {
    store.jobs.set([job({ id: 'a' }), job({ id: 'b', state: 'Stopped' })]);
    store.runningCount.set(1);
    fixture.detectChanges();

    expect(all('[data-testid="job"]').length).toBe(2);
    expect(fixture.nativeElement.querySelector('[data-testid="jobs-running"]').textContent).toContain('1 running');
  });

  it('shows the failure count only when a job has failures', () => {
    store.jobs.set([job({ failureCount: 3 })]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="job-failed"]').textContent).toContain('3');
  });

  it('stops a running job', () => {
    store.jobs.set([job({ id: 'a', state: 'Running' })]);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="job-stop"]') as HTMLButtonElement).click();
    expect(store.stopJob).toHaveBeenCalledWith('a');
  });

  it('removes a job', () => {
    store.jobs.set([job({ id: 'a' })]);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="job-remove"]') as HTMLButtonElement).click();
    expect(store.removeJob).toHaveBeenCalledWith('a');
  });

  it('does not offer Stop for an already-stopped job', () => {
    store.jobs.set([job({ state: 'Stopped' })]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="job-stop"]')).toBeNull();
  });
});
