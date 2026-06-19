import { Component, effect, inject, signal } from '@angular/core';
import { PulsarStore } from '../../core/pulsar.store';

const PRESETS = [
  { label: '250 ms', ms: 250 },
  { label: '500 ms', ms: 500 },
  { label: '1 s', ms: 1000 },
  { label: '5 s', ms: 5000 },
  { label: '30 s', ms: 30000 },
];

@Component({
  selector: 'app-composer',
  standalone: true,
  templateUrl: './composer.component.html',
  styleUrl: './composer.component.scss',
})
export class ComposerComponent {
  readonly store = inject(PulsarStore);
  readonly presets = PRESETS;

  readonly channel = signal('');
  readonly json = signal('');
  readonly intervalMs = signal(1000);
  readonly jsonError = signal<string | null>(null);

  constructor() {
    // Re-seed the editor whenever a different message is selected.
    effect(() => {
      const detail = this.store.selectedDetail();
      if (!detail) return;
      this.channel.set(detail.defaultChannel);
      this.json.set(detail.templateJson);
      this.intervalMs.set(1000);
      this.jsonError.set(null);
    }, { allowSignalWrites: true });
  }

  reset(): void {
    const d = this.store.selectedDetail();
    if (!d) return;
    this.channel.set(d.defaultChannel);
    this.json.set(d.templateJson);
    this.jsonError.set(null);
  }

  format(): void {
    if (!this.validate()) return;
    this.json.set(JSON.stringify(JSON.parse(this.json()), null, 2));
  }

  send(): void {
    const d = this.store.selectedDetail();
    if (!d || !this.validate()) return;
    this.store.publishOnce(d.key, this.channelOrNull(), this.json());
  }

  startCyclic(): void {
    const d = this.store.selectedDetail();
    if (!d || !this.validate()) return;
    if (this.intervalMs() < 10) { this.jsonError.set('Interval must be at least 10 ms.'); return; }
    this.store.startCyclic(d.key, this.channelOrNull(), this.intervalMs(), this.json());
  }

  private channelOrNull(): string | null {
    const c = this.channel().trim();
    return c.length > 0 ? c : null;
  }

  private validate(): boolean {
    try {
      JSON.parse(this.json());
      this.jsonError.set(null);
      return true;
    } catch (e) {
      this.jsonError.set('Invalid JSON: ' + (e as Error).message);
      return false;
    }
  }
}
