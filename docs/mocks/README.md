# Pulsar UI — design mocks

Static, presentation-only HTML mocks of the Pulsar dashboard, **reverse-engineered
from the implemented Angular app** (`frontend/src/app`). They reproduce the intended
design — layout, component states, and styling — without any JavaScript or backend,
so they open in a browser as a self-contained design reference.

## View them

Open **[`index.html`](index.html)** in a browser for the gallery (scaled live previews
of each screen + a component map and the design-token legend), or open any screen
directly.

## Files

| File | Screen | Reproduces |
| --- | --- | --- |
| `index.html` | Gallery | Overview, previews, mock→component map, design tokens |
| `dashboard.html` | Populated dashboard | Plugin loaded, message selected, running + stopped cyclic jobs, live feed (with a failure), success toast |
| `empty-state.html` | First run | No plugin / Redis offline; every panel's empty placeholder |
| `settings.html` | Settings modal | `SettingsDialogComponent` over the dashboard — Redis connection + plugin load/unload |
| `mocks.css` | — | The shared design system + all component styles |

## How they map to the app

The single stylesheet `mocks.css` consolidates the global design system from
`frontend/src/styles.scss` together with each component's styles. Angular isolates
component styles via view encapsulation; here the component-specific overrides are
qualified under their panel's root class (`.bar`, `.catalog`, `.composer`, `.jobs`,
`.feed`, `.dialog`) so one global sheet reproduces the UI faithfully.

| Mock region | Component |
| --- | --- |
| Header bar | `HeaderBarComponent` |
| Left panel (catalog) | `CatalogComponent` |
| Center panel (composer) | `ComposerComponent` |
| Right top (jobs) | `JobsComponent` |
| Right bottom (activity) | `ActivityFeedComponent` |
| Modal | `SettingsDialogComponent` |
| Shell + toast | `DashboardComponent` |

## Notes

- **Presentation only.** Inputs/buttons are inert; the sample data (Sample Messages
  plugin, channels like `telemetry.heartbeat`, the Heartbeat payload) mirrors what the
  bundled sample plugin actually produces.
- These are **hand-maintained**. If the frontend's layout or design system changes,
  update `mocks.css` and the affected screen(s) to match.
