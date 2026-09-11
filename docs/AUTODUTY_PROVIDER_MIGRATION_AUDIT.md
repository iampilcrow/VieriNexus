# AutoDuty provider migration audit

Snapshot: 2026-09-09
Vieri source: `a5e1e757e35bd77191a647add7124210cdf86122` (`1.0.0.440`)
Stock upstream: `2b0943ed113da76f3ce9df0df2f302151f828292`
Common ancestor: `17f54e99235d84fe39582258eca7058fc5fb3e2b`

## Decision

The permanent dependency is stock AutoDuty as a replaceable, module-scoped Duty provider. VieriAutoDuty is a migration source and temporary compatibility provider, not the final duty engine inside Nexus. Nexus owns Vieri-specific goals, policy, UI, routes, travel composition, gear/inventory decisions, maintenance scheduling, telemetry, commands, and cross-provider coordination. Stock AutoDuty continues to own its supported duty paths and internal duty state machine.

This matches the stock-Questionable direction: compatible upstream updates should normally require only a provider-contract check, not a Vieri fork merge or a Nexus source change.

## Measured fork delta

Compared with current stock AutoDuty, the Vieri tree contains 105 commits after the common ancestor and changes 110 files: 8,712 insertions and 1,156 deletions. The changed-file distribution is 70 helpers, 18 tests, 9 core files, 6 UI/config files, 4 IPC files, one duty path, and two root/provenance files.

Stock upstream is currently one substantive commit plus its merge ahead of the fork's common ancestor. That change corrects character gathering to use the home world rather than the current world. The Vieri final tree still uses the current world at that call site, so this is a pending one-line upstream correction while the fork remains in service; it must be integrated and released through the normal VieriAutoDuty verification path, not hidden inside this documentation-only audit. The source lock already records the current Vieri and upstream revisions. This audit classifies the final tree difference; it does not assume that every historical intermediate commit still represents distinct live behavior.

## Why Nexus used to say “through VieriAutoDuty”

Nexus Routes calls three fork-only endpoints:

- `AutoDuty.TravelVieriRoute`
- `AutoDuty.StopVieriRouteTravel`
- `AutoDuty.IsNavPlotterVisualizationActive`

Those endpoints accept the Nexus route payload, perform city/inn/zone travel, run the authored points, expose visualization ownership, and stop the tracked trip. They do not exist in stock AutoDuty. Nexus therefore prefers the fork for every complete route trip when it is loaded, even for a same-territory route.

The live 0.1.0.25 test accepted this temporary bridge in both directions, including ordinary vendor and Grand Company inn travel. Version 0.1.0.27 removes it: Nexus now owns the route-trip state, uses stock Lifestream for teleport/Aethernet/Grand Company inn entry, and uses vnavmesh for the authored path. Live testing found that the first Nexus provider sent mesh-assisted points directly to `Path.MoveTo`, producing a literal line instead of a calculated corridor path. Version 0.1.0.28 corrected that boundary but passed Faezghim's saved flight permission into ground-only Limsa, where vnavmesh correctly reported that no flight volume was built. Version 0.1.0.29 suppresses flight in unsupported territories and provides one ground fallback for missing flight paths. The user accepted both the inn and Faezghim flows; the three fork-only AutoDuty route endpoints remain absent and the route retirement step is accepted.

## Stock IPC capability boundary

Current stock AutoDuty publicly exposes configuration listing/get/set and override push/pop; duty `Run`, `Start`, and `Stop`; `IsNavigating`, `IsLooping`, and `IsStopped`; `ContentHasPath`; leveling-mode selection; and the Wrath lease callback.

The following Vieri endpoints are additions and cannot be assumed on stock:

| Vieri endpoint | Current purpose | Permanent owner/replacement |
| --- | --- | --- |
| `IsPaused` | Companion control/status | Nexus task/provider state; use a capability check if stock later adds it |
| `IsGearReadinessBusy` / `StartGearReadiness` | Shared pre-duty gear transaction | Nexus Gear & Inventory module |
| `IsNavPlotterVisualizationActive` | Filter route drawing to owned travel | Nexus route execution state |
| `TravelVieriRoute` / `StopVieriRouteTravel` | Whole Nexus route trip | Nexus travel orchestrator over Lifestream, vnavmesh, and interaction adapters |
| `StartProgressionLeveling` | Long-running target-level loop | Nexus Progression goal scheduling one bounded duty task at a time |
| `ExecuteVieriCommand` | Start/stop/leave/pause/resume/loops/sell/repair/inn/job gateway | Nexus command gateway plus narrow stock-provider calls |
| `GetVieriStatus` | Character, duty, queue, gear, durability, and completion telemetry | Nexus world snapshots, provider observations, and activity history |

