import { Component, inject } from '@angular/core';
import { PulsarStore } from '../../core/pulsar.store';

@Component({
  selector: 'app-catalog',
  standalone: true,
  templateUrl: './catalog.component.html',
  styleUrl: './catalog.component.scss',
})
export class CatalogComponent {
  readonly store = inject(PulsarStore);
}
