import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FakeStore, createFakeStore, provideFakeStore } from '../../../testing/fake-store';
import { HeaderBarComponent } from './header-bar.component';

describe('HeaderBarComponent', () => {
  let fixture: ComponentFixture<HeaderBarComponent>;
  let component: HeaderBarComponent;
  let store: FakeStore;
  const el = (sel: string) => fixture.nativeElement.querySelector(sel) as HTMLElement;

  beforeEach(async () => {
    store = createFakeStore();
    await TestBed.configureTestingModule({
      imports: [HeaderBarComponent],
      providers: [provideFakeStore(store)],
    }).compileComponents();
    fixture = TestBed.createComponent(HeaderBarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('shows "No plugin" until one is loaded', () => {
    expect(el('[data-testid="plugin-status"]').textContent).toContain('No plugin');
  });

  it('shows the plugin name and message count when loaded', () => {
    store.plugin.set({ isLoaded: true, plugin: { name: 'Sample', sourcePath: 'pulsar.plugin.json', loadedAt: 't', messageCount: 9 } });
    fixture.detectChanges();
    const text = el('[data-testid="plugin-status"]').textContent ?? '';
    expect(text).toContain('Sample');
    expect(text).toContain('9 msgs');
  });

  it('reflects the Redis offline/online state', () => {
    expect(el('[data-testid="redis-status"]').textContent).toContain('offline');
    store.connection.set({ isConnected: true, endpoint: 'localhost:6379', error: null });
    fixture.detectChanges();
    expect(el('[data-testid="redis-status"]').textContent).toContain('localhost:6379');
  });

  it('marks the live dot connected from the store signal', () => {
    expect(el('[data-testid="live-dot"]').classList.contains('dot--off')).toBe(true);
    store.liveConnected.set(true);
    fixture.detectChanges();
    expect(el('[data-testid="live-dot"]').classList.contains('dot--ok')).toBe(true);
  });

  it('emits open when the Settings button is clicked', () => {
    const spy = jest.fn();
    component.open.subscribe(spy);
    const btn = [...fixture.nativeElement.querySelectorAll('button')].find(b => b.textContent?.includes('Settings'));
    (btn as HTMLButtonElement).click();
    expect(spy).toHaveBeenCalled();
  });
});