Stock AutoDuty is therefore usable now for bounded duty start/stop and path eligibility, but it cannot replace VieriAutoDuty in production until the Vieri-only responsibilities below have moved or been proven unnecessary.

## Custom behavior inventory and destination

### 1. Nexus route and vendor bridge

Current anchors include `VieriRouteTravelHelper`, `VieriRoutePlaybackContract`, `NexusNavigationRouteContract`, the Nexus/NavPlotter subscribers, the fork-only route IPC endpoints, special Grand Company inn destinations, and Nexus vendor-override consumption.

Destination: Nexus Routes & Navigation. Implemented in 0.1.0.27 and corrected/accepted through 0.1.0.29: the 27 measured vendor templates and exact assignment model live in Nexus, and Nexus owns cross-zone transfer coordination, exact Grand Company inn entry, territory-aware mesh-path calculation for each authored leg, direct non-mesh playback, visualization state, provider failure reporting, and Stop without calling a fork-only AutoDuty route endpoint.

### 2. Vendor travel data and arrival policy

This includes measured standing points, separate NPC object coordinates, native interaction checks, authored-point priority, collision avoidance, short-leg walking, destination-region aetheryte selection, flight policy, bounded stall recovery, and the retired Old Sharlayan stair sequence.

Destination: Nexus Routes & Navigation plus shared Travel Utilities. Generic correctness improvements that benefit stock AutoDuty should be proposed upstream, but Nexus must retain its route fixtures independently.

### 3. Gear readiness and shopping

This is the largest Vieri-owned domain: vendor-band/catalog selection, empty/weak slot handling, job-primary-stat filtering, explicit preview and approval, gil reserve, owned-item recovery, EXP-item protection, main-hand-first ordering, two-handed/off-hand rejection, verified equipping with one retry, gearset update, displaced-equipment cleanup, telemetry, and VieriCodex readiness coordination.

Destination: Nexus Gear & Inventory. Stock AutoDuty may request a readiness outcome before a duty, but it should not own Nexus selection policy or the user-facing shopping planner.

### 4. Inventory and maintenance

Custom work covers protected selling, bag thresholds and preferred vendors, repair, extraction confirmation, desynthesis/Armoire/Glamour ordering, minion/orchestrion registration, deferred displaced-item transfers, and safe in-duty withdrawal/maintenance/resume policy.

Destination: Nexus Gear & Inventory and task orchestration. Low-level UI operations remain behind focused adapters. Generic AutoDuty defects may be upstreamed; Vieri policy and scheduling remain in Nexus.

### 5. Progression, loops, and Last Run

Custom work includes target-level looping, delayed duty selection after gear changes, gear-readiness coordination, Last Run, cooperative VieriCodex queue stop, pause/resume handoff, duty timing, and completion events.

Destination: Nexus Progression and Duties orchestration. Nexus should schedule one bounded stock-AutoDuty duty run, verify completion, and decide whether another task is needed. Last Run then means “do not schedule another duty,” rather than requiring a custom endless loop inside AutoDuty.

### 6. Duty-engine corrections

The history contains wipe/death/shortcut recovery, stale-path cancellation, Ktisis teleporter work, chest timing, boss encounter handling, combat target range, gaze handling, and explicit restorations of proven stock behavior. One duty path file still differs from current upstream.

Destination: stock AutoDuty wherever the behavior is generic. Before retiring the fork, compare each remaining final-tree duty difference with current upstream, contribute generally useful fixes upstream where practical, and retain Nexus provider-contract/replay tests for the required outcomes. Nexus must not absorb the stock dungeon engine merely to preserve a fork patch.

### 7. Integration, control, and status

Custom work coordinates VieriCodex, VieriLink, VieriRotationHelper/Wrath, Avarice, Boss Mod, Discord status, remote control, live location/level/durability, queue readiness, and gear events.

Destination: Nexus command gateway, world snapshots, provider health, activity history, Communications, Progression, and Combat ownership. Cross-module behavior must not remain as peer plugins calling private Vieri endpoints.

### 8. UI, branding, and packaging

Vieri branding, the cleaned-up categorized overlay actions, manual shopping windows, striking-dummy menus, support-link changes, tags, versioning, and the Vieri changelog differ from stock.

Destination: Nexus owns and preserves the custom overlay experience—not stock AutoDuty's overlay—including the compact categorized Goto, Gear, Inventory, and Extras actions; striking-dummy destinations; manual Shop for Upgrades review; and the useful duty controls/status. These are rebuilt as coherent Nexus UI over Nexus commands and narrow provider capabilities. Fork branding and duplicate AutoDuty windows retire; the user-facing functionality and cleaner organization do not.

