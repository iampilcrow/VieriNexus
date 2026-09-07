# VieriNexus implementation status

## Foundation implemented

- One Dalamud plugin package with separate Domain, Application, Contracts, and Plugin assemblies.
- Polished dark/red/gold application shell based on the supplied visual direction.
- Branded VieriNexus Home experience using the supplied logo; no temporary splash window.
- All Nexus windows remain hidden until a targetable character has settled in the world.
- Mandatory dependency setup gate with six core required providers and a separately labeled recommended-integration catalog audited from every current Vieri product.
- Dependency health detection and focused Install/Enable/Manage actions through Dalamud's plugin installer.
- Nine neutral built-in module registrations, including Routes & Navigation.
- Read-only discovery of all nine predecessor configuration sources, including VieriNavPlotter.
- Route migration requirements preserve searchable names, stable IDs, notes, tags, ordered points, territory, playback settings, and explicit assignments without enabling disabled overrides.
- VieriNavPlotter's visible built-in baseline includes all 27 current AutoDuty gear-vendor destinations and distinguishes the two authored multi-point approaches from destination-only navmesh behavior.
- Route migration now explicitly preserves the review and execution controls: persistent in-world Show Route previews, Travel to Start/Destination, complete point-by-point Play Route, cross-zone AutoDuty travel ownership, and a working Stop Playback path.
- Navigation parity now includes the complete live vnavmesh waypoint chain, a resizable route-library pane, exact copyable player coordinates, and the corrected Domitien approach that avoids both wall-side endpoints.
- Explicit Discord credential preservation notice and migration boundary.
- Global and content-ID/world-scoped character configuration.
- Immutable, revisioned world snapshots with unknown-state handling.
- Atomic resource lease manager with implied ownership for navigation, combat, retainer, and market work.
- Versioned public status and dependency IPC contracts.
- Upstream source lock and update workflow.
- Initial automated tests for ownership atomicity and world-state sequencing.
- Provider-neutral solo-duty combat policy requiring a fresh rotation-automation handoff on duty entry while forbidding Nexus from rewriting hard targets or competing with the encounter provider for movement.
- Regression coverage for the VieriCodex 1.12.2.74-76 solo-duty incident: selected-target behavior stays primary, nearest-hostile action targeting activates only after a sustained targetless gap, yields immediately when normal targeting recovers, automatically expires after a bounded assist window, and never owns encounter movement.
- Source provenance now includes VieriCodex 1.12.2.77, preserving flight for every resumable `Sage's Focus` Dravanian Hinterlands duty approach.
- Source provenance now includes VieriAutoDuty 1.0.0.417, preserving the complete manual Shop for Upgrades transaction, the validated route playback/travel contract consumed by VieriNavPlotter, and the safe Domitien aisle-to-interaction approach.

## Intentionally not enabled yet

- No predecessor settings have been imported.
- No Discord credential, channel, or message ID has been read or changed.
- No existing Vieri plugin has been disabled, uninstalled, or modified.
- No live automation, HUD replacement, combat hook, market action, or remote command has been enabled.
- Dependency actions open Dalamud at the exact plugin entry; Nexus does not yet call Dalamud's private installer internals.
- Questionable and all eight migration-source products are intentionally excluded from the third-party dependency catalog.

## Next implementation gate

Build transactional configuration importers and compatibility tests for each source before migrating the first live module. The first authoritative gameplay behavior should not ship until resource ownership, pause/stop/manual override, reload reconciliation, and rollback are exercised against an existing workflow.
