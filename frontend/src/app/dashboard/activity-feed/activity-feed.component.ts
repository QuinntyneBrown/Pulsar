import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { PulsarStore } from '../../core/pulsar.store';

@Component({
  selector: 'app-activity-feed',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './activity-feed.component.html',
  styleUrl: './activity-feed.component.scss',
})
export class ActivityFeedComponent {
  readonly store = inject(PulsarStore);
}