### 9. Tests and provenance

The 18 custom test files and `VIERI_CHANGELOG.md` preserve important regression evidence. Tests should move with their owning Nexus policy or become stock-provider contract/replay fixtures. Historical source and license provenance remains pinned even after the fork is retired.

## Persisted VieriAutoDuty state that requires disposition

The final configuration diff identifies 21 Vieri-added fields/state collections that cannot be silently lost: retired-equipment transfers; the Sell action toggle; smart gil-vendor buying and gil reserve; minion and orchestrion registration; automatic selling mode; occupied-slot and bag-percent thresholds; gearset protection; preferred seller; and the safe in-duty maintenance, durability, inventory, extract, desynthesis, and return-to-inn settings.

Each receives one of three outcomes before retirement: an exact Nexus mapping, a documented compatibility default, or an explicit user-approved retirement reason. The source configuration remains untouched and rollbackable.

## Retirement sequence

1. Keep VieriAutoDuty authoritative while migration is incomplete.
2. **Implemented through 0.1.0.29 and live-accepted:** replace the fork-only Nexus route-trip bridge with Nexus-owned Lifestream/vnavmesh travel composition. Nexus no longer references `TravelVieriRoute`.
3. **Implemented in 0.1.0.30, compatibility-corrected in 0.1.0.31, and live-accepted:** build the Progression proof using the stock-compatible AutoDuty contract. Nexus selects one exact eligible duty, disables AutoDuty's internal leveling scheduler through either `SetLevelingMode` or the older stock `SetConfig("leveling", "None")` path, requests one loop, holds the full resource bundle, requires the matching game duty-completed event plus safe return, and decides whether to stop or schedule exactly one fresh task. It never calls the Vieri-only endless Progression endpoint. The user accepted Start, Stop, fresh-plan Resume, and repeated Mt. Gulg dispatch. Questionable quest execution remains planning-only.
4. **Gear ownership completed in 0.1.0.36:** Nexus creates the durable pre-duty gear task, owns its full resources and gil floor, and uses the same exact native approval for manual and automatic work. It reads current/owned equipment and real gil-shop catalogs, selects the vendor band/job family, ranks replacements, protects EXP and two-handed layouts, pins vendor identity, travels through Nexus's Lifestream/vnavmesh route provider, selects only relevant combat-shop pages, confirms exact purchases, verifies each equipped slot, updates the current gearset, and moves only transaction-displaced items out of the Armoury Chest. Failed or stopped transactions do not advance the completion sequence or dispatch a duty. No VieriAutoDuty gear IPC remains.
5. **Operations preservation completed in 0.1.0.37:** Nexus transactionally stages the complete VieriAutoDuty profile/character mapping, retired-item transfers, overlay choices, and every identified custom maintenance policy group with source backup, atomic target, SHA-256 receipt, reload verification, and guarded rollback. A compact Nexus-owned Goto/Gear/Inventory/Extras overlay now presents only working Nexus route, shopping, Progression, Last Run, status, and Stop controls; unfinished actions are omitted rather than exposed as dead UI.
6. **Safe native maintenance and striking-dummy travel implemented in 0.1.0.38:** Nexus promotes the verified operations import into an independent atomic working library, resolves the current character's profile, and directly executes self-repair, materia extraction, Triple Triad/minion/orchestrion registration, and eligible coffer opening under exclusive UI/inventory leases and explicit Stop. The preserved overlay exposes those actions and the full striking-dummy destination catalog; Lifestream performs only compatible travel and vnavmesh performs the final approach. Selling, desynthesis, Grand Company turn-ins, Armoire/Glamour ordering, and in-duty withdrawal remain pending behind destructive-item review and recovery contracts.
7. Audit the remaining duty-engine tree diff against then-current stock AutoDuty. Upstream generic fixes or prove the stock behavior equivalent; do not copy the full duty engine into Nexus.
8. Run coexistence, provider-loss, duty completion, Last Run, gear interruption, reload, maintenance recovery, and clean stock-provider tests.
9. Only then enable stock AutoDuty beside Nexus by default and retire the VieriAutoDuty package/feed entry through the deliberate retirement process.

## Non-goals

- Do not enable stock AutoDuty beside VieriAutoDuty while both could act.
- Do not copy the full stock duty engine or its path tree into Nexus.
- Do not expose the fork's branding as a Nexus module.
- Do not treat a matching method name as a capability handshake.
- Do not retire the fork until configuration, behavior, IPC, recovery, in-game, and rollback parity are demonstrated.
