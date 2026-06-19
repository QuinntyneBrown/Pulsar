import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FakeStore, createFakeStore, provideFakeStore } from '../../../testing/fake-store';
import { CatalogComponent } from './catalog.component';

describe('CatalogComponent', () => {
  let fixture: ComponentFixture<CatalogComponent>;
  let store: FakeStore;
  const el = (sel: string) => fixture.nativeElement.querySelector(sel) as HTMLElement | null;

  beforeEach(async () => {
    store = createFakeStore();
    await TestBed.configureTestingModule({
      imports: [CatalogComponent],
      providers: [provideFakeStore(store)],
    }).compileComponents();
    fixture = TestBed.createComponent(CatalogComponent);
    fixture.detectChanges();
  });

  it('shows the "no plugin" empty state by default', () => {
    expect(el('[data-testid="catalog-empty-noplugin"]')).toBeTruthy();
  });

  it('shows the "no messages" empty state when a plugin exposes none', () => {
    store.plugin.set({ isLoaded: true, plugin: null });
    store.messages.set([]);
    fixture.detectChanges();
    expect(el('[data-testid="catalog-empty-nomessages"]')).toBeTruthy();
  });

  it('renders one item per message grouped by category', () => {
    store.plugin.set({ isLoaded: true, plugin: null });
    store.messages.set([{ key: 'a', displayName: 'A', category: 'Event', defaultChannel: 'c' }]);
    store.groups.set([{ category: 'Event', messages: [{ key: 'a', displayName: 'A', category: 'Event', defaultChannel: 'c' }] }]);
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('[data-testid="catalog-item"]');
    expect(items.length).toBe(1);
    expect(items[0].getAttribute('data-key')).toBe('a');
  });

  it('selects a message when its item is clicked', () => {
    store.plugin.set({ isLoaded: true, plugin: null });
    store.messages.set([{ key: 'a', displayName: 'A', category: 'Event', defaultChannel: 'c' }]);
    store.groups.set([{ category: 'Event', messages: [{ key: 'a', displayName: 'A', category: 'Event', defaultChannel: 'c' }] }]);
    fixture.detectChanges();

    (el('[data-testid="catalog-item"]') as HTMLButtonElement).click();
    expect(store.select).toHaveBeenCalledWith('a');
  });
});
