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
- Navigation parity now includes the complete live vnavmesh waypoint chain, scoped strictly to NavPlotter-owned playback/travel and explicitly authorized gear-shopping movement so ordinary Goto and dungeon paths remain hidden. It also retains a resizable route-library pane, exact copyable player coordinates, and Domitien's direct approach from `Territory 133 | X 164.4264 | Y 15.5000 | Z -75.7035` to the measured standing point `X 157.5930 | Y 15.7000 | Z -69.3316` with a stable 0.75-yalm arrival radius.
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
- Source provenance now includes VieriAutoDuty 1.0.0.435 and VieriNavPlotter 1.0.0.16, preserving the complete manual Shop for Upgrades transaction, validated route playback, explicit visualization ownership, and hidden ordinary Goto/dungeon navigation. Gear planning, native recommendations, Gearsetter recommendations, and final verified equipping reject an off-hand whenever the effective main hand is two-handed; main-hand recommendations execute first so legitimate one-handed weapon plus shield sets remain supported on every applicable job. Long same-zone vendor journeys use an unlocked destination aetheryte when it changes regions, regional overworld routes retain flight permission for long post-teleport approaches, adjacent vendor legs under 100 yalms bypass Aethernet and remain on direct foot navigation, and walking requests can never mount merely because zone flight is unlocked. Distinct authored standing points always finish before interaction eligibility can end travel; raw NPC-coordinate routes retain native early stopping. Measured standing points are preserved separately from NPC coordinates for Geraint; the three Limsa gear vendors Iron Thunder `-155.3658, 18.2000, 23.3950`, Faezghim `-236.5439, 16.2000, 40.3006`, and Sorcha `-135.1727, 18.2000, 14.8682`; the three Ishgard gear vendors Seghuie `-189.1842, -12.6349, -40.0551`, Elbert `-216.0509, -16.1262, -60.4229`, and Norlaise `-205.2957, -16.1349, -51.2569`; the Kugane accessories `29.9279, 4.0000, 52.4925`, weapons `35.2371, 4.0000, 52.5185`, and armor `40.1606, 4.0000, 52.5056` vendors; the level-64 accessories vendor `-283.8307, 17.3200, 492.3687`, level-66 accessories vendor `171.1704, 5.1697, -421.6375`, and level-68 accessories vendor `-249.5169, 257.5265, 750.1727`; and the Crystarium accessories `-120.8424, -1.0766, 126.7847`, first gear `-129.5804, -1.0767, 112.0974`, and second gear `-122.9644, -1.0765, 99.1908` vendors.
- Source provenance now includes VieriAutoMarket 1.0.0.13, preserving crash-safe exact price matching between owned retainers, a settled comparison-window handoff, complete native confirmation event data, saved-price verification, and logical-market selection that rejects the full suspicious low-price cluster behind a 1-gil listing instead of following it downward.

## Intentionally not enabled yet

- No predecessor settings have been imported.
- No Discord credential, channel, or message ID has been read or changed.
- No existing Vieri plugin has been disabled, uninstalled, or modified.
- No live automation, HUD replacement, combat hook, market action, or remote command has been enabled.
- Dependency actions open Dalamud at the exact plugin entry; Nexus does not yet call Dalamud's private installer internals.
- Questionable and all eight migration-source products are intentionally excluded from the third-party dependency catalog.

## Next implementation gate

Build transactional configuration importers and compatibility tests for each source before migrating the first live module. The first authoritative gameplay behavior should not ship until resource ownership, pause/stop/manual override, reload reconciliation, and rollback are exercised against an existing workflow.
