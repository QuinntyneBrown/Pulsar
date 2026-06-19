import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FakeStore, createFakeStore, provideFakeStore } from '../../testing/fake-store';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let component: DashboardComponent;
  let store: FakeStore;

  beforeEach(async () => {
    store = createFakeStore();
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideFakeStore(store)],
    }).compileComponents();
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('boots the store on init', () => {
    expect(store.start).toHaveBeenCalledTimes(1);
  });

  it('renders the core panels', () => {
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('[data-testid="header-bar"]')).toBeTruthy();
    expect(root.querySelector('[data-testid="catalog"]')).toBeTruthy();
    expect(root.querySelector('[data-testid="composer"]')).toBeTruthy();
    expect(root.querySelector('[data-testid="jobs"]')).toBeTruthy();
    expect(root.querySelector('[data-testid="activity-feed"]')).toBeTruthy();
  });

  it('keeps the settings dialog closed until opened', () => {
    expect(fixture.nativeElement.querySelector('[data-testid="settings-dialog"]')).toBeNull();
    component.settingsOpen.set(true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="settings-dialog"]')).toBeTruthy();
  });

  it('opens settings when the header emits open', () => {
    const settingsButton = [...fixture.nativeElement.querySelectorAll('button')]
      .find(b => b.textContent?.includes('Settings')) as HTMLButtonElement;
    settingsButton.click();
    fixture.detectChanges();
    expect(component.settingsOpen()).toBe(true);
  });

  it('renders a toast when the store has one', () => {
    store.toast.set({ id: 1, kind: 'ok', text: 'Saved' });
    fixture.detectChanges();
    const toast = fixture.nativeElement.querySelector('[data-testid="toast"]');
    expect(toast.textContent).toContain('Saved');
    expect(toast.getAttribute('data-kind')).toBe('ok');
  });
});
