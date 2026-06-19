import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FakeStore, createFakeStore, provideFakeStore } from '../../../testing/fake-store';
import { SettingsDialogComponent } from './settings-dialog.component';

describe('SettingsDialogComponent', () => {
  let fixture: ComponentFixture<SettingsDialogComponent>;
  let component: SettingsDialogComponent;
  let store: FakeStore;
  const el = (sel: string) => fixture.nativeElement.querySelector(sel) as HTMLElement;

  async function build() {
    await TestBed.configureTestingModule({
      imports: [SettingsDialogComponent],
      providers: [provideFakeStore(store)],
    }).compileComponents();
    fixture = TestBed.createComponent(SettingsDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  beforeEach(() => {
    store = createFakeStore();
  });

  it('defaults the inputs when the store has no values', async () => {
    await build();
    expect(component.connectionString()).toBe('localhost:6379');
    expect(component.pluginPath()).toBe('plugins/Pulsar.SampleMessages.dll');
  });

  it('seeds the inputs from existing store state', async () => {
    store.connection.set({ isConnected: true, endpoint: 'redis:7000', error: null });
    store.plugin.set({ isLoaded: true, plugin: { name: 'P', sourcePath: 'custom/My.dll', loadedAt: 't', messageCount: 1 } });
    await build();
    expect(component.connectionString()).toBe('redis:7000');
    expect(component.pluginPath()).toBe('custom/My.dll');
  });

  it('connects using the trimmed connection string', async () => {
    await build();
    component.connectionString.set('  redis:6380  ');
    component.connect();
    expect(store.setConnection).toHaveBeenCalledWith('redis:6380');
  });

  it('loads the plugin using the trimmed path', async () => {
    await build();
    component.pluginPath.set('  plugins/X.dll  ');
    component.load();
    expect(store.loadPlugin).toHaveBeenCalledWith('plugins/X.dll');
  });

  it('emits close on the backdrop and the close button', async () => {
    await build();
    const spy = jest.fn();
    component.close.subscribe(spy);
    (el('[data-testid="settings-close"]') as HTMLButtonElement).click();
    (el('[data-testid="settings-backdrop"]') as HTMLElement).click();
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it('disables Connect when the connection string is blank', async () => {
    await build();
    component.connectionString.set('   ');
    fixture.detectChanges();
    expect((el('[data-testid="connection-connect"]') as HTMLButtonElement).disabled).toBe(true);
  });
});
