import { Component, EventEmitter, Output, inject } from '@angular/core';
import { PulsarStore } from '../../core/pulsar.store';

@Component({
  selector: 'app-header-bar',
  standalone: true,
  templateUrl: './header-bar.component.html',
  styleUrl: './header-bar.component.scss',
})
export class HeaderBarComponent {
  readonly store = inject(PulsarStore);
  @Output() open = new EventEmitter<void>();
}
