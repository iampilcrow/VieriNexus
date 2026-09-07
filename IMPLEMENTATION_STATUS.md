# VieriNexus implementation status

## Foundation implemented

- One Dalamud plugin package with separate Domain, Application, Contracts, and Plugin assemblies.
- Polished dark/red/gold application shell based on the supplied visual direction.
- Branded VieriNexus Home experience using the supplied logo; no temporary splash window.
- All Nexus windows remain hidden until a targetable character has settled in the world.
- Mandatory dependency setup gate with six core required providers and a separately labeled recommended-integration catalog audited from every current Vieri product.
- Dependency health detection and focused Install/Enable/Manage actions through Dalamud's plugin installer.
- Eight neutral built-in module registrations.
- Read-only discovery of all eight predecessor configuration sources.
- Explicit Discord credential preservation notice and migration boundary.
- Global and content-ID/world-scoped character configuration.
- Immutable, revisioned world snapshots with unknown-state handling.
- Atomic resource lease manager with implied ownership for navigation, combat, retainer, and market work.
- Versioned public status and dependency IPC contracts.
- Upstream source lock and update workflow.
- Initial automated tests for ownership atomicity and world-state sequencing.
- Provider-neutral solo-duty combat policy requiring a fresh rotation-automation handoff on duty entry while forbidding Nexus from rewriting hard targets or competing with the encounter provider for movement.
- Regression coverage for the VieriCodex 1.12.2.74-76 solo-duty incident: selected-target behavior stays primary, nearest-hostile action targeting activates only after a sustained targetless gap, yields immediately when normal targeting recovers, automatically expires after a bounded assist window, and never owns encounter movement.

## Intentionally not enabled yet

- No predecessor settings have been imported.
- No Discord credential, channel, or message ID has been read or changed.
- No existing Vieri plugin has been disabled, uninstalled, or modified.
- No live automation, HUD replacement, combat hook, market action, or remote command has been enabled.
- Dependency actions open Dalamud at the exact plugin entry; Nexus does not yet call Dalamud's private installer internals.
- Questionable and all eight migration-source products are intentionally excluded from the third-party dependency catalog.

## Next implementation gate

Build transactional configuration importers and compatibility tests for each source before migrating the first live module. The first authoritative gameplay behavior should not ship until resource ownership, pause/stop/manual override, reload reconciliation, and rollback are exercised against an existing workflow.
