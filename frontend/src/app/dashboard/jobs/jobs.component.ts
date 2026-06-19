import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { PulsarStore } from '../../core/pulsar.store';

@Component({
  selector: 'app-jobs',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './jobs.component.html',
  styleUrl: './jobs.component.scss',
})
export class JobsComponent {
  readonly store = inject(PulsarStore);
}
