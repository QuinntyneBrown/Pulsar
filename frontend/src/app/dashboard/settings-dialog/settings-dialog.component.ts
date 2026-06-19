import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { PulsarStore } from '../../core/pulsar.store';

@Component({
  selector: 'app-settings-dialog',
  standalone: true,
  templateUrl: './settings-dialog.component.html',
  styleUrl: './settings-dialog.component.scss',
})
export class SettingsDialogComponent {
  readonly store = inject(PulsarStore);
  @Output() close = new EventEmitter<void>();

  readonly connectionString = signal(this.store.connection().endpoint ?? 'localhost:6379');
  readonly pluginPath = signal(this.store.plugin().plugin?.sourcePath ?? 'plugins/Pulsar.SampleMessages.dll');

  connect(): void { this.store.setConnection(this.connectionString().trim()); }
  load(): void { this.store.loadPlugin(this.pluginPath().trim()); }
}
