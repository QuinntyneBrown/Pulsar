import { Component, OnInit, inject, signal } from '@angular/core';
import { PulsarStore } from '../core/pulsar.store';
import { ActivityFeedComponent } from './activity-feed/activity-feed.component';
import { CatalogComponent } from './catalog/catalog.component';
import { ComposerComponent } from './composer/composer.component';
import { HeaderBarComponent } from './header-bar/header-bar.component';
import { JobsComponent } from './jobs/jobs.component';
import { SettingsDialogComponent } from './settings-dialog/settings-dialog.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    HeaderBarComponent, CatalogComponent, ComposerComponent,
    JobsComponent, ActivityFeedComponent, SettingsDialogComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  readonly store = inject(PulsarStore);
  readonly settingsOpen = signal(false);

  ngOnInit(): void {
    this.store.start();
  }
}
