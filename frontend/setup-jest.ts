import { setupZoneTestEnv } from 'jest-preset-angular/setup-env/zone';

// Initialises the Angular TestBed with the zone.js test environment for every
// spec file. Replaces the Karma/Jasmine `test.ts` entry point.
setupZoneTestEnv();
