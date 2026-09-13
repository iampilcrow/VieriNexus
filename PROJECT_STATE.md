# PROJECT STATE

Working snapshot: 2026-09-13 (America/New_York)
Repository: `D:\FFXIV Plugins\VieriNexus`  
Current product version: `0.1.0.62`
Current published source: `0.1.0.61`, source `045ddf52cc7b2909be3aeb4eaf513119ca6e5cc6`, website release `929b1e128a3e9b1e72982963b7cd2268c30c2f6d`, verification documentation `d6fa76f5f772fb372a61c0c54efd437683b97f68`. The unrelated untracked `rustdesk-1.4.9-x86_64.exe` remains untouched.
Current workstream: version 0.1.0.62 hardens the completed one-package transition. Dalamud entries that share an internal identity are collapsed deterministically, with the loaded/highest-version entry preferred; this prevents disabled VieriAutoDuty from masking loaded stock AutoDuty and prevents the Plugins page from throwing on the duplicate `AutoDuty` key. The Nexus operations overlay restores the approved VieriAutoDuty category coverage: routes, Inn, Grand Company destinations, flag marker, housing, market board, summoning bells, Triple Triad, striking dummies, gear and inventory maintenance, duty controls, and collectible registration. Lifestream and Nexus route travel remain interruptible through the same Stop surface. Historical paused-duty reconciliation remains available on Progression but no longer appears as an unrelated message on an idle overlay. Version 0.1.0.61 remains the embedded-runtime consolidation foundation.

Production Dalamud custom repository URL: `https://www.thedailypilcrow.com/dalamud/pluginmaster.json`  
GitHub repository: `https://github.com/iampilcrow/VieriNexus.git`  
Operational rule: a published release is not complete until source, packages, public hosting/feed, Dalamud install/update visibility, validation/hashes, project-state documentation, and the release Discord-bot/changelog update are handled and verified according to the established production workflow.

This document combines three kinds of evidence:

- **VERIFIED IMPLEMENTATION** means the behavior is present in the current VieriNexus repository.
- **RECOVERED DECISION/REQUIREMENT** means it was explicitly established in the predecessor Codex conversation or its attached planning brief.
- **PLANNED** means it is architecture or roadmap, not current runtime behavior.

The repository is authoritative for what exists today. The recovered conversation and this document preserve product intent, migration requirements, and decisions that are not necessarily visible in the code.

## 1. Project Overview

### Name and purpose

`VieriNexus` is the permanent product and Dalamud internal name for the planned unified Vieri FFXIV suite. It is intended to replace the separately installed Vieri plugins with one coherent, modular Dalamud package. It is not intended to be a launcher for separate plugins, a collection of embedded predecessor windows, or one giant controller.

The current production line is the unified Vieri runtime. Versions 0.1.0.4 through 0.1.0.60 established transactional migration, Nexus-owned routes, progression and multi-job queueing, native gear and maintenance transactions, Progress Atlas, stock Questionable compatibility, the favorites-first Plugins page, the compact operations overlay, Fast Job Switcher integration, and correct player-facing job names. Version 0.1.0.61 packages the remaining custom Rotation, Positional, HUD, Market, and Link engines inside Nexus with isolated dependencies and settings. Version 0.1.0.62 makes stock AutoDuty coexistence deterministic when its disabled predecessor shares the same Dalamud identity and restores the full approved operations-overlay category surface. VieriDeck and VieriCodex are retired runtime products; after per-computer preparation, the remaining standalone Vieri plugins can be disabled while stock Questionable and stock AutoDuty provide ordinary quest/duty mechanics.

Version 0.1.0.61 completes the one-package runtime consolidation by embedding the five remaining custom engines under isolated load/configuration contexts and redirecting VieriLink to Nexus's own status and command gateway. VieriDeck, VieriCodex, VieriNavPlotter, VieriRotationHelper, VieriAvarice, VieriDelvUI, VieriAutoMarket, VieriLink, and the custom responsibilities formerly carried by VieriAutoDuty can all transition out of the runtime. Stock Questionable and stock AutoDuty remain external mechanics providers; Boss Mod, vnavmesh, Lifestream, and Fast Job Switcher remain narrow external dependencies. The complete isolation, settings-handoff, secret, and provider boundaries are recorded in `docs/EMBEDDED_MODULE_ARCHITECTURE.md`.

The complete VieriCodex migration scope—Hunting and Grand Company logs, Aetheryte discovery, all Aether Currents, world exploration, achievements, and the unified Progress Atlas—is implemented as Nexus-owned live game-state functionality and remains independent after VieriCodex is disabled.

### Product vision

The long-term experience is outcome-driven. A user should state a durable desired result—such as reaching a job level, acquiring items, completing achievements, finishing a relic, earning currency, or completing recurring activities—and Nexus should:

1. Observe the actual character/world state.
2. Resolve prerequisites and constraints.
3. Produce a short-horizon plan of bounded tasks.
4. Select eligible internal or third-party providers.
5. Grant exclusive authority over movement, targeting, combat, UI, inventory, market, and other contested resources.
6. Execute, verify, checkpoint, recover, and re-plan as the world changes.
7. Explain what it is doing, why, who owns the action, what comes next, and what blocks progress.

The intended architectural style is a **modular monolith**: one installed `VieriNexus` package containing multiple bounded assemblies/modules with inward dependency direction, stable contracts, shared state, and one ownership model.

### Intended user experience

- One polished, expandable control-center UI rather than ordinary plugin-style utility windows.
- Dark near-black panels with restrained red highlights, gold headings, clear green/amber/red status, compact navigation, and strong visual hierarchy.
- The supplied `VieriNexusLogo.png` is part of the permanent Home page. It must not return as a separate transient splash popup, and the removed redundant header/logo/tagline strip must not return.
- Dependencies have a dedicated setup page. Required providers gate entry; recommended integrations are visibly separate and do not block setup.
- Automation remains understandable and interruptible: pause, stop, safe checkpointing, manual override, recovery, and clear status are first-class.
- Each user/character keeps its own configuration. One user's or friend's data must never inherit another user's settings, progression, secrets, or identifiers.

### Long-term direction

The final installed topology is:

```text
VieriNexus (the only installed Vieri product)
  + genuine external providers required by enabled modules
```

The eight original migration sources are:

1. VieriAutoDuty
2. VieriAutoMarket
3. VieriAvarice
4. VieriCodex
5. VieriDeck
6. VieriDelvUI
7. VieriLink
8. VieriRotationHelper

`VieriNavPlotter` was added afterward as the ninth migration source and becomes the shared Routes & Navigation module.

`VieriWrathSwitch` is not an additional Nexus migration source: its behavior and compatibility surface were already absorbed into VieriRotationHelper. `VieriHildaLayer` is obsolete and explicitly excluded. `MarkerIconPriority` was already absorbed into VieriDelvUI and migrates as an optional Custom UI/Nameplates behavior, not as a standalone module or plugin.

Future expansion may include crafting, gathering, farming, dailies/weeklies, procurement, retainers, currencies, collections, relics, schedules, hunts, fishing, events, and other domains. These are extension directions, not present foundation features.

### Scope boundaries

- Nexus should coordinate existing strong providers rather than reimplementing Boss Mod, vnavmesh, Lifestream, TextAdvance, Marketbuddy, or Allagan Market without a proven reason.
- VieriCodex is a settings-migration source only. Nexus owns its planners, policies, safety fixes, queue, Atlas, custom routes, and UI; stock Questionable is the sole ordinary quest-execution provider through the narrow capability-versioned adapter.
- VieriAutoDuty remains authoritative during its migration. Nexus will own its Vieri-specific route/travel, progression-loop, Last Run, gear/inventory, maintenance, telemetry, command, and UI behavior, then use stock AutoDuty through a module-scoped capability/versioned adapter for supported duty execution. The full fork retires only after configuration, behavior, IPC, recovery, in-game, and rollback parity.
- Per-frame combat decisions stay inside the embedded Wrath-derived combat engine. The slower planner grants or denies authority but must not enter that hot path.
- Remote commands may never bypass local authorization, current-character checks, ownership, manual override, or safety policy.
- Natural-language goals may be a future frontend, but the engine should execute structured, versioned goal models.

## 2. Architecture Overview

### Current implemented architecture

The current solution contains four production assemblies and one test assembly:

```text
VieriNexus.Plugin
  -> VieriNexus.Application
  -> VieriNexus.Domain
  -> VieriNexus.Contracts

VieriNexus.Application
  -> VieriNexus.Domain

VieriNexus.Domain       (no project references)
VieriNexus.Contracts    (no project references)
```

- `VieriNexus.Domain` holds identifiers, goal/task records, resource/failure enums, module contracts, and immutable world-snapshot types. It has no Dalamud dependency.
- `VieriNexus.Application` holds dependency metadata, the module registry, resource-lease primitive, solo-duty combat policy, world-state store, and transactional navigation migration logic. It has no Dalamud dependency.
- `VieriNexus.Contracts` holds versioned public IPC names and DTOs.
- `VieriNexus.Plugin` is the current Dalamud entry point and composition root. It also contains game-state observers, dependency/legacy discovery, migration orchestration, IPC registration, and all ImGui UI code.
- `VieriNexus.Application.Tests` tests pure application/domain behavior.

At startup, `Plugin.Plugin()` loads `Configuration`, creates migration and working stores, provider adapters, Routes, Progression, Progress Atlas, Gear, maintenance, the shared `NexusControlService`, neutral built-in `ModuleRegistry`, `WorldStateStore`, `WorldSnapshotObserver`, `NexusWindow`, the custom operations overlay, and `NexusIpcProvider`; registers `/vierinexus` and `/nexus`; and attaches Dalamud UI callbacks.

On each draw callback, `WorldSnapshotObserver.Update(...)` publishes at most every 250 ms. Navigation safety, authority, guarded execution, recording capture, recovery, and diagnostics update even while the Nexus window is closed. `GameplayReadyGate.Evaluate(...)` prevents the window system from drawing until the player is logged in, targetable, has a territory, is not between areas, and has remained eligible for 900 ms. The main window then renders navigation and the selected page.

### Planned end-state architecture

The target remains a modular monolith with conceptual layers/assemblies for Plugin, Contracts, Domain, Application, Dalamud infrastructure, UI, feature modules, provider adapters, and tests. Dependency direction must remain inward:

```text
Plugin / UI / Infrastructure / Modules / Providers
                         |
                         v
                    Application
                         |
                         v
                       Domain
```

Modules register goal schemas, satisfaction evaluators, planners, task handlers, capability providers, dependency manifests, state observers, resource requirements, settings schemas/migrations, UI contributions, and sanitized diagnostics through a boundary conceptually represented by `INexusModule`. The application core must not grow a central switch statement for every future feature.

Cross-module work communicates through desired-state requirements, capabilities, commands, facts/events, immutable snapshots, and resource leases. Modules must not call another module's private implementation. UI, hotkeys, IPC, and Discord should eventually enter through one command gateway.

### Current-versus-target warning

Several target concepts are now live but remain specialized rather than a fully general scheduler. `NexusGoal`/`NexusTask` drive the character-scoped Progression coordinator; `NexusCommandDto` drives the registered command gateway; Routes, Atlas, Gear, maintenance, and Progression each have bounded executors over the shared resource leases. Nexus still does not implement a general event bus, durable cross-module audit log, SQLite store, or schema-contributed module scheduler.

## 3. Repository Map

### Root

- `VieriNexus.slnx` — solution containing the four production projects and the application test project.
- `Directory.Build.props` — common `net10.0-windows`, latest C#, nullable, implicit usings, warnings-as-errors, deterministic builds.
- `README.md` — concise current-release description and safety guarantees.
- `IMPLEMENTATION_STATUS.md` — detailed implemented/not-enabled status and pinned predecessor behavior.
- `MASTER_ARCHITECTURE_PLAN.md` — authoritative target architecture, migration sequence, milestone, risks, and non-relaxable rules. Treat its architecture as planned unless code proves otherwise.
- `docs/DEPENDENCY_AUDIT.md` — required/recommended provider inventory and present-day Vieri consumer evidence.
- `docs/MIGRATION_AND_UPSTREAM_POLICY.md` — non-negotiable migration, safety, route, gear, market, rotation, and upstream-update guardrails.
- `docs/AUTODUTY_PROVIDER_MIGRATION_AUDIT.md` — exact stock/fork revisions, measured 108-commit/112-file custom delta, stock IPC boundary, Vieri behavior inventory, persisted-state inventory, target ownership, and fork-retirement gates.
- `docs/QUESTIONABLE_PROVIDER_MIGRATION_AUDIT.md` — exact stock/fork revisions, measured final-tree delta, permanent single-quest IPC boundary, Vieri behavior ownership inventory, protected regressions, and fork-retirement gates.
- `upstreams/source-lock.json` — exact pinned commits for all nine migration-source repositories plus upstream provenance where applicable.
- `.gitignore` — excludes build and IDE output (`bin/`, `obj/`, `.vs/`, `dist/`, `artifacts/`). Generated folders may exist locally but are not architecture or source.

### `src/VieriNexus.Domain`

- `Identifiers.cs` — `GoalId`, `TaskId`, `AttemptId`, `CharacterKey`, `GoalKind`, `TaskKind`, `CapabilityId`, and `ProviderId`. `CharacterKey` uses content ID plus home world and formats as hexadecimal content ID plus world ID.
- `Goals.cs` — `GoalStatus`, `ConstraintStrength`, `GoalConstraint`, and versioned `NexusGoal` desired-state record.
- `Tasks.cs` — `NexusTaskStatus`, `FailureKind`, `ResourceKind`, `TaskFailure`, and `NexusTask`.
- `WorldSnapshot.cs` — `KnowledgeState`, generic `Observed<T>`, session/character/provider slices, and immutable revisioned `WorldSnapshot`.
- `Modules.cs` — `ModuleDescriptor` and current minimal `INexusModule` registration contract.

### `src/VieriNexus.Application`

- `DependencyCatalog.cs` — all seven required and fifteen recommended external dependency descriptors, including required Fast Job Switcher.
- `ModuleRegistry.cs` — case-insensitive, duplicate-rejecting module registry.
- `ResourceLeaseManager.cs` — atomic in-memory acquisition, implied-resource expansion, lease heartbeat/expiry, exactly-once watchdog expiration drain, snapshot, and `IDisposable` release.
- `NavigationStopCoordinator.cs` — provider-neutral, idempotent verified-Stop state machine that retains a tracked execution lease unless movement is explicitly confirmed inactive.
- `ManualMovementSafetyCoordinator.cs` — retained historical manual-takeover policy primitive and regression fixture; it is no longer connected to production route input after 0.1.0.25 made route cancellation explicit-Stop-only.
- `NavigationExecutionIntentStore.cs` — minimal versioned execution/route/lease/state journal with validation and same-directory atomic replacement; no resumable instruction pointer.
- `NavigationExecutionSafetyCoordinator.cs` — every-draw reload/shutdown and lease-expiry reconciler that can Stop and checkpoint but cannot replay movement.
- `NavigationAuthorityCoordinator.cs` — automatic session-only route authority while VieriNavPlotter is off, source/safety revocation, and the guarded local execution-entry gate that acquires resources and arms no-replay Stop before direct provider movement.
- `NavigationDiagnosticsMonitor.cs` — pure bounded session monitor that records provider state/code transitions without per-frame audit flooding.
- `NavigationSafetySimulator.cs` — isolated memory-only six-scenario exercise of the production navigation safety coordinators, including provider loss/retry; its provider has Stop observation only and no movement operation.
- `NavigationRecoveryCoordinator.cs` — retained no-replay recovery composition. Production route user cancellation now enters only through explicit Stop; manual-yield state is an inert compatibility seam.
- `NavigationLibraryStore.cs` — atomic Nexus-owned working-library persistence, validation, and previous-file recovery copy, deliberately separate from immutable migration staging.
- `NavigationRoutePlanner.cs` — side-effect-free Review, Travel to Start, and Playback planning with one-or-more ordered points, distance, validation, and territory gating.
- `NavigationRouteDispatchPolicy.cs` — provider-neutral selection that consistently prefers suite travel for both same-zone and cross-zone trips, with raw local vnavmesh as a same-zone fallback.
- `NavigationBuiltInRouteCatalog.cs` — immutable 27-route catalog of verified VieriAutoDuty vendor standing points with target/territory provenance and disabled assignments.
- `NavigationRouteTargetBinding.cs` — target/kind binding policy that never enables an override implicitly and rejects cross-territory capture for populated routes.
- `NavigationSuiteRouteRequest.cs` — additive JSON suite-travel request/parser plus a separate 27-target NPC fallback catalog that never replaces authored movement points.
- `NavigationSuiteTravelCoordinator.cs` — provider-neutral tracking for same-zone or cross-zone travel explicitly dispatched by the current Nexus process, including structured running/completed/failed observations so provider failures cannot be mislabeled as completion.
- `NavigationRouteOverrideResolver.cs` — pure exact-target assignment editor/resolver with one-winner enforcement and fail-closed invalid/ambiguous results.
- `NavigationRouteRecordingCoordinator.cs` — non-moving timed-capture state machine with cadence, spacing, territory, and route-availability guards.
- `NavigationRouteClipboardCodec.cs` — bounded versioned route exchange plus compatible legacy NavPlotter JSON ingestion with regenerated identity and disabled automation assignment.
- `NavigationRouteExecutionCoordinator.cs` — guarded same-zone movement entry, lease heartbeat, natural completion, explicit Stop handling, and shared-provider destination ownership yielding.
- `ProgressionPlanning.cs` — provider roles/flavors/readiness, fail-closed provider selection, current-job level-goal draft with live item-level/gil inputs, validation, warnings, and bounded gear/quest/duty/verification plan construction. `IsExecutionConnected` reports the bounded duty lane; the runtime separately requires the gear adapter before Start.
- `ProgressionExecution.cs` — character-scoped atomic Reach Job Level desired state and task history; separate gear-readiness, exact-quest, and one-duty task kinds; exclusive resource acquisition; hard gil-floor/non-regression, pinned quest-ID, and exact duty-completion verification; task-appropriate provider reconciliation; Stop-after; Stop; and no-replay reload recovery.
- `ProgressionQueue.cs` — Nexus-owned multi-job queue schema, ordered-step/status normalization, imported VieriCodex queue conversion, leveling-method selection, failure policy, and deterministic advancement.
- `ClassJobRoleQuestPolicy.cs` — Nexus-owned stable game-data chapter families for all class, job, crafting/gathering, limited-job, and combat-role quest selection.
- `GeneralSideQuestPolicy.cs` — Nexus-owned ordinary side-quest family boundary excluding MSQ, repeatable, seasonal, Allied Society, Aether Current, no-journal, and Class/Job/Role-owned quests before runtime eligibility checks.
- `ClassJobDisplay.cs` — localized player-facing full class/job name plus abbreviation formatting without leaking numeric row IDs.
- `ProgressAtlas.cs` — provider-independent live completion categories, loaded-state handling, bounded progress, and aggregate totals.
- `HuntingLogPlanning.cs` — provider-neutral complete/current/open-world target eligibility and deterministic next-target selection independent of VieriCodex.
- `WorldStateStore.cs` — current immutable snapshot reference plus change event and monotonic revision check.
- `SoloDutyCombatPolicy.cs` — preserved provider-neutral policy for the VieriCodex 1.12.2.74–76 targeting incident.
- `MigrationModels.cs` — route-library snapshots, issues, receipts, write results, and verified staged-state read results.
- `NavigationRouteMigrationImporter.cs` — deserializes and validates the VieriNavPlotter JSON shape and maps every supported field.
- `TransactionalMigrationStore.cs` — timestamped source backup, prior-target backup, atomic Nexus write, SHA-256 receipt, guarded rollback, and hash-verified staged-state reload.
- `OperationsProfileStore.cs` — independent schema-1 Nexus operations working library with atomic replacement, `.previous` recovery, receipt-bound rollback, validation, and content-ID/default-profile resolution.
- `OperationsExecutionPolicy.cs` — stable safe-maintenance queue policy plus the preserved expansion-grouped striking-dummy catalog and exact map/world coordinate conversion.
- `NexusCommandPolicy.cs` — pure command-name normalization, contract/payload validation, character scoping, and fail-closed rejection of unsupported fork-only controls.
- `AutoDutyProviderIdentityPolicy.cs` — pure shared-internal-name assessment that accepts exactly one loaded stock or Vieri compatibility provider and fails closed when multiple `AutoDuty` implementations are loaded.
- `QuestionableProviderIdentityPolicy.cs` — pure VieriCodex/stock Questionable handoff assessment that rejects a both-loaded state before either distinct IPC contract can be invoked.

### `src/VieriNexus.Contracts`

- `PublicContracts.cs` — contract version 1, global status/dependency names, registered command and rich operations-status endpoints, six read-only `VieriNexus.Navigation.V1.*` endpoints including exact gear-vendor override resolution, immutable DTOs, and stable navigation JSON serialization.

### `src/VieriNexus.Plugin`

- `Plugin.cs` — Dalamud entry point/composition root, command registration, draw lifecycle, setup/open behavior, and disposal.
- `Configuration.cs` — schema 9 global presentation/setup/operations-overlay settings, imported-operations preference receipt, character-scoped safety, Progression draft and complete multi-job queue state, one-time verified queue promotion, per-source migration state, and independent embedded-module enablement.
- `VieriNexus.Plugin.csproj` — `Dalamud.NET.Sdk/15.0.0`, version `0.1.0.62`, assembly/internal root `VieriNexus`; builds and packages the five isolated custom runtime archives.
- `VieriNexus.json` — Dalamud API level 15 manifest, author `Valentina Vieri`, permanent internal name `VieriNexus`.
- `Assets/VieriNexusLogo.png` — permanent Home hero artwork.
- `Services/BuiltInModuleCatalog.cs` — nine neutral module registrations and capability identifiers.
- `Services/DependencyService.cs` — installed-plugin detection, complete same-internal-name inventory for provider identity checks, and focused Dalamud Plugin Installer actions.
- `Services/GameplayReadyGate.cs` — post-login/zone stable-world gate.
- `Services/LegacyConfigurationInventory.cs` — read-only path discovery for nine predecessor sources; VieriLink is marked protected.
- `Services/AutoDutyMigrationService.cs` — VieriAutoDuty profile discovery, cached full-operations preview, transactional staging/rollback, saved-receipt recovery, and automatic promotion into an independent verified Nexus working profile library.
- `Services/CommandCenterMigrationService.cs` plus the Application importer/store — complete VieriDeck settings discovery, null-safe preview, transactional staging/receipt/rollback, immutable source backup, and a separately editable atomic Nexus working copy; clean installations can initialize without a predecessor.
- `Services/CommandCenterCatalogService.cs` and `CommandCenterPluginUiBridge.cs` — installed/disabled plugin and registered-command discovery, documented subcommand/Lifestream shortcut enrichment, settings/main-window opening, preferred quick commands for command-only plugins, and guarded close-only inspection that excludes overlays/progress/HUD/status windows.
- `Services/NavigationMigrationService.cs` — source location, cached preview, import/rollback orchestration, Nexus storage paths, saved-receipt recovery across plugin reloads, and in-memory access to the verified staged snapshot.
- `Services/NavigationActivationService.cs` — composition of working-library readiness, installed/loaded source ownership, dependency readiness, Navigation/Movement lease state, verified Stop, explicit-stop compatibility readiness, reload/watchdog readiness, and automatic session authority into the pure activation policy.
- `Services/VnavmeshNavigationStopProvider.cs` — provider adapter that requests `vnavmesh.Path.Stop` and independently observes `vnavmesh.Path.IsRunning` for verified completion.
- `Services/GameManualMovementInputSource.cs` — retained source file for historical compatibility; production route composition no longer instantiates it.
- `Services/ManualMovementSafetyService.cs` — explicit-stop-only compatibility policy. It observes no input, never blocks a route start, and never cancels active movement.
- `Services/NavigationDiagnosticsService.cs` — live mapping of navigation providers, predecessor/authority state, resource ownership, and safety coordinators into the shared provider-health snapshot and transition audit; owns the isolated simulator result.
- `Services/NavigationRecoveryService.cs` — character-aware composition of the no-replay checkpoint with the retained explicit-stop compatibility seam; no manual-input quiet period gates route restart.
- `Services/NavigationLibraryService.cs` — promotes verified staging into a separate working file and provides atomic route creation/edit/save/delete operations without touching the predecessor or receipt.
- `Services/NavigationRoutePreviewService.cs` — persistent current-territory world drawing for explicitly selected Nexus route plans.
- `Services/NavigationLivePathService.cs` — generated vnavmesh waypoint overlay gated to current-process Nexus local or delegated suite ownership.
- `Services/NexusRouteTravelProvider.cs` — Nexus-owned complete route-trip state machine. It calls stock Lifestream only for teleport, Aethernet, and exact Grand Company inn shortcuts, waits through transitions/readiness, then calls vnavmesh for the authored path; it owns route status, visualization authorization, timeout/failure reporting, and Stop. It has no AutoDuty route dependency.
- `Services/NexusMaintenanceRuntimeService.cs` — Nexus-owned bounded self-repair, materia extraction, collectible registration, eligible-coffer opening, protected selling/desynthesis, Grand Company turn-ins, and collection storage. It owns current-character policy, exact item protection/approval, destination selection, Lifestream/vnavmesh travel, storage furnishing interaction, exclusive leases, provider-before/after verification, gearset restoration, timeouts, and unified Stop. AutoRetainer and Glamour Log are narrow final-mechanics providers; no AutoDuty maintenance IPC is invoked.
- `Services/StrikingDummyTravelService.cs` — preserved expansion-grouped training destinations, compatible unlocked Lifestream teleport, map/dummy floor projection, final Nexus/vnavmesh approach, timeout/provider failure handling, and unified Stop without AutoDuty IPC.
- `Services/ProgressionProviderService.cs` — stock Questionable-only quest IPC plus every installed implementation sharing the AutoDuty identity. Nexus enumerates and classifies current game-data Class/Job/Role, MSQ, current, and ordinary general side quests, dispatches one exact quest, observes exact ID/completion, session-blacklists pathless candidates, and exposes bounded Stop. A loaded VieriCodex is a migration-source conflict and is never selected or called. The duty edge retains one exact bounded `Run`, status/path observations, leveling-mode reset, duplicate-identity rejection, and Stop. Gear planning and execution remain Nexus-owned.
- `Services/FastJobSwitchService.cs` — required Fast Job Switcher presence/load checks, localized full job labels, permanent per-job level reads, documented lower-case slash-command dispatch, and exact current-job observation.
- `Services/ProgressionQueueRuntimeService.cs` — character-scoped ordered queue lifecycle, safe job-switch gating and confirmation, settle delay, fresh bounded goal creation/resumption, durable advancement, fallbacks, Stop, and reload recovery without provider instruction replay.
- `Services/ProgressAtlasService.cs` — Nexus-owned current-game-data catalogs and per-character completion reads for the complete Aetheryte/Aethernet network, all field/quest Aether Currents, and non-Legacy achievements; achievement data is requested from the game and never shared between characters.
- `Data/hunting_log_targets.json` plus `Services/ProgressAtlasService.cs` — the complete 12-log/666-target class and Grand Company catalog joined to live `MonsterNoteManager` kill counts; current incomplete targets are available to both the Atlas UI and the provider-neutral execution selector. World exploration builds its region catalog from current plan-map game files and reads per-character discovery state directly.
- `GearShopping.cs`, `Services/NexusGearCatalogService.cs`, `Services/NexusGearExecutionService.cs`, and `Services/GearShoppingRuntimeService.cs` — Nexus-owned live equipment/owned-item scan, curated vendor-band/job-family selection, ordinary gil-shop catalog traversal, job-aware per-slot candidate ranking, automatic/manual exact approval, vendor travel, shop interaction, purchase confirmation, exact equipping, gearset update, displaced-item cleanup, and full-run coordination. Exact selected slots, vendor identity, item IDs, quantities, maximum unit prices, equipment signature, character, and gil floor form one single-use approval; Teleport/Navigation/Movement/UI/Inventory leases remain held through verified completion or confirmed Stop.
- `Services/NavigationRouteRuntimeService.cs` — plugin-facing planning, static/live preview, consistent same/cross-zone suite dispatch with guarded local fallback, immediate Stop/restart, and per-frame runtime composition.
- `Services/NavigationRouteRecordingService.cs` — live position observation and atomic capture persistence around the provider-neutral recording coordinator.
- `Services/NexusControlService.cs` — one character-scoped, request-idempotent control and live-telemetry boundary shared by overlay, chat, and IPC; its global Stop covers every Nexus runtime and its command list excludes unsafe provider-private approximations.
- `Services/NexusIpcProvider.cs` — registered global status, operations telemetry, command, dependency, and Nexus-namespaced navigation IPC.
- `Services/EmbeddedModuleManager.cs`, `EmbeddedModuleLoadContext.cs`, and `EmbeddedModuleProxies.cs` — one-predecessor-at-a-time settings preparation, timestamped backup, isolated runtime extraction/loading, module-scoped configuration/UI callbacks, protected VieriLink token validation, independent failure containment, and automatic takeover after the standalone predecessor is disabled.
- `Services/WorldSnapshotObserver.cs` — throttled Dalamud client/player/object/condition observation plus the current read-only provider-health snapshot.
- `UI/NexusWindow.cs` — entire current shell and pages, including the one-scrollbar Plugins launcher with top-level Dalamud actions, favorites-first grouping, remaining-plugin list, inline command expansion, preferred/custom actions, hidden restoration, filters, and behavior/hotkey controls.
- `UI/NexusOperationsOverlay.cs` — optional compact Goto/Gear/Inventory/Duty/Extras surface with the approved predecessor shortcuts, Nexus/Lifestream travel, current operation status, and unified Stop. Historical paused-duty detail is deliberately excluded while the overlay is idle.
- `UI/NexusTheme.cs` — dark/red/gold ImGui theme and shared status/section helpers.

### `tests/VieriNexus.Application.Tests`

There are 324 automated Nexus tests across the application/domain policies, including native queue import/normalization/order/method behavior, MSQ planning/execution priority, null-safe complete VieriCodex settings import, transactional recovery/rollback, command aliases including `job.switch`, public-version alignment, payload bounds, character scoping, rejected unsafe controls, provider identity handoff coverage, and Plugins-page grouping/command behavior. The five packaged engines retain their own focused regression suites, and the release package has a separate nested-archive safety/completeness verifier.

- `DependencyCatalogTests.cs`
- `NavigationRouteMigrationImporterTests.cs`
- `NavigationStopCoordinatorTests.cs`
- `ManualMovementSafetyCoordinatorTests.cs`
- `NavigationActivationPolicyTests.cs`
- `NavigationAuthorityCoordinatorTests.cs`
- `NavigationContractJsonTests.cs`
- `NavigationDiagnosticsMonitorTests.cs`
- `NavigationExecutionIntentStoreTests.cs`
- `NavigationExecutionSafetyCoordinatorTests.cs`
- `NavigationLibraryQueryTests.cs`
- `NavigationLibraryStoreTests.cs`
- `NavigationRecoveryCoordinatorTests.cs`
- `NavigationRouteExecutionCoordinatorTests.cs`
- `NavigationRouteClipboardCodecTests.cs`
- `NavigationRoutePlannerTests.cs`
- `NavigationRouteDispatchPolicyTests.cs`
- `NavigationBuiltInRouteCatalogTests.cs`
- `NavigationRouteOverrideResolverTests.cs`
- `NavigationRouteRecordingCoordinatorTests.cs`
- `NavigationRouteTargetBindingTests.cs`
- `NavigationSuiteRouteRequestTests.cs`
- `NavigationSuiteTravelCoordinatorTests.cs`
- `ProgressionProviderPolicyTests.cs`
- `QuestionableProviderIdentityPolicyTests.cs`
- `ReachJobLevelPlannerTests.cs`
- `NavigationSafetySimulatorTests.cs`
- `ResourceLeaseManagerTests.cs`
- `SoloDutyCombatPolicyTests.cs`
- `TransactionalMigrationStoreTests.cs`
- `OperationsProfileStoreTests.cs`
- `OperationsExecutionPolicyTests.cs`
- `WorldStateStoreTests.cs`
- `ClassJobDisplayTests.cs`
- `ProgressAtlasModelTests.cs`
- `ProgressAtlasActionCatalogTests.cs`
- `HuntingLogCandidatePolicyTests.cs`

The 0.1.0.62 release candidate passes all 325 Nexus tests, 12 DelvUI tests, 21 VieriLink tests, and the five-archive package verifier. The complete Nexus solution compiles with zero warnings/errors.

## 4. Major Systems and Features

### 4.1 Dalamud lifecycle and commands — IMPLEMENTED

`src/VieriNexus.Plugin/Plugin.cs` registers:

- `/vierinexus`
- `/nexus`
- Page subcommands: `home`, `splash` (alias of Home), `show`, `hide`, `dependencies`, `migration`, `routes`, `progression`, `queue`, `atlas`, and `plugins` plus `commands`/`commandcenter`/`deck` aliases.
- Shared control subcommands: `status`, `start`, `resume`, `last`, `stop`, `maintenance`, `repair`, `extract`, `register`, `coffers`, `desynth`, `gcturnin`, `storage`, `sell`, `play <route>`, and `preview <route>`.

Dalamud's Open Main UI and Open Config UI callbacks open the appropriate page. Disposal unregisters callbacks, removes both command handlers, unregisters IPC, removes windows, and saves configuration.

Safeguard: no Nexus window is drawn until the gameplay-ready gate passes. On first eligible session, first-run users are taken to Dependencies; returning users optionally open Home when `OpenOnLogin` is enabled.

### 4.2 Dependency setup gate — IMPLEMENTED

`NexusDependencyCatalog.All` defines seven required and fifteen recommended integrations. Fast Job Switcher is required because native multi-job queues depend on its documented slash-command switching contract. `DependencyService.Snapshot()` matches Dalamud `InstalledPlugins` by internal name, prefers a loaded candidate and then the highest available version, and reports only `Missing`, `Disabled`, or `Healthy`; its complete identity inventory separately lets provider adapters detect two loaded plugins claiming the same internal name instead of trusting colliding IPC. `OpenInstaller(...)` opens Dalamud's installer focused on the exact install/manage search.

`NexusWindow.PreDraw()` locks non-setup pages when first-run setup is incomplete or any required provider is not loaded. The Dependencies page shows progress, separates Required and Recommended, and enables **Continue to Vieri Nexus** only when all seven required providers are healthy.

Limitations:

- The richer planned states (`Incompatible`, `Starting`, `Degraded`, `Faulted`), version ranges, repository-configuration checks, IPC health, and per-module dependency gating are not implemented.
- The seven providers are globally required by the foundation gate; Fast Job Switcher is actively used by Job Queue while the other requirements retain their documented module roles.
- Install actions open Dalamud UI; Nexus does not call private installer internals or silently install/enable anything.

### 4.3 Neutral module registry — IMPLEMENTED AS METADATA ONLY

`BuiltInModuleCatalog.Create()` registers nine neutral entries:

| ID | Display name | Capability |
| --- | --- | --- |
| `progression` | Progression | `vieri.capability.progression/v1` |
| `duties` | Duties | `vieri.capability.duty.run/v1` |
| `combat` | Combat | `vieri.capability.combat.control/v1` |
| `gear` | Gear & Inventory | `vieri.capability.gear.readiness/v1` |
| `market` | Market | `vieri.capability.market.reprice/v1` |
| `custom-ui` | Custom UI | `vieri.capability.ui.custom/v1` |
| `communications` | Communications | `vieri.capability.notify.discord/v1` |
| `command-center` | Plugins | `vieri.capability.command.invoke/v1` |
| `navigation` | Routes & Navigation | `vieri.capability.navigation.route/v1` |

The registry prevents duplicate IDs case-insensitively. These are display/registration descriptors only; every current module page says the predecessor remains authoritative.

### 4.4 World observation and readiness — IMPLEMENTED FOUNDATION

`WorldSnapshotObserver.Update(long now)` samples at most every 250 ms and publishes:

- login/area transition/player availability/territory;
- observed character key, name, class/job row ID, level, and combat state;
- current navigation provider/safety health for vnavmesh Stop, explicit route-Stop policy, reload/watchdog state, predecessor ownership, Navigation/Movement ownership, and Nexus authority.

`WorldStateStore` exposes the latest immutable snapshot and a `Changed` event. `Observed<T>` explicitly represents Known/Unknown/Stale/Unavailable in the domain, although the current observer only emits Known or Unknown for the character.

Limitations: inventory, gear, progression, location vectors, targets, detailed user-control signals, non-navigation provider health, duties, and other planned slices are not observed. `SessionSnapshot.IsLoading` and `IsBetweenAreas` currently receive the same between-area condition.

### 4.5 Resource ownership, verified Stop, manual yielding, reload recovery, and same-zone execution — IMPLEMENTED, LIVE TEST PENDING

`ResourceLeaseManager.TryAcquire(...)` expands resource implications, orders them, checks conflicts under one lock, and acquires the complete bundle or nothing. Current implications include:

- Navigation implies Movement.
- Teleport implies Navigation and Movement.
- Combat implies Targeting.
- Rotation implies Combat and Targeting.
- Retainer implies UI Interaction.
- Market implies Retainer, UI Interaction, and Inventory Mutation.

Leases carry a goal/task/attempt owner, priority, reason, acquisition/expiry times, heartbeat, and disposable release. Expirations discovered by an acquire, snapshot, late heartbeat, or explicit sweep are retained exactly once for the active watchdog instead of disappearing silently. An expired lease cannot be revived by a late heartbeat.

`Plugin` instantiates one manager and supplies it to activation assessment and the live route coordinator. `NavigationStopCoordinator` tracks each active route lease and treats Stop as idempotent: no tracked execution produces no provider call. When execution is tracked, provider unavailability, a failed request, unknown activity, or still-active movement retains and heartbeats the lease; only an explicit inactive result releases it. A vnavmesh adapter keeps the Stop request and `Path.IsRunning` confirmation separate. Natural completion performs observation-only release without sending Stop.

The Routes UI and `/nexus play` acquire and track local execution leases through the sole guarded coordinator; `/nexus stop` and the UI Stop action enter verified no-replay recovery. Cross-zone actions use the capability-checked AutoDuty suite-travel adapter and are tracked only when explicitly dispatched by this Nexus process. There is still no automatic priority negotiation or public resume action.

Route cancellation is explicit in production as of 0.1.0.25. Keyboard, mouse, gamepad, jump, and autorun input neither block route starts nor stop tracked route movement. `ManualMovementSafetyService` publishes a permanently ready `explicit-stop-only` compatibility state and has no input observer. The historical `ManualMovementSafetyCoordinator`, input-source file, and tests remain as non-production recovery history; they must not be mistaken for current behavior. The visible Routes Stop button and `/nexus stop` are the user cancellation paths. Provider loss, source conflict, reload reconciliation, lease expiry, and character authorization at the moment of a new start remain guarded.

The route execution and recovery monitors run each plugin draw even when the Nexus window is closed.

`NavigationExecutionSafetyCoordinator` connects durable movement intent, reload/shutdown reconciliation, active lease expiry, normal completion, and superseded-provider yield. Before provider movement, `BeginExecution` atomically saves schema/execution/route/lease identity and state, then registers the lease with verified Stop; it deliberately persists no instruction pointer. Reload Running/StopPending intent can only request Stop, confirm inactivity, and enter `AwaitingExplicitResume`. It cannot replay movement. Missing-provider, rejected-Stop, active/unknown movement, corrupt-journal, and unwritable-journal outcomes remain fail-closed. `Superseded` records independently verified replacement by another provider without calling global Stop.

The safety coordinator sweeps lease expiry every plugin draw, including while the UI is closed. A missed tracked Navigation/Movement heartbeat persists StopPending, invokes the same verified Stop coordinator, and stays blocked until inactive confirmation. Plugin disposal also arms StopPending before requesting Stop. Routes now use this live execution boundary. The bounded Progression duty lane has its own durable goal/task store and reconciliation because it owns a broader resource bundle and provider lifecycle.

### 4.6 Goal/task/failure contracts and first bounded Progression duty executor — IMPLEMENTED

`NexusGoal` is a versioned, character-scoped desired-state record with constraints, priority, lifecycle, plan revision, and status detail. `NexusTask` is a bounded unit with capability, selected provider, required resources, lifecycle, payload, and structured failure. Stable string-backed kinds/capabilities/providers avoid a global enum that every module must edit.

`ReachJobLevelPlanner` is the first concrete desired-state planner. It validates current character/job/level, target level, a hard non-negative gil reserve, at least one allowed leveling method, and compatible provider availability. It can propose Nexus-owned gear readiness, one exact Class/Job/Role or ordinary general side quest, one exact open-world or duty-only Hunting Log target, exactly one bounded duty, and post-activity level verification/replanning. An unavailable optional lane is a warning when another allowed lane is ready; no ready provider or an ambiguous double-provider state blocks the draft. Gear readiness, both exact quest families, both Hunting Log execution paths, and bounded duties are connected to execution.

The current character's draft persists in Nexus configuration. `ProgressionExecutionCoordinator` and `FileProgressionGoalStore` durably own one character-scoped Reach Job Level desired state, plan revision, bounded task history, active task, Stop-after flag, provider-start checkpoint, duty-entry checkpoint, and matching game duty-completion checkpoint. Nexus first creates a separate native gear-readiness task, owns its travel/UI/inventory resources, enforces the gil policy, and verifies live item level/gil. It then prefers one exact eligible Class/Job/Role quest, followed by one exact ordinary general side quest, pins the quest ID/kind, and delegates `StartSingleQuest` through the selected Questionable-compatible contract; a false start marks that provider/quest path unsupported for the session and automatically selects the next candidate, capped at 24 fallbacks. Next it owns one exact eligible Hunting Log target. Open-world targets use stock Lifestream, vnavmesh, and Boss Mod mechanics; duty-only Grand Company targets resolve the current-game-data duty, require its unlocked AutoDuty path, acquire DutyQueue/UI/combat ownership, suppress AutoDuty leveling, run exactly once, retain Stop ownership until inactivity, and require the exact live log count before replanning. When no supported quest or hunt target is available Nexus selects one eligible unlocked leveling duty through the same bounded AutoDuty contract. Each operation returns to Nexus for verification and replanning. Stop, Stop-after, provider loss/mismatch, job change, reload, and unload are reconciled through the active task's provider without replaying stale work. There is still no general cross-module goal repository/task graph executor.

### 4.7 Solo-duty combat handoff policy — IMPLEMENTED AS POLICY/TESTS ONLY

`SoloDutyCombatPolicy` preserves a hard-won VieriCodex incident fix:

- every solo-duty entry requires a fresh rotation-automation handoff;
- selected-target behavior remains primary;
- nearest-hostile fallback starts only after 2 seconds without a usable hostile and only if retry is allowed;
- fallback yields immediately when normal targeting recovers or after a 1.5-second assist pulse;
- retry delay is 2 seconds;
- hard-target mutation is forbidden;
- encounter provider remains the movement owner.

Nexus does not execute this policy yet. It is a migration/regression contract for later Progression/Combat integration.

### 4.8 Legacy configuration discovery — IMPLEMENTED

`LegacyConfigurationInventory.Scan()` checks the parent of Nexus's Dalamud config directory for known file/directory names belonging to AutoDuty, AutoMarket, Avarice, Codex, Deck, DelvUI, Link, RotationHelper, and NavPlotter. It reports existence and matching paths without changing them.

VieriLink is marked `ContainsProtectedValues`. Current Communications discovery is existence-only: generic discovery/UI must not open, deserialize, decrypt, log, export, copy, or rewrite its token, channel IDs, status message ID, or command cursor.

### 4.9 Routes & Navigation transactional migration and working module — IMPLEMENTED

This is the first real migration slice.

`NavigationRouteMigrationImporter.Preview(string json)` maps VieriNavPlotter configuration fields into `NavigationLibrarySnapshot`, including:

- source configuration version;
- recording interval and minimum point distance;
- world preview, point-number, and live-navigation-path visibility;
- library-pane width and selected route ID;
- stable route ID, name, territory, ordered XYZ points, notes, tags;
- mesh/flight flags, ordinary/final tolerances;
- binding kind, target data ID/label;
- explicit `OverrideEnabled` state and update timestamp.

It reports invalid JSON, empty IDs, duplicate stable IDs, non-finite tolerances/coordinates, and warnings for empty names, territory, points, or missing selected routes. Zero personal routes is valid and imports display/recording settings; built-in routes do not live in the personal config.

`NavigationMigrationService` locates `VieriNavPlotter.json`, caches preview by path/write time, and writes Nexus-owned state under the Nexus configuration directory:

```text
NexusData/routes.v1.json
NexusData/backups/navplotter/<timestamp-guid>/legacy-source.json
NexusData/backups/navplotter/<timestamp-guid>/previous-nexus-state.json (when applicable)
NexusData/receipts/<receipt-id>.json
```

`TransactionalMigrationStore.Apply(...)` copies the legacy source first, optionally backs up prior Nexus state, atomically writes staged JSON through a same-directory temporary file, records source/target SHA-256 hashes, and writes a receipt. Failure restores the prior Nexus target where possible. `Rollback(...)` refuses to overwrite a staged route library whose hash changed after import, then restores the prior Nexus state or removes the first imported target. It never rewrites or deletes the predecessor configuration.

The Migration page has **Create backup and import to staging** and, after a successful import, **Rollback staged import**. `LegacyImportState` records review/import/version/time/receipt/count/readiness while leaving `Activated = false`.

The Migration card sizes both actions from their rendered labels, keeps them on one row only when they fit, wraps long preview/operation/safety text, and scales its panel height with the configured UI scale. Successful zero-route imports explicitly say that settings and zero personal routes were imported, avoiding the false impression that no migration work occurred.

The Routes page preserves verified staging as immutable evidence and maintains the separate Nexus working library in `NexusData/navigation-library.v1.json`; each atomic replacement retains `.previous`. Its normal view now leads with Search/Create and Play/Travel/Show. Recording and Add Position are grouped with collapsed recording options. Route metadata/automation, display preferences, vendor templates, import/export/destructive tools, and diagnostics are categorized behind collapsed sections. The list/editor use one outer scrollbar. The full editing, recording, point management, vendor assignment, preview, confirmation, and safe clipboard behaviors remain available without dominating the normal playback workflow.

`NavigationActivationPolicy` is a pure fail-closed assessment covering the working library, loaded source ownership, dependencies, Navigation/Movement lease conflicts, verified Stop, explicit-stop policy, reload/watchdog readiness, and current Nexus execution state. Separate approval is no longer a user gate: Nexus becomes ready automatically when VieriNavPlotter is not loaded and all safety prerequisites pass.

`NavigationAuthorityCoordinator` automatically owns the route boundary only while VieriNavPlotter is not loaded and prerequisites are healthy; it never toggles the source. Source reappearance, provider/safety loss, character authorization loss, or an outside conflict revokes authority. Every movement start still atomically acquires Navigation/Movement and journals intent before provider dispatch. An explicit user Stop confirms inactivity, releases ownership, and marks the intent complete immediately. Reload/watchdog failures remain no-replay events; the next Play/Travel click is an explicit new request after safety reconciliation.

The shared-provider ownership rule is non-negotiable: while vnavmesh reports movement, Nexus compares the active waypoint-chain destination to its planned final point. If a different plugin replaces the path, Nexus writes the intent as `Superseded`, releases only its internal lease, and does not call global Stop. This specifically prevents Nexus from interrupting VieriCodex, AutoDuty, or another legitimate vnavmesh owner after they take over. Natural Nexus completion also uses observation-only release and sends no redundant global Stop.

`NavigationDiagnosticsService` adds read-only Provider Health and a bounded Safety Audit under the Routes page's collapsed Troubleshooting section. It observes vnavmesh Stop availability/version, explicit route-Stop policy, reload/watchdog status, VieriNavPlotter ownership/version, outside or tracked Navigation/Movement ownership, and Nexus authority. The same six observations populate `WorldSnapshot.Providers`. Only state/code transitions enter the session audit; it is not durable general task history and contains no credentials. `NavigationSafetySimulator` remains a historical isolated coordinator regression harness; its manual-takeover scenario is not the current production route policy.

`NavigationRecoveryCoordinator` remains the internal no-replay gate. Normal user Stop completes immediately. A new Play/Travel request may begin after ownership is released and any reload/watchdog stop is reconciled; there is no manual-input quiet-period or acknowledgement workflow.

Critical limitation: migration staging remains read-only; the user must create the separate working copy once. After that, VieriNavPlotter loaded means it remains authoritative, while unloading it makes Nexus routes ready automatically. Built-in templates, target capture, override resolution, filtered waypoints, one-click same/cross-zone suite travel, and local fallback are implemented. Nexus never disables VieriNavPlotter or activates duplicate navigation.

### 4.10 Public IPC — IMPLEMENTED FOR CURRENT RUNTIMES

Runtime registration in `NexusIpcProvider` currently provides:

- `VieriNexus.Status.V1.Get` -> `NexusStatusDto`
- `VieriNexus.Commands.V1.Execute` -> `NexusCommandDto` to `NexusCommandResultDto`
- `VieriNexus.Operations.V1.GetStatus` -> `NexusOperationsStatusDto`
- `VieriNexus.Dependencies.V1.Get` -> `DependencyDto[]`
- `VieriNexus.Navigation.V1.GetApiVersion` -> `int`
- `VieriNexus.Navigation.V1.GetStatus` -> `NavigationLibraryStatusDto`
- `VieriNexus.Navigation.V1.ListRoutes` -> compatibility-shaped route-summary JSON
- `VieriNexus.Navigation.V1.GetRoute` -> compatibility-shaped full-route JSON or `null`
- `VieriNexus.Navigation.V1.ResolveGearVendorOverride` -> read-only exact territory/vendor resolution JSON; absent, invalid, and ambiguous assignments fail closed
- `VieriNexus.Navigation.V1.GetActivationStatus` -> `NavigationActivationStatusDto`

The compatibility status reports readiness from required dependencies and the current player snapshot. The richer operations status independently reports character/content ID, localized job label, level/item level/gil, territory/location, combat/duty/queue state, bag usage, equipped durability, active Nexus module/activity/detail/provider, Stop-after state, completed bounded work counts, and last completed activity without calling VieriAutoDuty telemetry.

Navigation status reports whether execution is actively running and reports source authority only when VieriNavPlotter is actually loaded. List/detail calls expose the Nexus working library when present, otherwise the verified staged snapshot. Their JSON property shape mirrors the predecessor's read-only route list/detail payload closely enough for consumers to adapt without taking the unversioned `VieriNavPlotter.*` names while both plugins coexist. `NavigationContractJsonTests` lock the empty-list, full-route, and resolution-envelope shapes, including ordered points and a disabled override. Activation status exposes source install/load state and every policy blocker. Nexus registers only its own versioned exact-target resolver, not predecessor IPC aliases, preventing name collisions during coexistence.

The command gateway is shared by IPC, `/nexus`, and the custom overlay. Requests require contract version 1 and a non-empty ID, cap payloads at 8 KiB, reject character mismatches, and cache the last 128 request results so an exact retry cannot repeat a mutation. It exposes status, global Stop, configured Progression Start/Resume/Last Run/Stop, working native maintenance actions, protected-selling review, saved-route Play/Preview, and a fixed page-opening allowlist. Provider-private leave, pause, endless-loop, and arbitrary command execution are not exposed or approximated. Legacy Wrath/Switch/AutoDuty/Codex/NavPlotter aliases, general activation, and broad provider mutation remain absent by design during coexistence.

### 4.11 Release and upstream provenance — IMPLEMENTED AS DOCUMENTED/PINNED PROCESS

`upstreams/source-lock.json` records exact source revisions for all nine migration sources. At recovery, every sibling repository existed locally, was clean, and its `HEAD` exactly matched the Nexus pin:

| Source | Nexus-pinned/current commit |
| --- | --- |
| VieriAutoDuty | `e1318fcf3cc950cf928a5a95058cb806a27f76a4` |
| VieriAutoMarket | `e08a70f7a9fece486962843cbe89ea9e2b969871` |
| VieriAvarice | `d9f17fd1aa8c15f69608797ff95573ef01f16b3b` |
| VieriCodex | `1bc749b1e6f04519608b04caaca21c882e28412f` |
| VieriDeck | `d8a77d1c693baacf57306cc92d6601ccc4d70595` |
| VieriDelvUI | `1062765e33fd9d1e1fb386adb4043cd4cb4f9b0a` |
| VieriLink | `b25b9c8b263c1907e89ae00273193b9526ffc062` |
| VieriRotationHelper | `bfe27a9793633372617e440445d4c7ed7afd3502` |
| VieriNavPlotter | `010017861e152e94ebf3796fa27de72ba5db47cb` |

Recovered release evidence states Nexus `0.1.0.3` was committed/pushed, published to the shared Dalamud feed/website, verified as valid runtime and source archives with matching hashes, and announced through Discord. That publication was not independently re-run during this documentation-only recovery.

## 5. External Dependencies and Integrations

### Build/runtime platform

- Windows and `.NET 10` (`net10.0-windows`).
- Dalamud API level 15 through `Dalamud.NET.Sdk/15.0.0` and `DalamudPackager 15.0.0`.
- Dalamud APIs currently used directly: `IDalamudPlugin`, service injection, `IDalamudPluginInterface`, `ICommandManager`, `IClientState`, `IPlayerState`, `IObjectTable`, `ICondition`, `ITextureProvider`, `IChatGui`, `IPluginLog`, Windowing, ImGui bindings, installed-plugin inventory, Plugin Installer navigation, and call gates.
- BCL APIs for JSON, SHA-256, file operations, immutable records, synchronization, and collections.
- Tests use `Microsoft.NET.Test.Sdk 17.13.0`, `xunit 2.9.3`, and `xunit.runner.visualstudio 3.1.3`.

There is no SQLite package yet, despite SQLite being the planned durable goal/task/history store.

### Required external providers

These currently gate first-run completion but are not yet called for gameplay:

| Provider/internal name | Intended purpose | If unavailable now |
| --- | --- | --- |
| Boss Mod / `BossMod` | encounter mechanics, supported solo duties, duty movement/coordination | setup remains locked |
| vnavmesh / `vnavmesh` | in-zone mesh building and movement | setup remains locked |
| Lifestream / `Lifestream` | Aetheryte, Aethernet, world, and local travel | setup remains locked |
| TextAdvance / `TextAdvance` | quest dialogue, acceptance, turn-in, cutscenes | setup remains locked |
| Marketbuddy / `Marketbuddy` | applying retainer listing-price changes | setup remains locked |
| Allagan Market / `AllaganMarket` | ownership, price state, undercut intelligence | setup remains locked |
| Fast Job Switcher / `FastJobSwitcher` | verified unattended class/job changes for the Nexus Job Queue | setup remains locked |

Nexus currently detects installation/load state only. It has no health handshake or gameplay IPC with these providers.

### Recommended integrations

These do not block setup: AutoRetainer, Glamour Log, Anti-AFK (`AntiAfkKick-Dalamud`), Pandora's Box (`PandorasBox`), Gearsetter, Stylist, CBT (`Automaton`), Artisan, AutoHook, Mogmail, NotificationMaster, SelectString, QuestMap, YesAlready, and Skippy. Their intended consumers/capabilities are recorded in `docs/DEPENDENCY_AUDIT.md` and `DependencyCatalog.cs`.

### Intentionally not global dependencies

- Questionable: stock Questionable is the sole module-scoped ordinary quest-execution provider behind a capability/version adapter. VieriCodex is retained only as a local settings-migration source and is never selected or called for runtime work.
- AutoDuty: VieriAutoDuty remains authoritative during migration; after its Vieri-specific route/travel, gear/inventory, maintenance, progression-loop, telemetry, command, and UI layers move into Nexus, stock AutoDuty becomes the module-scoped replaceable supported-duty provider. Its current stock IPC supports bounded duty run/stop/status basics but not the fork-only route, gear-readiness, Last Run, or rich telemetry contracts. The user explicitly requires Nexus to preserve the custom overlay's cleaner categorized Goto/Gear/Inventory/Extras experience, striking-dummy destinations, manual shopping review, and useful duty controls/status; Nexus must not fall back to stock AutoDuty's overlay when the fork retires.
- The nine Vieri migration-source forks: temporary coexistence/migration inputs, not third-party dependencies in the final topology.
- Wrath Combo and VieriWrathSwitch: already incorporated into VieriRotationHelper's combat suite.
- Rotation Solver Reborn and BossMod AutoRotation: alternative engines, not Nexus combat requirements.
- VieriHildaLayer: obsolete/excluded.
- Hilda: used historically as an authorized visual/forecasting reference for VieriRotationHelper, but not a runtime dependency.

### Slash commands and IPC

Current slash commands are documented in section 4.1. Current IPC is documented in section 4.10. The long-term compatibility requirement is to keep necessary `WrathCombo.*`, `WrathSwitch.*`, `AutoDuty.*`, `VieriCodex.*`, Avarice, and positional-guidance aliases routed to the same Nexus services while real outside consumers transition. Compatibility names must not become Nexus module/page branding.

## 6. UI / UX Architecture

### Current window

`NexusWindow` is a single resizable Dalamud window titled `Vieri Nexus###VieriNexusMain`, initially 1220x760 and constrained to 940x580–2200x1400. It has a left navigation child and a content child. Compact navigation changes the sidebar from 205 pixels to 72 pixels and shows first-letter buttons with tooltips.

Navigation is grouped as:

- Overview: Home, Control Center, Automation, Progression, Progress Atlas, Queue
- Modules: Combat, Routes, Market, Custom UI, Communications
- Setup: Dependencies, Migration, Settings

Home, Control Center, Progression, Progress Atlas, Routes, Dependencies, Migration, and Settings have specialized current implementations. Other destinations render an honest staged-module placeholder explaining that no live behavior has moved.

### Current pages/workflows

- **Home** — permanent large logo hero plus current Routes, Progression, and provider-boundary status cards. This replaced the rejected standalone splash popup.
- **Control Center** — current route/progression state, dependency/character cards, neutral module grid, and explicit safety state. It is informational only.
- **Progression** — character-scoped current-job target level, Class/Job/Role-quest/Hunting Log/general-side-quest/duty allowances, hard gil reserve, stock/transition provider-contract status, conflict detection, a bounded plan preview, and durable Start/Stop-after/Stop/resume controls for exactly one gear, exact quest, open-world/duty-only Hunting Log target, or leveling duty activity at a time.
- **Progress Atlas** — Nexus-owned per-character completion for Aetherytes/Aethernet, all open-world and quest-earned Aether Currents, Mapping/Remapping exploration regions, current non-Legacy achievements, and all 12 Hunting/Grand Company Logs with 666 exact targets and live kill counts. One-click bounded actions select the next reachable locked travel node, field current, currently unlockable quest current, or unexplored world region and verify the exact live completion flag. Quest-current discovery comes from current game data and uses the narrow Questionable-compatible contract for exact execution; no VieriCodex planner or Atlas UI is called.
- **Hunting Log execution** — Nexus chooses one exact current-rank target. Open-world work travels through stock Lifestream/vnavmesh, searches known camps, approaches by job range, targets the exact monster, runs a Nexus Boss Mod preset, verifies every live kill credit, and rotates alternate camps. Duty-only Grand Company work resolves the exact unlocked dungeon and stock AutoDuty path, runs one bounded duty with internal leveling disabled, and accepts success only after AutoDuty is inactive and the exact log count is complete. Nexus owns the appropriate leases, Stop reconciliation, reload/provider-loss recovery, and prior Boss Mod state restoration.
- **Dependencies** — required/recommended catalog, health, version, purpose, installer/manage buttons, and first-run Continue gate.
- **Migration** — read-only predecessor discovery, the live Routes & Navigation preview/import/rollback card, and credential-safety notice.
- **Routes** — compact saved-route search/create, one-click Play/Travel/Show, grouped recording and point editing, and collapsed automation, display, vendor, advanced-action, and troubleshooting sections under one page scrollbar.
- **Settings** — UI scale (0.8–1.5), open Home after login, compact navigation, per-character automation authorization, and manual-target settings. The removed movement-to-stop route toggle must not return.
- **Remaining staged module pages** — status/explanation only; no controls are connected to gameplay.

### Visual conventions

`NexusTheme` establishes rounded panels/frames, fine borders, near-black window and raised-panel backgrounds, muted gray secondary text, red selection/action accents, gold headings, green healthy, amber attention/staged, cyan informational, and red error/missing. The three recovered inspiration images show dashboard/queue/control-center layouts with clear status panels, left navigation, progress, toggles, event feeds, connected services, and explicit start/pause/stop controls. They are inspiration, not pixel-exact specifications.

### UX requirements not to lose

- Stunning, smooth, clean, intuitive, user-friendly, and expandable—not “any old plugin.”
- Home owns the logo; no random splash window and no cluttered top identity strip.
- Plain-language status and reasons, especially during automation, recovery, and dependency failure.
- No uncontrolled permanent top-level tab for every future module; modules contribute contextual panels/actions to a scalable shell.
- In-world overlays remain lightweight separate windows and later share a central visibility/occlusion service.
- All Nexus overlays must remain hidden through login/loading and should respect configured cutscene/screenshot/native-window occlusion.

## 7. Configuration and Persistence

### Current configuration — IMPLEMENTED

`Configuration` implements Dalamud `IPluginConfiguration`, currently schema `Version = 9`:

- Global: `FirstRunComplete`, `OpenOnLogin`, `CompactNavigation`, `UiScale`, `SelectedPage`, and the compact operations overlay's visibility/lock/transparency/status choices.
- Per-character dictionary keyed by `CharacterKey.ToString()` (`content ID + home world`): profile name, allow automation, pause-on-manual-target, retained legacy pause-on-manual-movement/quiet-period fields, and a Progression draft containing target level, allowed job-quest/Hunting Log/side-quest/duty methods, and hard minimum-gil reserve. Legacy route fields remain readable for configuration compatibility but are no longer displayed or used by production route control.
- Per-source `LegacyImports`: reviewed/imported flags, source version, import time, receipt ID, imported count, ready-for-activation, activated.

`Initialize(...)` clamps UI scale, restores dictionary comparers/null safety, supplies missing per-character Progression drafts, sets schema version 4, and attaches the plugin interface. There is no destructive older-version migration; existing character and route settings remain intact.

Nexus configuration is saved through Dalamud. NavPlotter and VieriAutoDuty operations staging are separate JSON under `NexusData`; backups and receipts live beside them under the Nexus config directory.

### Persistence rules — RECOVERED/PLANNED

- Existing predecessor configuration remains untouched until explicit, validated migration.
- Each importer maps every setting, intentionally retires it with a reason, or supplies a documented compatibility default.
- Back up before import, validate in staging, commit atomically, record source version/hash/receipt, support rollback, and leave the predecessor authoritative on failure.
- Character identity is content ID plus world, never character name. Global, character, and named-profile scopes remain distinct.
- Planned precedence: code defaults -> global/account -> character -> named profile -> goal constraints/preferences.
- Planned JSON owns human-scale settings/profiles/layout/dependency preferences; SQLite owns durable goals, plan revisions, tasks, attempts, checkpoints, audit history, and provider observations.
- Persist desired intent and verified safe checkpoints, never game pointers, addon indexes, access tokens in task payloads, or unsafe transient instruction pointers.
- On restart, previously Running/Verifying/Retrying work becomes NeedsReconciliation and is checked against actual game state before any retry.

Current limitations: SQLite, schema migrators beyond initialization normalization, named-profile resolution, and general goals/tasks/history/audit persistence are not implemented. The navigation-only JSON execution-intent journal is a minimal no-replay safety primitive, not the planned durable task database.

## 8. Automation / Execution Architecture

### Current runtime reality

Routes & Navigation is the first live Nexus automation module. It owns its working route library, authoring, preview, explicit Play/Travel/Stop, Navigation/Movement leases, route intent, complete-trip state, Lifestream transfer/inn handoff, and authored vnavmesh playback. It no longer uses VieriAutoDuty route IPC. Progression owns a durable Reach Job Level goal, one verified gear-readiness task, and exactly one bounded AutoDuty duty run at a time. General inventory maintenance, combat, quest execution, retainers, market work, Discord work, HUD replacement, and remote commands remain with standalone products/providers.

The current code also provides reusable contracts/primitives for desired-state goals/tasks/failures/resources, atomic in-memory leases, basic world snapshots, preserved solo-duty combat policy, and transactional migration.

### Planned orchestration model

1. A durable goal describes desired state and hard constraints/preferences.
2. A goal handler decomposes unmet state into requirements.
3. strategy providers propose eligible ways to satisfy each requirement.
4. The planner filters by hard constraints and current capability/provider health, scores deterministically, and builds only the near-term executable frontier.
5. A task evaluates readiness/satisfaction/unsupported/unknown, prepares an immutable request, atomically acquires all resources, executes through a provider, observes heartbeat/progress, verifies the postcondition, and persists only a safe checkpoint.
6. Reconciliation runs after startup, login/logout, character/zone/duty transitions, dependency changes, manual intervention, state changes, completion, timeout, failure, or lost heartbeat.
7. Retrying creates a new immutable attempt under the same logical task. Attempt count and elapsed time are bounded.

Failure categories already defined in code are `TransientExternal`, `RateLimited`, `PreconditionChanged`, `DependencyUnavailable`, `ResourceConflict`, `UserIntervention`, `UnsafeState`, `Unsupported`, `PermanentData`, and `Cancelled`.

### Sequencing, recovery, and safety requirements

- All authoritative state transitions occur on one sequenced Dalamud framework-thread dispatcher. Background network/persistence/pure computation must marshal results back.
- Providers can advertise capability/health but may not act without a lease.
- Bundles are acquired atomically in canonical order; no partial ownership.
- Higher-priority work requests a safe checkpoint rather than force-preempting unsafe teleport/UI/purchase/duty work.
- Missing heartbeats expire via watchdog and trigger reconciliation.
- Unknown/stale state never counts as success.
- Unsafe operations—market confirmation, purchase, discard/sell, duty acceptance, retainer interaction—do not blindly resume after uncertain state.
- Route movement stops only through the explicit Stop control; ordinary movement input does not cancel it. Other future modules must define equally explicit user-control semantics: manual target changes suspend automated targeting, explicit F1/manual rotation control wins, and interacting with a critical native window pauses its owner.
- Resume re-observes and replans; it does not continue an old instruction pointer.
- Stop cancels the active goal safely. Pause preserves intent and releases at a safe checkpoint. Stop-after completes the current bounded quest or duty and pauses before Nexus schedules another activity.
- The embedded Wrath combat hot path must remain isolated from database, network, Discord, goal planning, and heavyweight UI work.

None of the planned general orchestration, retry, cancellation, or goal/task recovery loop is live yet. The navigation-only reload reconciler and ownership-expiry watchdog are live safety observers, but no executor can create gameplay work for them.

## 9. Important Product and Architectural Decisions

### Product identity and composition

- **IMPLEMENTED / RECOVERED DECISION — permanent identity:** `VieriNexus` is the permanent display and internal name. The earlier working name `VieriDirector` is historical only.
- **PLANNED — final topology:** one installed VieriNexus package plus genuine external dependencies. The eight original Vieri products and later VieriNavPlotter are migration sources, not permanent peer products.
- **RECOVERED DECISION — neutral names:** predecessor product names must not appear as normal Nexus page, module, service, or marketing names. They may appear in importers, provenance, release notes, and temporary compatibility diagnostics. Mappings are:

| Migration source | Neutral Nexus destination |
| --- | --- |
| VieriCodex | Progression, Questing, Hunting Log, Achievements, Exploration |
| VieriAutoDuty | Duties, Gear & Inventory, Travel Utilities |
| VieriRotationHelper | Rotation Engine, Combat Suggestions, Keybinds |
| VieriAvarice | Positional Guidance |
| VieriDeck | Plugins |
| VieriLink | Communications |
| VieriAutoMarket | Market |
| VieriDelvUI | Custom UI, Nameplates, Overlay Presentation |
| VieriNavPlotter | Routes & Navigation |

- **ABANDONED/SUPERSEDED — leave DelvUI separate:** the first architecture response proposed leaving VieriDelvUI separate. The user explicitly corrected this. VieriDelvUI must be absorbed as neutral Custom UI; it is not a permanent dependency.
- **ABANDONED — standalone Hilda layer and marker plugin:** VieriHildaLayer is obsolete and excluded. MarkerIconPriority already lives inside VieriDelvUI and must not reappear as a ninth original product or separate install.
- **IMPLEMENTED IN ROTATIONHELPER / PLANNED FOR NEXUS — WrathSwitch:** WrathSwitch functionality is already part of VieriRotationHelper, including F1/manual ownership behavior and legacy aliases. Do not create a separate Nexus WrathSwitch module.

### Migration strategy

- **RECOVERED DECISION — strangler migration:** coexist with working standalone plugins, wrap/observe them where necessary, migrate coherent bounded subsystems, prove parity and rollback, then retire their predecessor. The user explicitly requested larger implementation slices where safe; repetitive micro-gates are not the desired development rhythm. An all-at-once source merge remains rejected.
- **RECOVERED DECISION — migrate behavior, not files/classes:** do not build `Codex.cs`, `AutoDuty.cs`, etc. Shared navigation, state, ownership, dependencies, recovery, inventory interpretation, and logging should become shared infrastructure. Domain behavior remains in bounded modules/providers.
- **IMPLEMENTED — current authority:** VieriNavPlotter remains authoritative whenever it is loaded. When it is off and the working library/providers are ready, Nexus automatically owns route actions for the session; source reappearance or safety/conflict loss revokes that authority immediately. Nexus never toggles the predecessor. Explicit Play/Travel uses the Nexus-owned complete-trip provider for same-zone or cross-zone work; Lifestream handles transfer/inn entry and vnavmesh handles authored local points.
- **RECOVERED DECISION — configuration safety:** every existing setting, option, keybind, route, profile, and hard-won fix must be mapped or explicitly retired. Source files stay intact. Import uses preview, backup, staging, atomic commit, receipt, validation, and rollback.
- **RECOVERED DECISION — friend/multi-user behavior:** another user installs the same product but imports and uses their own local settings. Character data is isolated by content ID/world. Never copy one user's config/secrets into another user's package.
- **IMPLEMENTED — first importer and working consumer:** Routes & Navigation is the first live slice because route data is structured and non-secret. Import/reload/rollback staging stays immutable; a separate working library supports authoring, preview, one-click same/cross-zone Play and Travel, immediate Stop/restart, vendor assignments, and compatibility-shaped read-only IPC. Version 0.1.0.25 received live acceptance for vendor and Grand Company inn travel in both directions. Version 0.1.0.27 removed the temporary VieriAutoDuty route bridge; 0.1.0.28 replaced literal movement with corridor pathfinding, and 0.1.0.29 prevents saved flight permission from requesting a nonexistent city flight volume. The user accepted both Faezghim and Grand Company inn playback on 0.1.0.29, completing focused live parity for the Nexus-owned route provider.
- **DEFERRED — Communications import:** VieriLink configuration may not even be opened until a dedicated encrypted-value adapter and same-Windows-account round-trip tests exist. File existence is the only allowed generic discovery signal.

### Dependencies and external ownership

- **CURRENT TRANSITION RULE:** stock Questionable is the only quest runtime and VieriCodex is a settings-migration source only. A loaded VieriCodex blocks Nexus quest delegation until it is disabled; it is never a selectable runtime candidate.
- **CURRENT TRANSITION RULE:** stock AutoDuty must not be enabled beside VieriAutoDuty while both could act. After Duties/Gear/Inventory parity, the approved target is to retire the Vieri fork and use stock AutoDuty as a capability-versioned, module-scoped duty provider while Nexus owns all Vieri-specific routes/travel, progression loops, Last Run, gear/inventory, maintenance, telemetry, commands, and UI. `docs/AUTODUTY_PROVIDER_MIGRATION_AUDIT.md` is the authoritative inventory and retirement checklist.
- **IMPLEMENTED:** required and recommended dependencies have their own page with install/manage actions. Recommended integrations do not block setup.
- **PLANNED:** modules/providers eventually declare their own manifests and tested version/health contracts rather than dependency checks being scattered or permanently global.
- **RECOVERED DECISION:** third-party providers keep their native implementation. Nexus coordinates them and owns cross-provider policy/resources; it does not take ownership of their update channels.

### UI and interaction decisions

- **RECOVERED CORRECTION / IMPLEMENTED:** the logo is a permanent Home-page hero, not a random splash popup. The redundant top VieriCodex icon, Nexus name, and “unified automation” header strip was rejected as clutter and removed.
- **RECOVERED REQUIREMENT:** retain the dark, red, gold, highly polished control-center feel from the reference images, but favor clarity and scalability over copying a mockup literally.
- **RECOVERED REQUIREMENT:** nothing should appear before a targetable character and stable territory are fully loaded. This rule was applied across several predecessor overlays and is implemented in Nexus with a 900 ms gate.
- **PLANNED:** all in-world Nexus overlays use shared loading/visibility/native-UI occlusion rules. Rotation suggestions must render behind native FFXIV windows without changing decisions, hotkeys, forecasts, or layout.

### Automation, ownership, and persistence decisions

- **RECOVERED DECISION:** goals express desired state; tasks express bounded work; plans are derived and disposable; verified progress is durable.
- **RECOVERED DECISION:** use short-horizon, continuously reconciled plans rather than a giant once-only DAG for an entire 1–100 journey.
- **RECOVERED DECISION:** planning has no side effects. A provider receives an immutable execution request only after all required resources are granted.
- **RECOVERED DECISION:** facts are typed events; requests are commands. UI, hotkeys, IPC, and Discord eventually share one authorized/deduplicating command gateway. Do not event-source the entire application; store current state plus a bounded audit log.
- **RECOVERED DECISION, WITH ROUTE-SPECIFIC SUPERSESSION:** user controls must have explicit predictable semantics. For Routes, the user explicitly chose button/command-only Stop; manual movement must not cancel travel. Manual targeting, F1 rotation control, camera/UI interaction, Pause, Stop, and Last Run semantics remain domain-specific and must not fight a working encounter/targeting provider.
- **RECOVERED DECISION:** persistence resumes intent, not unsafe UI instruction pointers. Completion is verified from game state rather than trusted from a provider message.
- **PLANNED:** JSON for settings/layout/profiles; SQLite for goals/tasks/attempts/checkpoints/history/observations. SQLite has not yet been explicitly approved by the user or added to the project.

### Release and update workflow

- **RECOVERED REQUIREMENT:** ordinary Vieri development updates are expected to include implementation validation, version synchronization, commit, push, shared website/Dalamud publication, runtime/source archive and hash validation, and a release Discord-bot/changelog update unless the user explicitly asks for local-only work.
- **AUTHORITATIVE PRODUCTION FEED:** users configure Dalamud with `https://www.thedailypilcrow.com/dalamud/pluginmaster.json`. This exact URL is durable project state and must not be replaced casually.
- **RELEASE-DEFINITION RULE:** a local build or successful Git push is not a published release. A release is complete only when the intended commit/version is packaged, the public artifacts and feed are updated, the live feed points at the intended artifacts, Dalamud can see/install/update the release, hashes/artifacts are validated, and the release Discord-bot/changelog update has been sent/verified.
- **SHARED-INFRASTRUCTURE RULE:** the Daily Pilcrow feed/hosting serves more than Nexus during migration. Never regenerate or edit it destructively in a way that drops or breaks unrelated Vieri plugin entries or download URLs. Standalone products remain published until their Nexus replacement is explicitly retired after parity.
- **DISCOVER-DON'T-INVENT RULE:** exact website source paths, deployment commands, archive filenames, bot command/webhook mechanism, and feed fields must be derived from the current working infrastructure/repositories/scripts/live feed before a release. If they are not yet documented, the first release-capable thread must inspect and record them in this file instead of guessing.
- **IMPLEMENTED POLICY:** a predecessor fix is not complete for the consolidation effort until Nexus pins the exact source revision and records the relevant behavior/regression requirement. `docs/MIGRATION_AND_UPSTREAM_POLICY.md` rule 13 is authoritative.
- **RECOVERED REQUIREMENT:** future Questionable, AutoDuty, Wrath, Avarice, DelvUI, and other upstream integrations must be repeatable. Fetch/diff first, classify changes, update only the owning module, reapply Vieri patches visibly, run contract/replay/config/legacy IPC/package tests, update source lock/notices, then publish.

The full production/distribution operating contract is in section 18.

### Preserved predecessor behavior that Nexus must eventually match

These are migration requirements, not current Nexus features:

- **VieriCodex -> Progression:** MSQ; class/job/role and side quests; Hunting Logs and Grand Company logs; achievements; Aether Currents/aetherytes/exploration; prerequisites and gating; FATE syncing; searching/alternate mob locations; flight/landing/dismount/stuck recovery; Progress Atlas; job switching/level goals/dungeon progression; One Click Navigation; named VieriNavPlotter routes; gear-readiness coordination; solo duties; maintained Questionable integrations. The 15.756.0.0 integration adds 7.56 MSQ routes 5475-5478, Beastmaster routes 5490-5491, and Beastmaster quest/category mappings. VieriCodex 1.12.2.80 then incorporates Questionable 15.756.0.1 at source `5c03483bccd2551257b61c45dcf9a5d53fbab844`, adding Beastmaster quest 5492 `Hearts Aligned`, its level-16/class-switch handling, Bastion placeholder mapping, current Allied Society revalidations, and the enabled Boss Mod handoff for quest 5476 `A Rush of Cold Wind`. Both integrations retain VieriCodex's live gearset refresh, empty-list guard, class/job eligibility policy, solo-duty targeting safeguards, and named routes. Questionable's intentional suppression of TextAdvance's quest-object `IN` option during controlled quest work was accepted unless a concrete missed interaction proves it wrong.
- **Current VieriCodex integration:** version 1.12.2.82 at `173d6ad599d2c057e0f88cea76ed302a7746bf32` incorporates Questionable 15.756.2.5 at `e21fec6934db687829b9530394a709a5c1eb1d52`. It includes the 149-file Beastmaster/Chocobo path, route-metadata, inactive Stop-settings, and comment-field update while preserving the Vieri live-gearset filter and empty-result guard, Progress Atlas, Progression Queue, named routes, solo-duty coordination, branding, and saved settings. The exact five-file semantic route delta and retirement gates are in `docs/QUESTIONABLE_PROVIDER_MIGRATION_AUDIT.md`.
- **VieriAutoDuty -> Duties/Gear/Inventory:** stock duty paths, duty loops and Last Run; gear planning/shopping/equipping/gearset update/displaced-item cleanup; empty/weak slot handling; EXP-item protection; vendor choice and redundant-trip avoidance; repair/extract/desynth/sell/turn-in/coffers/armoire/Triple Triad; striking-dummy travel; recovery; Codex/Wrath/BossMod/Link coordination; local and Discord status.
- **VieriRotationHelper -> Combat:** embedded Wrath engine for all supported combat jobs, auto-rotation/action replacement, Single Target/AoE/Dynamic Hilda-style suggestions, multi-action forecasting, simple/advanced modes, actual hotkeys, cooldown/GCD/weave/charge state, positionals, range/enemy counts, F1/manual/in-combat ownership, legacy Wrath/Switch commands and IPC, configuration import, duplicate-hook protection. Damage suggestions are mature; comprehensive healing/utility sequences were explicitly not implemented as of the recovered conversation.
- **VieriAvarice -> Positional Guidance:** rear/flank/any verdict, shared same-frame combat forecast, green positional/range guidance, confirmed miss versus unknown distinction, BossMod/AutoDuty coordination, native UI occlusion.
- **VieriDeck -> Plugins:** IMPLEMENTED in 0.1.0.53 and corrected in 0.1.0.55 as a Nexus-owned plugin launcher with complete transactional configuration import, atomic editable working state, rollback, fresh initialization, Dalamud controls, favorites-first grouping, filters/hidden entries, preferred/custom commands, editable exact-modifier hotkey, quick opening/toggling/settings, inline command expansion, and preserved layout reference. VieriDeck remains installed only through the ordinary acceptance/rollback gate, after which deliberate package/feed retirement can proceed.
- **VieriLink -> Communications:** Discord status/alerts/remote commands, editable permanent status cards, independent per-user/per-channel state, duplicate recovery, authorization, acknowledgements, audit, protected secrets. Retry edits after timeout/rate limit/outage/permission errors; create a replacement post only when Discord confirms deletion; recover an existing post when a local message ID is lost. Do not automatically delete historical duplicates.
- **VieriAutoMarket -> Market:** Marketbuddy and Allagan Market integration, full retainer scans, HQ/NQ and owned-retainer identity, duplicate listing coordination, exact owned-retainer matching, external undercut rules, cooldown pacing, bounded retry/stop/reporting, and verified saved prices.
- **VieriDelvUI -> Custom UI:** useful HUD behavior, independently enabled/lazily initialized elements, layouts, nameplates, marker priority, and overlay presentation. The 2.8.0.0 integration adds Beastmaster HUD/job mapping and current distance/ready-check API compatibility without changing Vieri branding, commands, profiles, highlighting, nameplate stacking, native enemy markers, Duty Support companion roles, or saved settings. DelvUI-derived licenses/notices/provenance must remain traceable even though user-facing names are neutral.
- **VieriNavPlotter -> Routes & Navigation:** timed/manual recording, named routes, notes/tags/search, point editing/reordering, duplication, import/export, resizable library, exact current-position copy, stable IDs, assignments/overrides, connected/numbered previews, live vnavmesh chain, travel to start/destination, ordered playback, stop, and cross-zone suite travel.

## 10. Current Development State

### Git and release state

- Branch: `main`.
- Current release source/implementation commit: `73fc2352fb0ba755d6bbd89ac3d526548fa21513 Rebuild VieriDeck replacement as Plugins page`; publication is recorded in this current `PROJECT_STATE.md` update.
- `origin/main` contains both the released implementation and published-source state commits.
- Recovery implementation commit: `ecaa8c7 Add transactional route migration`; the working tree was clean before `PROJECT_STATE.md` was created.
- No tags exist in this repository.
- Origin: `https://github.com/iampilcrow/VieriNexus.git`.
- Plugin project version: `0.1.0.62`. Dalamud API 15. Version 0.1.0.61 remains the currently published production package until the 0.1.0.62 release workflow completes.
- Production Dalamud custom-repository URL: `https://www.thedailypilcrow.com/dalamud/pluginmaster.json`.
- Distribution website/domain: `https://www.thedailypilcrow.com`.
- The authoritative deployment source is `D:\FFXIV Plugins\TheDailyPilcrow` / `https://github.com/iampilcrow/TheDailyPilcrow.git`; the live feed and versioned archives are under `public/dalamud/` and are deployed through the linked production Vercel project.

### Significant history

- `ff74fb2` — initial foundation.
- `ac82e26` — expanded dependency audit and permanent Home experience.
- `a83961a` / `d04b480` — carried current quest/gear fixes forward and made pinning every predecessor fix mandatory.
- `08b5b84` through `74b1949` — established navigation module metadata and successively pinned route-review, live visualization, vendor arrival/travel, market safety, off-hand safety, native UI occlusion, and measured vendor-route corrections.
- `ecaa8c7` — added the transactional NavPlotter importer, backup/atomic store/receipt/rollback, UI card, migration state, and tests; 720 insertions across 14 files.
- `54510ce` — released 0.1.0.4 with scale-aware Migration-card layout, untruncated button/status text, explicit zero-personal-route success wording, and a regression test.
- `101058d` — released 0.1.0.5 with hash-verified staged-receipt recovery across plugin reloads and compare-before-swap world revision publication.
- `460e730` — released 0.1.0.6 with the verified read-only Routes page, pure route query behavior, and four Nexus-namespaced read-only navigation IPC calls without activation or legacy-name collisions.
- `f073d94` — released 0.1.0.7 with content-aware Staged Settings panel sizing after the first live Routes screenshot exposed its clipped final row; also records successful guarded rollback/re-import and page-state synchronization.
- `7df701c` — released 0.1.0.8 with a fail-closed activation/conflict policy, runtime source/lease/dependency assessment, visible safety blockers, activation-status IPC, and contract serialization tests without enabling execution.
- `e99faaf` — released 0.1.0.9 with idempotent verified Stop coordination, separate vnavmesh inactive-path confirmation, lease retention for every unconfirmed outcome, and removal of the duplicate source-owner line without enabling execution.
- `685dca5` — released 0.1.0.10 with configured-action manual-movement observation, start inhibition, quiet-period handling, verified Stop takeover, and explicit-resume latching without enabling execution.
- `3900673` — released 0.1.0.11 with a minimal atomic execution-intent journal, no-replay reload/shutdown reconciliation, exactly-once lease-expiry observation, and an every-draw Navigation/Movement watchdog without enabling execution.
- `dc9242f` — released 0.1.0.12 with reversible session-only navigation-authority approval, atomic resource probing, source/safety conflict revocation, and a guarded future execution-entry boundary without enabling route execution.
- `b12f2f3` — released 0.1.0.13 with live navigation provider health, a bounded transition audit, shared provider snapshots, and an isolated five-scenario non-moving safety simulation.
- `723ef16` — released 0.1.0.14 with explicit stopped-intent acknowledgement after confirmed safety conditions and an isolated sixth provider-loss/retry simulation scenario.
- `f5c6f91` — released 0.1.0.15 as the first live Routes & Navigation vertical slice with a separate Nexus working library, manual authoring, static world preview, guarded same-zone travel/playback/Stop, and non-interrupting yield when another plugin replaces the vnavmesh path.
- `983948e` — released 0.1.0.16 with observation-only timed recording, capture/display preferences, detailed point editing, confirmation-protected clearing, duplication, compatible bounded route exchange, and automatic preview refresh.
- `448bea1` — released 0.1.0.17 with recording/display controls moved above the fixed route editor after the first 0.1.0.16 screenshot exposed nested-scroll discoverability.
- `c879c3f` — released 0.1.0.18 with working Clear all points/Delete route confirmations and one naturally sized Routes-page scrollbar instead of fixed nested route scroll regions.
- `cf4313c` — released 0.1.0.19 with the complete 27-route verified vendor-template catalog, safe personal copies, explicit exact-target assignments, atomic one-winner enforcement, and read-only fail-closed assignment resolution without enabling Gear dispatch.
- `5b908e91` — released 0.1.0.20 with exact current-target capture, filtered live generated-waypoint display, guarded cross-zone delegation, and the first fail-closed Nexus-to-VieriAutoDuty route-provider contract.
- `f359e900` — released 0.1.0.21 so fresh-route creation remains directly available below Search after the working library contains one or more routes.
- `d91be400` — released 0.1.0.22 with automatic route authority while VieriNavPlotter is off, consistent same/cross-zone suite dispatch, Play from anywhere including one-point routes, immediate Stop/restart, and the compact categorized Routes UI.
- `544bdc85` / 0.1.0.23 paired with VieriAutoDuty `3ae9957838110d457554b39f7c71469bf904727d` / 1.0.0.439 to enter Grand Company inns before playing authored route points and to forward the then-current manual-takeover policy to the complete Nexus-dispatched trip.
- `4cfc8962` / 0.1.0.24 paired with VieriAutoDuty `a5e1e757e35bd77191a647add7124210cdf86122` / 1.0.0.440 to preserve that trip across both loading screens and make its Stop result idempotent.
- `8c3829ec` / 0.1.0.25 makes user cancellation explicitly button/command-only and prevents transient post-teleport character snapshots from stopping an active trip.
- The user accepted 0.1.0.25 vendor and Grand Company inn route playback in both directions with no issues. The temporary UI result correctly identified VieriAutoDuty as the current whole-trip provider and prompted the approved stock-provider migration audit.

### Last completed work

The predecessor task's last successful turn implemented and published `0.1.0.3`:

- exact personal NavPlotter field mapping and validation preview;
- timestamped source/prior-Nexus backups;
- atomic inactive Nexus route-library staging;
- SHA-256 migration receipt;
- rollback protected from overwriting newer Nexus route data;
- Migration-page controls and persistent import state;
- 18 reported Nexus tests passing;
- reported website/package/hash/Discord/release inventory validation.

The current task then implemented and published `0.1.0.4`:

- action widths are measured from rendered labels and actions stack when the row is too narrow;
- preview, issue, operation, and staged-safety text wraps instead of clipping;
- the Migration card height scales with configured UI scale;
- zero-route success explicitly confirms that settings were imported;
- 19 Nexus tests and a zero-warning Release build passed;
- source commit `54510ce` and Daily Pilcrow release commit `684703f` were pushed;
- local/public runtime and source archives passed ZIP, identity/version, HTTP, and SHA-256 validation;
- Vercel deployment and Discord workflow `34282262570` succeeded.

The user's 0.1.0.4 reload test then proved that the configuration, staged route file, backups, and receipts survived disabling/re-enabling Nexus, but the green import message did not. The cause was service-local receipt state being initialized only during the import click. Version 0.1.0.5 now reloads the persisted receipt ID, verifies its source ID, target path, target hash, schema, and staged payload, and reconstructs the exact staged status without touching VieriNavPlotter. The same slice fixes `WorldStateStore.Publish(...)` so a rejected non-increasing revision never replaces `Current` or raises `Changed`. All 21 Nexus tests and the zero-warning full Release build pass. Source `101058d`; Daily Pilcrow release `a2bc367`; deployment `dpl_BnbzJgDxC1xQYp35B3tCqGKeffka`; live runtime/source archives and Discord workflow `34295565914` are verified.

The user then confirmed 0.1.0.5 restores the exact staged message after disabling/re-enabling Nexus. Version 0.1.0.6 builds the first consumer of that recovered data: a dedicated read-only Routes page and versioned Nexus navigation IPC. Both consume only the verified staged snapshot. The empty-library view still exposes imported preferences; non-empty libraries add search, details, ordered points, and visible inactive execution controls. The IPC surface provides API version, staging status, list, and detail calls under `VieriNexus.Navigation.V1.*`; it deliberately does not claim `VieriNavPlotter.*`, resolve overrides, or run routes. All 23 Nexus tests and the zero-warning Release build pass. Source commit `460e730`; Daily Pilcrow release commit `3be445d`; production deployment `dpl_2KVVzGiGwUAZQh7WGJp4WUL8LrDy`; live runtime/source archives and Discord workflow `34299175777` are verified.

The user's first 0.1.0.6 Routes-page screenshot confirmed the verified zero-route state, summary cards, explanatory copy, and first three staged settings render correctly. The user also confirmed guarded rollback and re-import both work and immediately reflect the correct no-staging/staged state on the Routes page. The screenshot exposed that the fourth `Live navigation path` row was clipped inside the Staged Settings child because that panel still used a fixed 150-pixel height. Version 0.1.0.7 derives the panel height from the active text-line metrics, window padding, and item spacing so all four preferences remain visible across supported interface scales. No migration, staged data, IPC, source-plugin, or navigation behavior changes in this correction. All 23 Nexus tests and the zero-warning Release build pass. Source commit `f073d94`; Daily Pilcrow release commit `ac73044`; production deployment `dpl_5vFZEh1q21VqVQZKkjCBW3UyziPn`; live runtime/source archives and Discord workflow `34300556246` are verified.

The user confirmed all four Staged Settings rows are visible on 0.1.0.7 and asked production to continue toward full capacity. Version 0.1.0.8 introduces the next navigation safety layer without an executor: a pure fail-closed activation policy, runtime assessment of source plugin/dependencies/current leases, a visible Activation Safety panel, and a versioned activation-status IPC. Stop, manual override, reload reconciliation, explicit approval, and execution remain false, so the current build cannot activate. Non-empty compatibility JSON now has direct regression coverage that preserves ordered points and a disabled override. All 29 Nexus tests and the zero-warning Release build pass. Source commit `7df701c`; Daily Pilcrow release commit `221b975`; production deployment `dpl_GUke8drcXKKab22w9d7o79wW2zSp`; live runtime/source archives and Discord workflow `34301968187` are verified.

The user confirmed the live 0.1.0.8 Activation Safety panel correctly detects loaded VieriNavPlotter ownership and displays every fail-closed blocker without clipping. That screenshot also exposed a duplicated source-owner sentence. Version 0.1.0.9 removes the duplicate and implements the verified Stop foundation: an idempotent coordinator, a vnavmesh Stop/activity adapter, retained Navigation/Movement ownership for unavailable/failed/unconfirmed/still-moving outcomes, and release only after explicit inactive confirmation. It does not add route execution, a public Stop command, or activation. All 35 tests and the zero-warning Release build pass. Source commit `e99faaf`; Daily Pilcrow release commit `8f08016`; production deployment `dpl_5GqCHKMNSyqhmEKm3LurT5c7ZaRd`; live runtime/source archives and Discord workflow `34303920092` are verified.

The user confirmed the live 0.1.0.9 panel shows Verified Stop connected, removes the Stop blocker and duplicate source-owner sentence, keeps all four staged settings visible, and still blocks manual movement/reload/approval. Version 0.1.0.10 implements the manual-movement foundation against FFXIV's configured movement actions. Player input blocks new starts through the quiet period; a takeover during tracked navigation latches verified Stop and cannot auto-resume. Turning the character protection setting off during tracked navigation also stops and blocks. All 43 tests and the zero-warning Release build pass; execution remains unavailable. Source commit `685dca5`; Daily Pilcrow release commit `b872861`; production deployment `dpl_ACdJrq7HN6ENtipicZungpjSE66n`; live runtime/source archives and Discord workflow `34306054991` are verified.

The user confirmed the live 0.1.0.10 panel shows both Verified Stop and Manual movement yielding connected, removes the manual blocker, leaves only reload reconciliation and explicit approval, preserves the complete staged-settings view, and remains staging-only under VieriNavPlotter ownership. Version 0.1.0.11 implements reload/no-replay and active-watchdog readiness without an executor. The atomic journal stores only execution, route, lease, state, and timestamp; startup, shutdown, and missed heartbeat paths can only Stop and require acknowledgement. All 59 tests and the zero-warning Release build pass. Source commit `3900673`; Daily Pilcrow release commit `12ba98a`; production deployment `dpl_E3gfwt2hYyXP9iRvgFkhR7K1EgQm`; live runtime/source archives and Discord workflow `34308239307` are verified.

The user asked production to continue without a separate 0.1.0.11 screenshot. Version 0.1.0.12 implements the next ownership boundary without execution: VieriNavPlotter must be manually unloaded, all gates must pass, and the user must explicitly approve a session-only Nexus authority state. Nexus does not toggle the source, approval starts no movement and does not survive reload, and a return-to-staging action is always available while active. The future execution gateway rechecks the source, atomically acquires Navigation/Movement, persists no-replay intent, and registers verified Stop before any provider call. All 71 tests and the zero-warning Release build pass. Source commit `dc9242f`; Daily Pilcrow release commit `8b92bc4`; production deployment `dpl_9jYWQzsMA38HwqHPPhJygzqvsusU`; live runtime/source archives and Discord workflow `34342268338` are verified. The user then confirmed the complete live handoff behavior: exactly three green safety guarantees appear while VieriNavPlotter is loaded, approval remains disabled with manual-unload guidance, manually unloading it enables approval, approval changes the panel to session authority without starting movement, and the return-to-staging action remains available.

Version 0.1.0.13 implements the next non-moving observability slice. Six live navigation provider/safety observations appear in Provider Health, feed the shared world snapshot, and create a bounded session audit only when state/code changes. The explicit simulation uses isolated memory-only state and a provider with no movement operation to exercise five production safety transitions without touching the live lease manager or journal. All 77 tests and the zero-warning Release build pass. Source commit `b12f2f3`; Daily Pilcrow release commit `730d3fc`; production deployment `dpl_2ANyPZSoLMN2bHHejHRRoNLLgEH1`; live runtime/source archives and Discord workflow `34344736882` are verified. The user confirmed the simulation passes 5/5 while VieriCodex is actively moving the character through a duty, with no movement interruption.

Version 0.1.0.14 adds the explicit stopped-intent acknowledgement/reset gate and provider-loss retry evidence without enabling execution. The conditional panel cannot clear a checkpoint until Stop is confirmed, ownership is released, and manual input is quiet; its action cannot resume/replay movement or approve authority. The isolated simulator now covers six scenarios, adding provider unavailability followed by automatic Stop retry and no-replay recovery. All 82 tests and the zero-warning Release build pass. Source commit `723ef16`; Daily Pilcrow release commit `7a74a9d`; release deployment `dpl_2wWdfsvANQyPyJh4WavnPqUCzo49`; documentation commit `1421810`; final production deployment `dpl_3p3i4TWqaX1fZqH1xWgcbLDfGydr`; live runtime/source archives and Discord workflow `34348284685` are verified.

The user confirmed the 0.1.0.14 isolated simulation passes 6/6. Version 0.1.0.15 then changes cadence from micro safety releases to a working vertical slice. Verified staging remains immutable while an explicitly created Nexus working library becomes editable and recoverable. Manual route construction, metadata/movement editing, world preview, guarded same-zone Travel to Start/Play Route, UI/command Stop, per-frame lease heartbeat, natural completion, and shared-vnavmesh path ownership detection are connected. Another provider replacing the path causes observation-only Nexus yield without a Stop call. All 96 tests and the zero-warning Release build pass. Source commit `f5c6f91`; Daily Pilcrow release commit `2e5957a`; release deployment `dpl_AzHaXQoGRPjHFTcAXXgwxa5TH6z7`; documentation commit `f7dc587`; final production deployment `dpl_gCLKGwZ3T4FzgWVqgvyw834NUEFQ`; live runtime/source archives and Discord workflow `34353177958` are verified.

The user then accepted the complete 0.1.0.15 live flow: creating points, seeing the route, traveling to its start, ordered playback, stopping by button and manual movement, acknowledging the stopped-intent checkpoint, returning authority to staging, and manually re-enabling VieriNavPlotter all worked. No movement auto-resumed. Version 0.1.0.16 added non-moving timed recording, persisted capture/display preferences, detailed point replacement/reordering/removal, confirmed clear, route duplication, safe versioned clipboard exchange, compatible legacy NavPlotter JSON import, and automatic preview refresh. Recording stops on territory change, unavailable character/route, logout, or unload and never acquires movement authority. The user's first 0.1.0.16 screenshot confirmed the prior two-point route, preview, timed-recording action, point list, duplicate/import/export/clear actions, and exposed that the interval/spacing panel was effectively hidden below the 650-pixel inner route editor behind an outer scroll region. Version 0.1.0.17 moved that panel above the route split. Subsequent live testing reached Clear all points successfully, but neither it nor Delete route opened a confirmation because the buttons were inside an ImGui child scope while their modals were drawn outside it. The same fixed child created an unwanted nested scrollbar. Version 0.1.0.18 removes both route child scroll regions and draws the confirmations beside their triggers in the same scope. The user confirmed Clear all points and Delete route both execute after approval, both cancel without mutation, and no additional page scrollbars remain.

Version 0.1.0.16 is published from source `983948e`. All 109 Nexus tests and the zero-warning Release build pass. Daily Pilcrow release `8998162` is live in production deployment `dpl_GPTtNam5WBAjNbmN2X1jJTTLMLCv`; documentation commit `26c2351` is live in final deployment `dpl_AmoKc2NpssgjHUUhr2pCRdS7pZgh`. The 13-entry inventory guard passes, runtime/source archives return HTTP 200 as valid ZIPs with exact SHA-256 matches, and Discord workflow `34357630865` succeeded. Runtime/source SHA-256: `B954249F32925544D452794373854900686F44197C3D3A49F4858CC2C5D9EC0B` / `6BE381534DDE51D77F1942B6E5163CF7859CD8E3C932D7D57F7D0526FC19AB5D`.

Version 0.1.0.17 is published from source `448bea1`. All 109 Nexus tests and the zero-warning Release build pass. Daily Pilcrow release `8483657` is live in production deployment `dpl_87vVMtwQEz7qov5DqkG1tmEZjKFN`; documentation commit `af88ddf` is live in final deployment `dpl_DJvrUFqZeosXwHLwm1x1mAiBdUot`. The 13-entry inventory guard passes, both public archives return HTTP 200 as valid ZIPs with exact SHA-256 matches, and Discord workflow `34376408900` succeeded. Runtime/source SHA-256: `D0526057B0EF4DF26DD27A8E02A08C3B95A3D5D04986D1662B9463152AA989A8` / `D5996AC9EFDB4DE0127BA913D290147129073756E612DF86F1F771417D462E2D`.

Version 0.1.0.18 is published from source `c879c3f`. All 109 Nexus tests and the zero-warning Release build pass. Daily Pilcrow release `82dcb03` is live in production deployment `dpl_556SjcJWe7PoLTZQaNARqtmajPxp`; documentation commit `8be2946` is live in final deployment `dpl_EqtxtEeurK3Xx9VYzvptYigNbS7V`. The 13-entry inventory guard passes, both public archives return HTTP 200 as valid ZIPs with exact SHA-256 matches, and Discord workflow `34378647358` succeeded. Runtime/source SHA-256: `8313883E3311EACDB142CC8223317F2B0375029AD378DA67BC5EF7F51E7F38E8` / `50897328612B253C4A2D63DF6DC8177AB9185A217E6918C9086F4232326CE08B`.

The 2026-09-09 upstream/provider integration audit is complete. VieriCodex 1.12.2.80 integrates Questionable 15.756.0.1 from source `5c03483bccd2551257b61c45dcf9a5d53fbab844`; all 8,899 Questionable tests, one generator test, and 4,327 route-validator tests pass. Daily Pilcrow release `3d8da3b` is live in deployment `dpl_HLj1vJB27jSBPrQMEDQj9XnsDGRv`; documentation commit `095dae2` is live in final deployment `dpl_6ATv41dbDiAMea11RTcmsYdQh8V1`. Runtime/source SHA-256: `EBD76DDBB936D7F328F256E327C526B238FE463D4A413AB4BB6ACE67943C7A1B` / `A14344CF754528700766168AF142E4542677A306D19679D5D5D29E57C98B1B85`. Focused public HTTP/ZIP/hash verification and Discord workflow `34394318939` succeeded. Boss Mod 7.5.6.0 and Lifestream 2.5.4.21 retain every public contract used by current Vieri consumers, so unaffected plugins were intentionally not rebuilt.

Version 0.1.0.19 is published from source `cf4313c7495dae56a2556ef5362beba65692f482`. All 27 verified VieriAutoDuty gear-vendor standing points are immutable built-in references; adding one creates a new independent working-library route with its override disabled. Enabling an assignment is explicit and atomically disables a competitor for that exact territory/vendor pair. `VieriNexus.Navigation.V1.ResolveGearVendorOverride` exposes only read-only resolution and fails closed for absent, invalid, or ambiguous assignments. The resolver does not start navigation or dispatch Gear/Duties work. All 122 automated tests and the zero-warning Release build pass. Daily Pilcrow release `1f2e4e52c89b31982a934aa231fc263df8aeaacc` is live in production deployment `dpl_fHUdKtsGDeahepUuJRkDk3XbqN6s`; documentation commit `57b2f65218bf13966c047833fead181723cb8ed7` is live in final deployment `dpl_FCtMirNZWUsb1bC481W7aJSsHJhh`. All 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, production build, public HTTP/ZIP/hash checks, and Discord workflow `34397580461` succeeded. Runtime/source SHA-256: `304C545BD904A05A66309CF03A33E005C556BC023500C3DC985B261D1BEAB6F0` / `8BCD52022F3FDCB3FCE8BF20B02300979304DAE70232041BA5F2A8B41B547404`.

Version 0.1.0.20 is published from source `5b908e91fbded0e912a8f1811d2685aa88d55fae` together with VieriAutoDuty 1.0.0.438 from source `cffd9a021fe4fac1b74188d8e96ee19d41ebba43`. Nexus can bind the current in-game target without implicitly enabling an override, render live generated vnavmesh waypoints only for local or delegated travel started by the current Nexus process, and delegate cross-zone route travel through exact capability-checked VieriAutoDuty endpoints. VieriAutoDuty consumes an exact enabled Nexus vendor override only while Nexus owns navigation authority, then retains VieriNavPlotter and built-in fallbacks. Ordinary Questionable, duty, and unrelated vnavmesh activity is never adopted as Nexus route ownership. All 133 Nexus tests, 328 VieriAutoDuty tests, and the zero-warning Nexus Release build pass; the full VieriAutoDuty build succeeds with its known upstream dependency/nullability warnings and no errors. Daily Pilcrow release `ebd0301dcfc556fd9602b50b65ffce8a8074d454` is live in production deployment `dpl_sEFozsqZhLR3Ktej38Jd8xLJ6axa`; documentation commit `90fe7ed` is live in final production deployment `dpl_8wYp7CAXm1VysukNdWNdgDbdLdw6`. All 205 website tests, typecheck, focused validation for both packages, thirteen-entry inventory guard, production build, public HTTP/ZIP/hash checks, and Discord workflow `34404997766` succeeded. VieriAutoDuty runtime/source SHA-256: `2AC0815ECB5308DF11C16FB829C710CB5DD1F1E7F2933CD0D8875437298495FA` / `E3A6176293CF3CE7EEC4EB8A766F4F9AB18C54586AA4E29CB911D11C24620DA3`. Nexus runtime/source SHA-256: `915522F203EB38976B9E8FE1722278ED6C48867389ADE71E7C611ED80E9C4D7D` / `48C639E3F41977D30E50807FB9577EEBEDE5E3142E434261DCA71A79906B110A`.

The first 0.1.0.20 live acceptance attempt exposed a route-creation discoverability defect: `Create route at current position` was rendered only while the working library contained zero routes, so a user with any existing route could not create another fresh route. The underlying creation service remained available and safe. Version 0.1.0.21 renders the same fresh-route action directly below search for every populated working library without changing route storage, movement, assignments, or provider behavior.

Version 0.1.0.21 is published from source `f359e900e07e26db7d7f75acfb87f9b1e28c5d4e`. All 133 Nexus tests and the zero-warning Release build pass. Daily Pilcrow release `16ea250dd82eacb450343b1b0a44d19048393c99` is live in production deployment `dpl_7Njr1RqJhdWWnVtgEPpHbivbwPWi`; documentation commit `3b7a3551eab1e2813142c386b5b43ece123e35aa` is live in final production deployment `dpl_6CxW1J7NtAqsgHRxj3z5BuQPYr8t`. All 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, production build, live HTTP/ZIP/hash checks, and Discord workflow `34410254108` succeeded. Runtime/source SHA-256: `E1EC998539C85672E5BD57315E1A084B24A864A4C7F00B78F24DDB1D13332835` / `A79EB8CB23FD41C5C768A8EEC8E66C02CE2DC9051C2ACC2DEF47D54C59076443`.

The user then reported that a cross-zone Travel to Start reached Faezghim correctly, but after stopping inside the city a second Travel showed a green vnavmesh path without walking. The same live review found the staging/approval/acknowledgement workflow and page layout needlessly complex and required Play to work from anywhere. The root transition was Nexus changing from AutoDuty whole-trip travel to raw same-zone vnavmesh after arrival; flight-enabled city routes could therefore calculate a visible path without the suite travel behavior that had handled the first trip. Version 0.1.0.22 consistently prefers AutoDuty for the complete trip whenever available, retains raw same-zone vnavmesh only as fallback, and implements the simplified authority/Stop/UI behavior above.

Version 0.1.0.22 is published from source `d91be400b0b8c3741f87e64968372adb8091da7d`. All 136 Nexus tests and the zero-warning Release build pass. Daily Pilcrow release `90aef0118406bfb44e13bcb3425981ec137f81c8` is live in production deployment `dpl_J917MaCr7zxFZcj7qsRmmaYQnJii`; documentation commit `45d84411d31f3e3efcec2159c61dd7f583687d4d` is live in final production deployment `dpl_H1vpZKdz36ZnBeAVP1BSCvZdtUYf`. All 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, production build, public HTTP/ZIP/hash checks, and Discord workflow `34423862188` succeeded. Runtime/source SHA-256: `209528F99AEE59563143291328770721C5A85B371E87A54EBF34F064DAAE5345` / `F7A33F2BD74DC7250CD112FADC3FF537F5AEFB6D8AF5EFBC49F0DD60CB16F634`.

The user's 0.1.0.22 check exposed two narrower route-provider defects. `Inn Test` targets Twin Adder inn territory 179, which has no ordinary aetheryte destination; the generic suite transfer therefore could not reach it from outside. Manual-input safety also guarded direct Nexus vnavmesh execution but was not evaluated while the suite adapter owned the approach to a route. VieriAutoDuty 1.0.0.439 now owns an inn-aware complete-trip helper: territories 177, 179, and 178 enter through the matching Grand Company innkeeper and only then dispatch the authored route points. Nexus 0.1.0.23 evaluates its existing execution-safety decision throughout suite travel and sends the provider Stop as soon as player input takes priority. The provider continues to track only work Nexus explicitly started, so unrelated AutoDuty, duties, Questionable, and vnavmesh movement remain untouched. All 332 VieriAutoDuty tests and 137 Nexus tests pass; AutoDuty builds with its known upstream warnings and no errors, while Nexus builds with zero warnings/errors. Nexus source `544bdc8584538b758b155bdf89d2ebacad28268f` and VieriAutoDuty source `3ae9957838110d457554b39f7c71469bf904727d` are published through Daily Pilcrow release `3dd91a474831c80cfc184eeababc7157410c897e`, production deployment `dpl_EKUADL8PhqDf93pc7ZTyspNtesaU`, documentation commit `ccaa438b52b398f87c769c905cdb1c39064f0dac`, and final deployment `dpl_9qHXkYjuuq8ka8qjTBq8QTxXiGYF`. Public archives and Discord workflow `34426996224` are verified. AutoDuty runtime/source SHA-256: `CA183C192E98540502B593444BA4B184E7C456CEB07093F4B5DCF92E184A66DF` / `A7311B6DF6FFFE56A4118E887C90695348C5FBF92A9A1CADCC16BAD6BBDF001A`. Nexus runtime/source SHA-256: `C528E877E1F79A824EBF7E1B278C8CA1C7C25DA0E728809885CBC87AD105AEE5` / `3745AD4473D1B9458078D1269CE5FD8E01C9EF53CCB763BA4F2236B50368BAFE`.

The first live 0.1.0.23 attempt showed that special-destination selection was correct but its wrapper stopped at each loading screen because `Player.Available == false` was handled as cancellation. One click ended at the Gridania aetheryte; a second click entered the Twin Adder inn but ended before running the saved points. That premature end also caused a later idempotent Stop request to return false and display an incorrect instruction to use the AutoDuty UI. VieriAutoDuty 1.0.0.440 now waits through transient unavailability and resumes the same tracked trip when the world is observable; its Nexus-only Stop confirms success when the trip is already inactive. Nexus 0.1.0.24 removes the provider-UI instruction from genuine unconfirmed outcomes. All 337 AutoDuty tests and 138 Nexus tests pass; both builds succeed with Nexus at zero warnings/errors and AutoDuty at its unchanged known warning baseline. Nexus source `4cfc89624b4c53b4a1af85a0ea31477b19a98681` pins AutoDuty source `a5e1e757e35bd77191a647add7124210cdf86122`. Daily Pilcrow release `b6dd50f1efc941cf7e0948be47ec217a877fb338` is live in deployment `dpl_A2Vq7QJJMZ4kGNx1NEkw2mjJM8iW`; documentation `1234f53e2c32167f5df581aa02f64f1323b247a6` is live in final deployment `dpl_9S2t5JtUPaGbCZPuZt1MpaMjcutV`; Discord workflow `34429358396` succeeded. AutoDuty runtime/source SHA-256: `3F2050D07E654F481B2ACD4D8E741DC31F6E77CEEA461801ED62A995F87C7C74` / `531F1121300A13E5AF88B1012312B0FEA40857A4B223713D5379CCB72A21DC56`. Nexus runtime/source SHA-256: `8DB78B2A309A22EA2347F4C8D82A28FA4B7FB353F74214BFFDF86345F0DFE658` / `6F9692741433E1E499D157F3A99CB174CE7F1970C48919856E6720940466120C`.

The 0.1.0.24 live attempt then showed both Faezghim and `Inn Test` teleport once and immediately report `Player control took priority` without any actual input. The Nexus suite coordinator was reevaluating a combined character/manual start gate every frame; the character portion legitimately becomes unknown while the world snapshot rebuilds after teleport, but that transient false value was mislabeled as manual takeover and sent Stop. The user explicitly removed movement-to-stop from the product contract: route starts and active movement are no longer blocked or cancelled by keyboard, mouse, gamepad, jump, or autorun input. Only the visible Stop button or `/nexus stop` is a user cancellation. Version 0.1.0.25 evaluates character automation authorization only at start, never during zoning, disconnects the manual-input observer from production route control, removes its settings toggle, and retains provider-loss, source-conflict, reload, lease, and explicit Stop safety. The suite regression proves a start-permission change cannot stop an active trip. Source `8c3829ec1af2c25bacb00d204cb5a245033f2d83` is published through Daily Pilcrow release `509a2dc14a59bca290e782952cd7e2e9506bd86d`; documentation commit `406caa6083a908958dc66c25a094dfea58fee7e4` is live in final deployment `dpl_94SvkUgKzFyvTjgpvDPh5i7xFLMC` after release deployment `dpl_CCuCv4vzXa5cSGqSND4BmVRGLfxW`. Discord workflow `34431922374` succeeded. Runtime/source SHA-256: `8EC4F33FE1DEBEB67F88EF0AD72DC8F8C8BDD393587D8029E78128761B0D98CC` / `598786B08EC1FEB9D6D0A8DB67F2E2CF944BB23C6B7F09522F0314D15A73F89A`.

The user accepted the corrected 0.1.0.25 behavior in game: Faezghim and `Inn Test` worked in both directions with no issues. The status text showed that every complete route still runs through VieriAutoDuty, which is accurate for the temporary `TravelVieriRoute` bridge but not the intended end state. A source-level audit compared VieriAutoDuty `a5e1e757e35bd77191a647add7124210cdf86122` with stock AutoDuty `2b0943ed113da76f3ce9df0df2f302151f828292` at common ancestor `17f54e99235d84fe39582258eca7058fc5fb3e2b`: 105 Vieri commits, 110 changed files, 8,712 insertions, and 1,156 deletions. `docs/AUTODUTY_PROVIDER_MIGRATION_AUDIT.md` classifies the fork-only route bridge, vendor travel, gear, inventory/maintenance, progression/Last Run, duty fixes, integration/status, UI, 21 added persisted state fields/groups, stock IPC limitations, and retirement sequence. The approved permanent model is Nexus-owned Vieri behavior over module-scoped stock Questionable and stock AutoDuty providers; compatible upstream provider updates should not require Nexus changes.

Version 0.1.0.26 begins that model in production without taking authority from either fork. `ProgressionProviderService` checks only provider presence and the required narrow IPC members; it never invokes them. VieriCodex/Questionable questing requires `IsRunning`, `StartSingleQuest`, and `Stop`; VieriAutoDuty/AutoDuty duties require `ContentHasPath`, `Run`, `IsStopped`, and `Stop`. The policy selects one ready implementation per role and rejects two ready quest providers. The new Progression page persists a current-job target, job-quest/Hunting Log/side-quest/duty allowances, and gil floor, then previews Nexus gear policy, supported quest work, exactly one bounded duty, and verification/replanning. Source `ecde39a0ad1a3af5a2200cef5b575e03f7b6b010` is published through Daily Pilcrow release `92460bd6f83ea758cb34112af40b982ad63aecad`, deployment `dpl_hdrNfaWSarivsbKuk5cdMxKy7Kub`, and Discord workflow `34468325542`. All 149 Nexus tests, the zero-warning build, site/package/build checks, and live runtime/source hash validation pass.

The user then accepted the 0.1.0.19 vendor-template persistence gate in game: after copying a vendor template and enabling `Use as gear vendor override`, disabling and re-enabling Nexus preserved that assignment. No automatic movement was reported. Template copying, explicit assignment, and reload persistence are therefore accepted.

A read-only VieriCodex/Questionable architecture review found that stock Questionable exposes a useful but bounded IPC surface for starting/stopping supported quests and gathering work, querying current quest/step and quest eligibility/status, and managing its quest-priority list. It does not expose VieriCodex's Progression Queue, Progress Atlas, Hunting Log planner and target data, exploration/Aether Current/Aetheryte planners, one-click/local transport, gear-readiness and AutoDuty sequencing, solo-duty combat handoff, named VieriNavPlotter routes, custom UI/hotkeys/settings, or arbitrary custom quest-path injection. The user approved a capability-versioned hybrid target: retain VieriCodex as authoritative during migration, move the Vieri planning/policy/UI layer into Nexus, and prove stock Questionable as the external provider for ordinary supported quest execution before retiring whole-fork upstream merges. Custom or unsupported route data remains in a small Nexus-owned overlay/executor or is accepted upstream.

### Current workstream

The user had said all vendor routes were in a good place and instructed development to continue piecing VieriNexus together while preserving every setting and Discord key. The assistant chose Routes & Navigation as the first safe vertical migration slice and completed staging/rollback.

The user subsequently confirmed that VieriNexus installs and updates through Dalamud, that the Migration card successfully staged a valid VieriNavPlotter configuration containing zero personal routes, and that closing/reopening the window retained the staged message. Disabling/re-enabling 0.1.0.4 made only the in-memory message disappear. Direct inspection confirmed that Nexus configuration, `routes.v1.json`, backups, and receipts remained present; the current VieriNavPlotter source and its timestamped backup both still match the receipt's original SHA-256. The zero-route result is expected because recording/display/pane/selection settings are still migrated. The earlier fixed-size button and status clipping was corrected in 0.1.0.4.

Version 0.1.0.27 implements the first concrete fork decoupling: Nexus Routes no longer selects or calls VieriAutoDuty for movement. It owns the complete trip and composes stock Lifestream with vnavmesh. The Progression page now says `Current migration provider` for VieriCodex/VieriAutoDuty and `target` for stock candidates, avoiding the false impression that the forks are permanent selections. After focused live route parity, the next large slice turns one Progression preview step at a time into a durable task with explicit resource ownership, verified provider completion, Stop, and replanning; it must not delegate an endless Vieri loop or enable stock beside an active Vieri counterpart.

Live 0.1.0.27 testing confirmed Nexus-owned Lifestream transfer and local dispatch but exposed that mesh-assisted authored points were passed directly to `Path.MoveTo`. A one-point Faezghim route therefore became a literal straight line into Limsa geometry. Version 0.1.0.28 uses stock vnavmesh's cancellable pathfinder for every mesh-assisted authored leg, sequences multi-point routes, preserves direct movement only when `UseMesh` is explicitly false, applies the final-point tolerance to the final leg, and cancels pending calculation on Stop. This is a correction inside the Nexus provider and does not restore VieriAutoDuty coupling.

The focused 0.1.0.28 Faezghim retest completed the Lifestream transfer but then failed before movement. Dalamud log evidence at 2026-09-10 08:39:27 EDT shows vnavmesh started a flight path from Limsa's main Aetheryte to the saved standing point and returned `Nav volume was not built`; Nexus then accurately surfaced provider failure. Version 0.1.0.29 checks vnavmesh's supported flight territory categories before each leg, uses ground pathfinding in city territory 129, and retries a missing attempted flight path once on the ground. No provider failure is reclassified as success, and no VieriAutoDuty route call is restored.

The user then confirmed that 0.1.0.29 route playback worked correctly for both the Grand Company inn route and Faezghim. The Nexus-owned route-provider migration gate is accepted. The two visible line styles are separate intentional layers: the saved authored route preview uses a red route chain with amber/yellow points, while the current generated vnavmesh path uses a bright-green first segment and cyan remaining segments. A one-point vendor route has no multi-point authored chain, so its longer generated cyan path dominates; a multi-point inn route can show its authored chain and the short generated current leg at the same time.

Version 0.1.0.30 implements the first durable Progression executor instead of another preview-only safety step. A character-scoped atomic JSON document with a `.previous` recovery copy owns the Reach Job Level desired state, plan revision, bounded task history, active task, Last Run flag, provider-start checkpoint, duty-entry checkpoint, and matching game duty-completion checkpoint. Nexus selects the highest currently unlocked stable leveling duty that meets current level, item level, provider-path, and content-unlock requirements; acquires the complete DutyQueue/Teleport/UI/Inventory/Targeting/Rotation resource bundle and its implied Movement/Navigation/Combat resources; disables AutoDuty's own leveling scheduler; and calls the stock-compatible `AutoDuty.Run` contract for exactly one loop. It never calls the Vieri-only `StartProgressionLeveling` endless loop.

The bounded duty task succeeds only after provider start, correct duty entry, Dalamud's matching `DutyCompleted` event, provider shutdown, and return to the normal world. Wipes, abandonment, a wrong completion event, provider loss, job changes, reload, unload, or an unconfirmed Stop cannot count the duty or schedule a replacement. Stop-after allows the current verified quest or duty to finish and pauses before another task; normal completion either satisfies the target or replans exactly one next activity from the current permanent level. The simplified Progression UI exposes Start, Stop after this activity, Stop now, and Resume while keeping provider and plan diagnostics collapsed. Gear readiness and duties are live-accepted; exact quest execution is implemented in 0.1.0.39 and awaits ordinary-use validation without another long test script.

Version 0.1.0.39 adds `ClassJobRoleQuestPolicy`, covering every base combat class, crafting/gathering class, combat job, Beastmaster chapter, and applicable tank/healer/melee/physical-ranged/caster role families by stable game-data chapter IDs. `ProgressionProviderService` combines those families with live `QuestChapter`/`Quest` data and the selected provider's `IsQuestLocked`, `IsReadyToAcceptQuest`, `IsQuestAccepted`, and `IsQuestComplete` calls; it never asks the provider to choose a broad activity. The durable coordinator pins the exact quest ID/name/starting level, acquires Teleport/Movement/Navigation/Targeting/Combat/Rotation/UI resources, calls `StartSingleQuest`, confirms that the provider's current quest matches, requires completion of that same ID, and routes Stop/reload recovery to the quest provider. A mismatch, provider loss, early exit, timeout, job change, lease loss, reload, or unload stops and reconciles without counting or replaying the quest. Stock Questionable already exposes this complete contract in 15.756.0.1; Nexus therefore needs no fork patch for the bounded lane. VieriCodex exposes the same names under its compatibility prefix and remains usable only during transition.

The first 0.1.0.30 live Progression screen showed that the fixed 320-pixel Reach Job Level child and fixed 150-pixel Start child clipped wrapped text and the Start button; other fixed provider/plan children were exposed to the same defect. It also classified loaded VieriAutoDuty 1.0.0.440 as incompatible because the new contract required the direct `SetLevelingMode` action even though AutoDuty's older stock `SetConfig` can perform the same runtime-only `leveling=None` reset. Version 0.1.0.31 replaces all Progression child panels with content-sized table panels under the one page scrollbar, accepts either reset contract, prefers the direct endpoint when present, and reports the exact missing IPC member if compatibility still fails.

The focused 0.1.0.31 check proved the layout correction but still showed every AutoDuty IPC member missing. The live Dalamud log identified an external provider collision rather than another ABI guess: Daily Pilcrow VieriAutoDuty 1.0.0.440 (`WorkingPluginId 8ba52817-bbb3-44ef-8084-5a0f2f9513fd`) and stock AutoDuty 0.0.0.335 (`WorkingPluginId 289204c4-0195-42f9-84ae-cda4ae855af5`) had both loaded with internal/assembly name `AutoDuty`. Dalamud logged duplicate assembly, command, and plugin-key errors. When the duplicate was unloaded, shared ECommons IPC disposal left the remaining visible VieriAutoDuty instance without `ContentHasPath`, `IsStopped`, `Run`, `Stop`, or its reset calls. The saved profile now has VieriAutoDuty enabled and stock AutoDuty disabled; a clean game launch or reloading VieriAutoDuty after disabling stock restores a single provider registration. This is not safe to paper over in Nexus: never enable stock AutoDuty beside the Vieri fork during migration, and treat an installed-name collision plus an entirely absent IPC surface as a duplicate-provider/session-reload diagnostic.

After the duplicate stock AutoDuty was disabled and the provider registration was restored, the user accepted the complete first Progression duty lifecycle in game on 0.1.0.31. Nexus successfully started the initial bounded level-goal duty, stopped it explicitly, built a fresh plan through Resume, and started Mt. Gulg again through the normal provider path. This accepts Start, explicit Stop, durable goal retention, fresh-plan resume, eligible-duty selection, and repeated bounded dispatch. The next Progression slice should move the first gear-readiness/shopping transaction and its verification into Nexus rather than repeating this duty gate.

Version 0.1.0.32 implements that first gear-readiness transaction as a separate durable task before Nexus selects a duty. The task acquires Teleport plus implied Navigation/Movement, UI interaction, and inventory-mutation ownership; applies the goal's minimum-gil reserve and smart-vendor enablement as temporary VieriAutoDuty configuration overrides; invokes only the bounded gear-readiness mechanics; observes completion; restores the provider's prior settings; invalidates duty eligibility; and verifies live item level and gil before scheduling one duty. Gil may not fall below the lesser of the approved reserve and the transaction's starting balance, and item level may not regress. Stop, provider loss, reload/unload, and setting-restoration failure retain the existing fail-closed reconciliation behavior. VieriAutoDuty remains a temporary mechanics adapter because exact preview/approval, slot selection, equipment/gearset/displaced-item policy, and maintenance are not yet Nexus-native.

The user accepted the complete 0.1.0.32 transaction in game. Nexus went to the required vendor, bought gear, returned to the inn, and started the planned duty without another command. This closes the first gear-readiness and automatic plan-resumption gate; do not repeat it unless later shopping, equipment, travel, or progression changes create a concrete regression.

Version 0.1.0.33 moves the complete manual Shop for Upgrades review and approval experience into a dedicated Nexus Gear & Inventory page. VieriAutoDuty 1.0.0.441 exposes a versioned read-only live scan and accepts only an exact single-use approval. Nexus chooses the slots and pins the character, equipment signature, item IDs, quantities, maximum unit prices, and protected gil floor. Both sides reject a stale or changed plan before movement. A Nexus coordinator acquires and heartbeats Teleport, Navigation, Movement, UI Interaction, and Inventory Mutation for the full run, supports explicit Stop, retains ownership through provider loss until inactivity can be confirmed, and restores the temporary gil-floor override. The fork still owns vendor-band/catalog candidate generation and low-level purchase/equip/gearset/displaced-item mechanics; those remain the next Gear migration boundary.

The user accepted the 0.1.0.33 manual preview with a valid live no-upgrade result: `Found 0 verified upgrade option(s) for MCH.` No movement or spending began. Version 0.1.0.34 changed the provider boundary so Nexus applies job-primary-stat scoring, best-candidate ordering, weakest-ring comparison, active EXP-item protection, and effective two-handed/off-hand rejection. The user accepted that boundary when the same MCH gear again returned zero verified upgrades. Version 0.1.0.35 removes the fork from live-equipment and vendor-catalog lookup entirely: Nexus resolves the supported vendor band and low-level job family, traverses each curated NPC's real ordinary gil-shop graph, counts owned items, and creates the same exact approval for manual checks and automatic Progression readiness. The VieriAutoDuty automatic gear planner and raw snapshot are no longer called. Provider-side fresh-plan validation remains defense in depth; only physical purchase/equip/gearset/displaced-item mechanics remain in the temporary adapter.

Version 0.1.0.36 removes that final adapter. `NexusGearExecutionService` pins exact vendor identity into the approval; resolves a user-enabled route override or the immutable verified built-in route; travels through Nexus's own Lifestream/vnavmesh provider; opens only relevant combat-gear menu pages; rechecks live price, inventory space, and the gil floor before each item; confirms ownership growth; equips only approved item IDs into approved slots with stable verification and bounded retry; handles ring counts and two-handed layouts; updates the active gearset; and moves only equipment displaced by this transaction from the Armoury Chest to free bag slots. Explicit Stop halts travel and closes owned shop UI. A failed transaction never advances its completion sequence, and both manual Gear and automatic Progression distinguish that from success; automatic Progression will not queue a duty after an unconfirmed gear exit. No VieriAutoDuty gear IPC remains.

Version 0.1.0.37 adds the complete VieriAutoDuty operations-state importer rather than another isolated checkbox migration. `AutoDutyMigrationImporter` maps every profile, content-ID assignment, pending retired-equipment transfer, overlay/button choice, gear/repair/extraction/coffer/desynthesis/Grand Company/Armoire/Glamour/registration/selling rule, thresholds, opaque preferred-vendor payloads, and safe in-duty maintenance setting. The plugin service locates the real `AutoDuty/AutoDutyConfig.json` layout, previews the mapping, creates a timestamped untouched-source backup, atomically writes `NexusData/autoduty-operations.v1.json`, verifies a SHA-256 receipt on reload, and can roll back only while the staged target still matches its receipt. Import remains staging-only and cannot enable duplicate actions. The optional `NexusOperationsOverlay` preserves the compact Goto/Gear/Inventory/Extras organization while exposing only working Nexus route, shopping, Progression, Last Run, status, and global Stop controls; pending native maintenance and striking-dummy actions are omitted instead of rendered as disabled clutter. Overlay visibility, locking, transparency, and status-line display are Nexus settings.

Version 0.1.0.40 exposed an operations-import parsing defect on a real existing AutoDuty configuration: `AutoOpenCoffersGearset` was present as JSON `null`, but `NullableByte` called the numeric accessor without first checking `ValueKind`, throwing from both startup profile application and the Migration draw path. Version 0.1.0.41 makes all integer/unsigned/nullable-byte readers require a numeric JSON kind, makes scalar text treat explicit null as missing, ignores null/wrong-type character-ID entries, rejects non-object profile array entries as normal migration issues, and retains a final malformed-type guard so configuration preview cannot escape into the UI draw loop. Regression coverage uses the exact null coffer-gearset shape plus null/wrong-type values across every numeric family.

Version 0.1.0.38 turns that preserved snapshot into usable Nexus ownership. Every successful or recovered operations import is promoted into `NexusData/operations-profiles.v1.json`, separate from immutable staging, with atomic replacement, `.previous` recovery, receipt-bound rollback, validation, and content-ID/default-profile resolution. `NexusMaintenanceRuntimeService` directly runs the non-destructive/self-service subset—crafter repair, materia extraction, Triple Triad/minion/orchestrion registration, and eligible coffer opening—under exclusive UI/inventory leases with explicit Stop, character/combat/duty guards, timeouts, bounded retry, quantity/unlock verification, and gearset restoration. It never calls AutoDuty. Protected selling, desynthesis, Grand Company turn-ins, Armoire/Glamour storage, and in-duty withdrawal remain blocked until their destructive-item and recovery transactions are native.

Version 0.1.0.47 completes the protected between-duty item-transaction boundary without importing another plugin engine. `ItemTransactionPolicy` makes selling selection deterministic and independently testable; its single-use SHA-256 approval pins container, slot, item, quantity, vendor price, and spiritbond state. Nexus rejects EXP-bonus gear, collectables, gearset references, zero-price items, non-equipment, and ordinary tradeable zero-spiritbond gear; it revalidates the whole approval and each exact slot, requires an already-open ordinary NPC shop, verifies one mutation before touching the next item, and fails closed on any change. Native desynthesis applies the imported categories, NQ confirmation, skill-up gap, free-space requirement, EXP protection, and gearset protection. Grand Company turn-ins and eligible Armoire/Glamour storage remain replaceable mechanics calls through stock AutoRetainer and Glamour Log, but Nexus now owns their sequencing, leases, timeouts, provider observation, completion, and Stop. No AutoDuty maintenance IPC is used. The preserved in-duty withdrawal thresholds remain deliberately inactive because current stock AutoDuty has no verifiable leave/resume contract; Nexus explicitly reports that limitation instead of pretending that `Stop` withdrew and safely resumed a duty.

Version 0.1.0.48 closes the prerequisite gap around those narrow maintenance providers. Nexus derives the character's Grand Company, travels through its own Lifestream/vnavmesh coordinator to the exact personnel officer or corresponding inn, locates and approaches the exact Armoire/Glamour furnishing by event identity, opens the required UI, and invokes the provider only after the prerequisite is ready. Storage selection is verified from the live bags and current Cabinet/Mirage catalogs before travel and again after the provider stops. No eligible items is a successful verified no-op; unavailable verification, remaining eligible items, an already-busy provider, provider loss, timeout, or travel failure all fail closed. Stop aborts only Nexus-owned travel or Grand Company work and closes only the maintenance UI Nexus opened.

Version 0.1.0.49 moves the remaining VieriAutoDuty-specific control/status role into Nexus as one coherent boundary. `NexusControlService` supplies current-character, job, location, duty/queue/combat, item-level/gil, bag, durability, active-module/provider/activity, Stop-after, and completed-task telemetry without calling VieriAutoDuty status IPC. The same service executes bounded character-scoped, request-idempotent route, Progression, native-maintenance, review, UI, and global Stop commands for the custom overlay, `/nexus`, and `VieriNexus.Commands.V1.Execute`; `VieriNexus.Operations.V1.GetStatus` exposes the rich snapshot. Global Stop now includes standalone Progress Atlas work and cancelling paused/blocked Progression reconciliation. The custom overlay gains a first-class Duty category instead of hiding Start/Resume/Last Run under Extras. Unsupported fork-only leave, pause, endless-loop, and arbitrary-command behavior is explicitly rejected rather than translated into unsafe stock-provider calls.

Version 0.1.0.49 is published from Nexus source `748f9ba13f18c08d2fda4cf03229785b14fde3a0`. Daily Pilcrow release commit `249d30b189585b77c4a06886c54197077df98d54` and documentation commit `06522b06d8236eb1f4cfe9b67c572b8c29599be3` are pushed; final production deployment `dpl_JCuEggJLTMbVTzjsSch7Q8BtUSYm` is Ready. All 271 Nexus tests, the zero-warning Release build, 205 website tests, focused package validation, the 13-entry inventory guard, production build, and final live HTTP/ZIP/hash validation pass. Discord changelog workflow `34657703986` succeeded. Runtime/source SHA-256: `141F38DE17C5B6FFE560FC21A2DA4335A384D078BE7CD3EDE503BC3C77BB8235` / `D15BB5D9A2966A3FA2513495A2DE9DB335C02DA4595901F5B0256CF37D2A8069`.

Version 0.1.0.50 is published from Nexus source `4e34de0553a42bae66aef791ac19b53580c9a59f` alongside VieriAutoDuty 1.0.0.443 source `e1318fcf3cc950cf928a5a95058cb806a27f76a4`. Daily Pilcrow release commit `ee7de4c2d8a2f7c0e7d10d0ccc31e62d64f1594e` and documentation commit `6150f97036b8593aa496579e36bb08ab9335f63c` are pushed; final production deployment `dpl_9xMynD1dziAspvc3UhXPSWM7sBFS` is Ready. All 276 Nexus tests, all 342 AutoDuty tests, the zero-warning Nexus Release build, AutoDuty's unchanged 32-warning/zero-error upstream baseline, 205 website tests, typecheck, focused validation for both packages, the 13-entry inventory guard, production build, and final live HTTP/ZIP/hash validation pass. Discord changelog workflow `34691760398` succeeded. Nexus runtime/source SHA-256: `592C73F44CF0C93FD0110A0B6D396056EDCCE5205C4D834FE8E6869AFB52985D` / `867ED1E61A1872E60A783BC11F92E72620BADDEA092EEADCC3D116069DB2DC83`. AutoDuty runtime/source SHA-256: `498F5B5D5877CAD3E93DDBD97DC5554A3ED6A85617F95072D9164F77A55774AA` / `82D309963CA6038191E4E412E9F8370643DE3FC50265D8AED436823A66950C74D`.

Version 0.1.0.51 is published from Nexus source `782d3e010939763916a2c3d51601f8215b8bc165` alongside VieriCodex 1.12.2.81 source `1bc749b1e6f04519608b04caaca21c882e28412f`, incorporating Questionable 15.756.2.4 tag commit `f1b6da5ee5a9509c2f955f1fa93e73d5bfc4ffb8`. Daily Pilcrow release commit `4d0ae2ead3512860d411090991a877c80f57f72f` and documentation commit `29da88e632f2387d7cb528aa0475c9a541b2dea6` are pushed; final production deployment `dpl_GVyecd4sWizVxJZ5TNM973NzVECY` is Ready. All 280 Nexus tests, the zero-warning Nexus Release build, all 13,257 VieriCodex solution tests, VieriCodex's six-warning/zero-error plugin baseline, 205 website tests, typecheck, focused validation for both packages, the 13-entry inventory guard, production build, and final live HTTP/ZIP/hash validation pass. Discord changelog workflow `34692667685` succeeded. Nexus runtime/source SHA-256: `3C641A35A2959EF6D07D0ED976C811C90BBF59E383C0526BF9F59FEBAFB5D632` / `C1DEF5E5E059B68EB53774757BF11FB9547C2763A899F206F2CE164429A55CFC`. VieriCodex runtime/source SHA-256: `47EA6DB77AD707039788A69CC5BE2D4CAB27080B5717B584A34BDE9B9ED008F8` / `5AD9D66AC36D7A5E288C37676425DA360E606B94818E4596B4E64B46E8A7BCDE`.

Version 0.1.0.52 is published from Nexus source `bea458dc16ed3c82ef5bc30e02f770f0d93bf10c` (implementation `f6204d2f51b55de2102326134ed04d6491ee8c09`) alongside VieriCodex 1.12.2.82 source `173d6ad599d2c057e0f88cea76ed302a7746bf32`, incorporating Questionable 15.756.2.5 tag commit `e21fec6934db687829b9530394a709a5c1eb1d52`. Nexus owns the stock-Questionable solo-duty Wrath/Boss Mod handoff; the remaining five semantic route fixes are isolated at `b9bbe51d2`. Daily Pilcrow release commit `944b1b8dca971916c99896d9c0a8c305002b10d4` and documentation commit `b2b22964c3a81f023b88cc3daa789ea633596c76` are pushed; final production deployment `dpl_AqW8ATvo5N5UR7A4qe4gU7qoQEof` is Ready. All 285 Nexus tests, the zero-warning Nexus Release build, all 13,257 VieriCodex solution tests, VieriCodex's six-warning/zero-error plugin baseline, 205 website tests, typecheck, focused package validation, the 13-entry inventory guard, production build, and final public HTTP/ZIP/hash checks pass. Discord changelog workflow `34700595178` succeeded. Nexus runtime/source SHA-256: `FDF11ABB9287A25188B4D1318D01790CDD1F79EAC9AA37579E74B840FD3F47C0` / `05F63D4B11A06F85A9F94466A4E50E6EA606AB578D1AA4832F106B917A2D02B2`. VieriCodex runtime/source SHA-256: `FE95A9D8888BCDC7C5D11EA1ECDA318527BE53794A50D4D2DCBE699E613EEB5F` / `C35460CB187C9B4C91DD310F349961E1053C3DF35DB259424A42B624995A624A`.

Version 0.1.0.53 is published from Nexus source `3388bbe1a223a6cf9162c01e774d118cb8a47635` through Daily Pilcrow release `00fb484fb8aad1819cc1e80b28b4f8e367e68c6c`. It completes the Nexus-owned Command Center and complete transactional VieriDeck handoff, including fresh initialization, editable atomic state, plugin/command discovery, safe window toggling, preferred/custom commands, and editable hotkeys. All 291 Nexus tests, the zero-warning Nexus Release build, all 205 website tests, typecheck, focused package validation, the 13-entry inventory guard, production build, and public HTTP/ZIP/hash checks pass. Production deployment `dpl_2mBmnLRSzwvrmwdTKQSrdjWHofx2` is Ready and Discord changelog workflow `34705251020` succeeded. Runtime/source SHA-256: `A9942751CD4E5649FEF43735FD855A1FAEAD4F217AC2BD1DA8437A0C281B2FC6` / `7932E7BA0E0E918A5DDD9390CFF096973A28D20414303B9C1206236643CAEBAB`. The independent stock AutoDuty recovery branch `nexus-recovery-fixes-0.0.0.335` is pushed at `2c583870c216573a054af109d11fafdd85d556be`, passes 5/5 focused tests and a stock Release build, and remains outside Nexus pending an upstream/stock path.

Version 0.1.0.54 is published from Nexus source `94830490db7213081af3d5513ef807a7ef6f41a1` through Daily Pilcrow release `0d120b563d43dcc31805de15dbda7e5255b008ff`. It corrects the `Int32` versus `UInt16` enum-validation exception observed immediately after the otherwise successful Command Center import; existing stored numeric JSON remains compatible. All 292 Nexus tests, the zero-warning Nexus Release build, all 205 website tests, typecheck, focused package validation, the 13-entry inventory guard, production build, and public HTTP/ZIP/hash checks pass. The first unaliased host deployment stalled before starting its build and was removed without affecting production; replacement deployment `dpl_L6ywyfWVGgfJ8oHk38kpc8eGiwww` is Ready. Discord changelog workflow `34711770825` succeeded. Runtime/source SHA-256: `0FE25740E1DD8D36E3244BE94F3B6AD621A1E8038F3B1DD60CC5E28CF6D2C744` / `B92002FB0F2B8247B31125385E3267914E8B2A0C7AE8065548508A0D26991EA3`.

Version 0.1.0.55 is published from Nexus source `73fc2352fb0ba755d6bbd89ac3d526548fa21513` through Daily Pilcrow release `9b24d36893f65ef2288b18666a8c818a69d37f51`. It renames and rebuilds the VieriDeck replacement as Plugins, adds first-class Dalamud Plugins and Dalamud Settings actions, groups favorites before the remaining plugin catalog, and expands commands inline beneath the clicked plugin. All 294 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, the 13-entry inventory guard, production build, and public HTTP/ZIP/hash checks pass. Git production deployment `dpl_ASqEmfm6AoqAT8NPVQgVVtuPUEkq` is Ready and Discord workflow `34714293040` succeeded; two stalled manual host deployments were removed without affecting production. Runtime/source SHA-256: `F6B6BE569868219BD2EB8186CDF721B3F1332432DD164DE3C743423FDF093072` / `7C98AAF475C8BAA9F39AFD0CF612E14782F390DB12CED90AA28558F837E9D3C7`.

Version 0.1.0.56 is published from Nexus source `4df85a6469e2fd78be727c575a5d792f9dea06c5` through Daily Pilcrow release `fd5071e2300bd013ccaa2db0645efc02c1625d3c`. It completes the source-to-source VieriDeck parity audit, adds the yellow favorite state, Nexus overlay/settings shortcuts, plugin action menu, right-click command copy, and hotkey warning, and records the deliberate one-scrollbar layout replacements. All 294 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, the 13-entry inventory guard, production build, and public HTTP/ZIP/hash checks pass. Production deployment `dpl_8Mh8jnpFc2tJHW5KsGAvUsMSPuh5` is Ready and Discord workflow `34715344049` succeeded. Runtime/source SHA-256: `65DDC7578A19B701B26256E91DA04CA74DFAD935804F9DD4D6D5A3927C995EFE` / `B833A4AF45449D7427AB0FB07022B764BBC523B4C54722F80BF6ACB60F300496`.

Version 0.1.0.57 is published from Nexus source `a6b9cb9f32d5ac4404d16d6a5d0703cd1313ade0` through Daily Pilcrow release `e1d1da27887c6d976dd92d156ca361f75e54d0fa`; verification documentation commit `7a6d68b9fe9bb130930c14a396d3decc45a67d9f` is also pushed. Nexus now owns exact Main Scenario quest selection, durable identity, completion verification, and replanning through the stock-compatible one-quest contract. The transactional VieriCodex handoff preserves all activity and Progress Atlas preferences, level stop, full saved queue definitions, and queue behavior, converts only matching current-job intent into fresh Nexus desired state, and supports guarded rollback. All 304 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, the 13-entry inventory guard, production build, and public HTTP/ZIP/hash checks pass. Release deployment `dpl_DaQpTJxRQBN5uDLAP9UvtsiSqdfH` and final documentation deployment `dpl_5gJXvxqrAyzZDNriAdzXuFbqNCpp` are Ready; Discord workflow `34717702996` succeeded. Runtime/source SHA-256: `BBEDF2BCF95E90EA0A03685D4E684AC2BD068575195C0FE4918796AB623DD1FC` / `E72AF87B04FE0309D9133E82723CA17E850DC1DE8878005847CD0BA8B0C11D75`.

Version 0.1.0.58 is published from Nexus source `41b1f9c829cc995176708603fc536a572f3a9ff6` through Daily Pilcrow release `3841aa3b4619458097bb78f9bb42e2a06ee3db56`; verification documentation commit `249fc004c50fe30b11ec4f2c1c054da03c17ecf2` is also pushed. Nexus manages the five finite Vieri route corrections over stock Questionable's complete downloaded bundle with exact structural/value guards, a hash-addressed byte-identical backup, atomic replacement, safe-idle reload, automatic update rechecks, native-correction detection, affected-quest gating, and guarded rollback. The user explicitly chose no upstream submission. The production algorithm applied all five corrections to a disposable copy of the real installed bundle, recognized the managed result, and restored the original byte-for-byte. All 313 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, the 13-entry inventory guard, production build, and repeated public HTTP/ZIP/hash checks pass. Release deployment `dpl_4zc3G7HEAhtfmQcp87n2P9CUEuL2` and final documentation deployment `dpl_DtqtRuNqz6cbPhbBS6jnRpYwvVkk` are Ready; Discord workflow `34726454932` succeeded. Runtime/source SHA-256: `25D4C7AB67AD5FD2791C1754BA1D6FF26391CCD5601183FEBC703CA8B5346901` / `2BD330C4C52BA7337BEB546C1C0173D446B3F604A62934DD10D7F2DBCD8C48C2`.

The same release completes the first preserved custom-overlay slice: Inventory exposes only working native maintenance actions; Goto restores the expansion-grouped striking-dummy catalog and uses compatible Lifestream teleport plus Nexus-owned vnavmesh approach; the global Stop covers navigation, dummy travel, gear, maintenance, and Progression. Migration now leads with one **Set Up This Computer** action that imports detected NavPlotter and AutoDuty state, creates working copies, applies imported overlay preferences, and leaves predecessors/configuration untouched. This is the required path for every user—including another player: install Nexus first on that computer, prepare that computer's own settings, verify replacement readiness, then disable predecessors. Nexus never ships or copies one user's routes, content IDs, settings, or secrets to another.

The user also made the AutoDuty UI disposition explicit: the custom overlay is product functionality, not disposable fork decoration. Nexus must preserve the cleaner categorized Goto, Gear, Inventory, and Extras controls; striking-dummy destination menu; manual shopping review; and useful duty actions/status as Nexus-owned UI. The eventual stock AutoDuty provider does not replace this experience, and the fork cannot retire until those features are present and accepted in Nexus.

### Completed versus unfinished

**Completed foundation:** solution layering, shell/Home/dependency/setup UI, basic character/world readiness, neutral module descriptors, domain contracts, tested lease/verified-Stop/reload-watchdog/automatic-authority/recovery primitives, explicit route-Stop policy, live navigation provider health and bounded transition audit, historical isolated safety simulation, registered status/operations/command/dependency/navigation/activation/override-resolution IPC, the shared replay-safe character-scoped command/telemetry gateway, nine-source read-only discovery, exact source lock, transactional importers, immutable verified staging, separate atomic working-library stores, provider-neutral route planning, manual and timed route authoring, detailed point editing and safe route exchange, the immutable 27-route vendor catalog, exact-target one-winner assignment and current-target capture, static and ownership-filtered generated-path preview, Nexus-owned same/cross-zone/vendor/inn route composition over Lifestream and vnavmesh, immediate button-only Stop/restart, source-level provider migration audits, character-scoped Progression policy, stock/transition quest and duty contract checks, conflict rejection, the durable gear/exact-quest/one-duty lifecycle with exact completion/Stop-after/Stop/reload reconciliation/replanning, and complete Nexus-owned Gear & Inventory planning/execution with live catalogs, exact approvals, protected gil, native shop/purchase/equip/gearset/cleanup mechanics, resource ownership, explicit Stop, and verified completion gating.

**Partially complete:** world state, dependency health, module contract, broader IPC surface, configuration migration framework, ownership, solo-duty policy, navigation migration, Progression execution, and operations migration. Progression duties, native Gear & Inventory, safe/native and protected between-duty maintenance, narrow stock-provider turn-in/storage mechanics, install-first NavPlotter/AutoDuty migration, striking-dummy travel, the preserved custom overlay, and current-runtime control/status gateway are implemented. In-duty withdrawal, remaining predecessor importers, and final stock-provider retirement are not.

**Not implemented:** automatic replay of interrupted movement (intentionally forbidden); durable general audit/history beyond the character-scoped Progression task document; the other seven predecessor transactional importers; Communications secret adapter; general schema-driven multi-module scheduler/event bus; SQLite history; diagnostics/support export; legacy IPC aliases; migrated combat/market/communications/command-center/custom-UI runtimes; safe stock-provider in-duty leave/resume; standalone retirement.

There is no known external blocker. The old conversation's context window, not the repository, caused the handoff.

## 11. Known Bugs, Edge Cases, and Reliability Concerns

### Current Nexus implementation concerns

- **Partially verified live migration:** Dalamud installation/update, responsive Migration text, a zero-route import, window reopen, on-disk persistence, source/backup hash integrity, receipt/payload recovery, guarded rollback/re-import, correct Routes-page staging transitions, corrected layout, earlier safety/authority transitions, both isolated simulations, a non-empty personal route, route authoring/preview/travel/playback/Stop, destructive-action confirmation/cancellation, the one-scroll Routes layout, vendor-template assignment persistence, and 0.1.0.25 vendor plus Grand Company inn travel in both directions are confirmed. Read-only navigation IPC, timed-recording shutdown, clipboard compatibility, and general preference persistence remain unverified in game.
- **Current-runtime command boundary:** `VieriNexus.Commands.V1.Execute` is registered for the explicitly supported Nexus runtimes. It is not a legacy arbitrary-command bridge; consumers must use version 1, unique request IDs, current-character scoping for mutations, and the published canonical commands.
- **Partial knowledge model:** `KnowledgeState.Stale/Unavailable` exist but are never emitted by the current observer; provider state is always empty.
- **Readiness semantics:** `SessionSnapshot.IsLoading` currently mirrors the between-area flags rather than representing every loading/occupied state.
- **Route execution is verified in game:** Travel to Start, ordered playback, explicit button Stop, immediate restart, vendor travel, cross-zone travel, Grand Company inn entry, and return trips passed. Manual input intentionally no longer stops routes. VieriNavPlotter coexistence/staging and manual re-enable also passed.
- **Migration validation boundaries:** route validation rejects non-finite coordinates/tolerances and duplicate/empty IDs, but does not currently impose semantic ranges for interval, pane width, tolerances, binding kind, or territory/target combinations. Empty routes intentionally remain editable drafts.
- **Migration service cache:** source preview invalidates by path and last-write time. Extremely unusual same-timestamp external rewrites could leave a stale preview until reload/mtime change.
- **Control Center is partial:** it now shows the durable Progression goal status/title, but resource/activity detail and the other module runtimes remain incomplete.
- **Dependency health is shallow:** installed/loaded state is not the same as compatible version or healthy IPC. The new navigation diagnostics label vnavmesh as loaded/available but deliberately do not invoke Stop or movement-state IPC merely to probe health.
- **Remaining AutoDuty migration scope:** route composition, bounded duty orchestration, the complete Gear & Inventory subsystem, operations-state staging/working profiles, protected between-duty maintenance, striking-dummy travel, and the clean categorized overlay are Nexus-owned. VieriAutoDuty no longer participates in routes, gear, maintenance, turn-ins, storage, or dummy travel. Safe in-duty leave/resume, the remaining duty-engine audit, and final stock-provider coexistence/retirement remain governed by `docs/AUTODUTY_PROVIDER_MIGRATION_AUDIT.md`.

### Preserved reliability incidents/regressions

These were fixed in predecessor products and must remain test fixtures/guardrails during migration:

- Solo-duty targeting originally fought BossMod/Wrath, rapidly switched targets, and pulled/moved past mobs. Preserve the bounded fallback policy in section 4.7.
- `Sage's Focus` resumable steps 5, 6, and 8 must request flight when unlocked; a missing route flag caused a 536-yalm ground ride.
- Manual gear shopping must finish purchase, equip, verify/retry once, update gearset, and move displaced Armoury items to inventory before completion.
- Off-hands must be rejected whenever the effective main hand is two-handed, while legitimate one-handed weapon + shield sets remain supported. Plan/equip the main hand first.
- Vendor travel previously stopped too far away, slammed into counters, skipped authored points, fought the final inch, leaked live vnavmesh lines globally, detoured through nearby Aethernet, mounted for walking-only legs, or attempted hazardous same-zone cross-region runs. Preserve authored endpoint priority, native interaction verification, route ownership, and smart travel policies.
- VieriAutoMarket previously timed out on empty/duplicate/stale searches, self-undercut across retainers, failed to price-match owned listings, followed suspicious `1, 5, 10000` low-price clusters, and crashed FFXIV during owned-retainer confirmation. Preserve the final verified one-pass behavior.
- VieriLink previously created duplicate Discord status posts after any edit failure. Preserve edit retry/recovery and replacement-only-on-confirmed-deletion.
- Rotation suggestions previously repeated starter/caster actions or changed the full bar abruptly. Preserve stateful forward prediction across all jobs/modes and actual hotkey mapping. Random procs/cards/steps refresh from live state rather than being falsely predicted.
- Rotation/positional/Deck overlays previously appeared during loading. Preserve stable-world gating.
- Rotation suggestions previously drew above the world map/native windows. Preserve per-icon native occlusion.

### Known limitation inherited from the combat source

VieriRotationHelper's suggestion bars primarily cover damage rotations. Wrath auto-rotation can heal, cleanse, raise, shield, and mitigate, but a complete healing/utility suggestion sequence was explicitly not present in the recovered conversation. Do not document or market full healer suggestion parity until implemented and verified.

## 12. Technical Debt

- The current Plugin assembly combines composition, infrastructure, services, and all UI; planned `Infrastructure.Dalamud` and separate UI/module/provider assemblies do not exist.
- `INexusModule` exposes only a descriptor. The rich registration boundary is still prose.
- Goal/task records are inert; no repositories, handlers, planner, executor, policy engine, or lifecycle enforcement.
- `ResourceLeaseManager` is a live in-memory navigation ownership system and route execution acquires/heartbeats/releases Navigation/Movement leases. It is not yet a durable general ownership system; priority is stored but unused, and cross-module safe checkpoint/preemption remain absent.
- `SoloDutyCombatPolicy` duplicates preserved behavior as constants/tests but is not connected to a provider adapter or replay fixture.
- World state is minimal and now contains six navigation-specific provider/safety observations. Other domains remain empty; future modules must not add independent ad hoc scanners as a shortcut.
- Dependency catalog is centralized and static rather than contributed by modules/providers; compatibility/version/IPC health is absent.
- UI navigation and status strings are hardcoded; most pages are honest placeholders. No schema-driven goal builder or registered page/panel contributions.
- `Configuration.Version` is forced to 4 without a formal migration pipeline; added fields use backward-compatible defaults.
- IPC now exposes the current-runtime command and rich operations-status boundaries but still lacks a general handshake/capability negotiation layer, direct goal CRUD, combat/positional contracts, legacy aliases, and deprecation discovery.
- Migration storage remains JSON; `TransactionalMigrationStore` now shares its atomic backup/hash/receipt/rollback mechanics across Navigation and VieriAutoDuty operations snapshots, but additional source types still need explicit validated mappings.
- There is no durable general logging/audit/history/support-export implementation despite `IPluginLog` and `IChatGui` being injected. The bounded session-only navigation transition audit is deliberately narrower.
- No SQLite dependency/store, general event bus, framework-thread dispatcher abstraction, background-work boundary, replay harness, provider contract harness, or packaging test project.
- Source licenses/notices for eventual embedded upstream code are planned but not yet represented beyond repository/commit provenance.
- The repository contains ignored build outputs locally. They are not source and should not be documented or committed.

## 13. Development Conventions

### Code and dependency organization

- Keep Domain free of Dalamud, ImGui, game structures, file/network/database implementation, and provider internals.
- Keep Application dependent inward on Domain and focused on pure policy/orchestration/migration behavior.
- Put stable public DTOs/IPC names in Contracts. Version identifiers explicitly (`...V1`, `/v1`) and prefer immutable records.
- Treat Plugin as composition/runtime edge. As it grows, extract infrastructure/UI/modules/providers along the architecture plan rather than enlarging `Plugin.cs` or `NexusWindow.cs` indefinitely.
- Modules declare contributions through registration, capabilities, and contracts. They do not reach into another module's private classes or call UI.
- Use string-backed typed identifiers for extensible goal/task/capability/provider kinds; do not introduce one cross-project enum for all future domains.

### State, async, and safety

- Publish immutable revisioned snapshots; distinguish Known, Unknown, Stale, and Unavailable. Unknown never means false/zero/done.
- Marshal authoritative state transitions to the Dalamud framework thread. Keep per-frame paths allocation-light and free from network/database/reflection/heavy LINQ.
- Planning is pure; execution begins only after immutable request preparation and atomic resource acquisition.
- Use bounded retry plus elapsed-time budgets, explicit failure taxonomy, postcondition verification, and safe checkpoints.
- User-control semantics are explicit per domain. Routes are button/command-only Stop and must not cancel on movement input. Never “solve” contention by two controllers repeatedly reasserting state.
- File/config migrations are previewed, backed up, staged, validated, atomically written, receipted, hash-guarded, and rollbackable.
- Never log secrets. Redact before audit/support output.

### Naming and UI

- User-facing modules use neutral domain names. Historical Vieri/upstream names stay in migration/provenance/compatibility surfaces only.
- Keep plain-language descriptions: what, why, provider, held resources, next step, blocker.
- Reuse `NexusTheme` status colors/raised panels and the Home branding rather than inventing inconsistent visual systems.
- Keep module pages scalable and contextual; do not add a top-level page merely because a new class exists.
- In-world overlays remain independently movable/configurable where appropriate, but share visibility/occlusion and world-readiness policy.

### Error handling, logging, and tests

- Catch only failures that can be handled safely, return user-safe messages, preserve technical detail for redacted diagnostics, and avoid silent fallback on destructive/transient operations.
- Tests should remain pure where possible. Add tests for every regression requirement before activating a migrated runtime.
- Expected suites include unit tests, provider contracts, sanitized incident replays, config migrations/golden fixtures, character isolation, IPC compatibility, in-game matrices, and clean-package validation.
- Warnings are errors and builds are deterministic. Do not relax these settings to hide a problem.
- Significant predecessor updates are committed/pushed/published/verified and then pinned into Nexus with behavioral notes/tests.

## 14. Regression Guardrails

Future threads must not casually remove, rename, redesign, duplicate, bypass, or weaken the following:

1. **The modular-monolith boundary.** One package does not justify a giant controller/project. Keep dependencies inward and combat hot paths isolated.
2. **The final one-product goal.** Do not turn migration adapters or predecessor plugins into the permanent installed topology.
3. **Neutral user-facing names.** Do not reintroduce predecessor branding as modules/pages.
4. **Standalone authority during migration.** Never auto-disable/uninstall a source, activate duplicate hooks, or run Nexus behavior before explicit parity/activation gates.
5. **Configuration preservation.** Do not rewrite source configs, silently default unknown settings, or import without backup/receipt/rollback.
6. **Credential boundary.** Do not open or deserialize VieriLink config through generic discovery. Never log/copy Discord tokens, channel/message IDs, or command cursors without the dedicated tested adapter.
7. **Per-character isolation.** Use content ID + world; never character name or another user's settings.
8. **Home/UI decisions.** Keep the logo on Home, not a popup; keep the rejected redundant header removed; retain the polished dark/red/gold system and stable-world gate.
9. **Dependency correction.** Do not list Questionable or migration-source Vieri plugins as external requirements. Recommended dependencies must not block setup.
10. **Atomic ownership and explicit user control.** No provider acts without all required leases; no partial acquisition; no unsafe force-preemption. Routes stop only through their explicit Stop control; other modules must define and test their own user-intervention semantics.
11. **Desired-state recovery.** Resume goals by re-observing/replanning, not by replaying unsafe transient steps. Unknown never equals success.
12. **Legacy compatibility.** Necessary Wrath/Switch/AutoDuty/Codex/Avarice aliases must route to one underlying service, never a second engine.
13. **Carry-forward rule.** A predecessor fix is incomplete for Nexus until the exact source commit and behavior/regression requirement are pinned.
14. **No unrelated path changes.** Vendor-specific route fixes must not change stock dungeon paths, inn/Goto behavior, Lifestream-owned visualization, or unrelated vnavmesh users.
15. **Native UI occlusion.** Suggestion icons must remain behind maps/inventory/action bars/native windows without changing rotation state, forecasts, keys, or saved layout.

### Navigation and vendor invariants

- Preserve complete personal route fields and disabled overrides exactly.
- Complete routes follow saved points in order. Destination-only entries must remain labeled as generated navmesh approaches, not authored complete paths.
- Show Route, Travel to Start/Destination, Play Route, and Stop Playback are required for built-in and personal route parity.
- Live waypoint chains are visible only during NavPlotter-owned play/travel or explicitly authorized gear-shopping travel. Existing external visualization remains untouched.
- Cross-zone work uses the suite travel owner so teleport/Aethernet/flight/navmesh/stall recovery do not compete.
- Authored standing points finish before interaction eligibility can end travel. Raw NPC-coordinate routes may stop early on native interaction. When the NPC object is unavailable, the conservative final fallback is 3.1 yalms.
- Long same-territory cross-region travel prefers an unlocked destination aetheryte. Adjacent legs under 100 yalms walk directly and must not mount just because flight is unlocked. Overworld long post-teleport approaches retain flight permission.
- The retired Old Sharlayan stair state machine must not return.

Measured vendor standing points that are mandatory migration data:

| Area/vendor | Territory | X | Y | Z |
| --- | ---: | ---: | ---: | ---: |
| Domitien approach point 1 | 133 | 164.4264 | 15.5000 | -75.7035 |
| Domitien final | 133 | 157.5930 | 15.7000 | -69.3316 |
| Geraint final | 133 | 168.4092 | 15.6999 | -73.9508 |
| Iron Thunder | 129 | -155.3658 | 18.2000 | 23.3950 |
| Faezghim | 129 | -236.5439 | 16.2000 | 40.3006 |
| Sorcha | 129 | -135.1727 | 18.2000 | 14.8682 |
| Seghuie | 419 | -189.1842 | -12.6349 | -40.0551 |
| Elbert | 419 | -216.0509 | -16.1262 | -60.4229 |
| Norlaise | 419 | -205.2957 | -16.1349 | -51.2569 |
| Kugane accessories | 628 | 29.9279 | 4.0000 | 52.4925 |
| Kugane weapons | 628 | 35.2371 | 4.0000 | 52.5185 |
| Kugane armor | 628 | 40.1606 | 4.0000 | 52.5056 |
| Level 64 accessories | 614 | -283.8307 | 17.3200 | 492.3687 |
| Level 66 accessories | 614 | 171.1704 | 5.1697 | -421.6375 |
| Level 68 accessories | 620 | -249.5169 | 257.5265 | 750.1727 |
| Crystarium accessories | 819 | -120.8424 | -1.0766 | 126.7847 |
| Crystarium gear 1 | 819 | -129.5804 | -1.0767 | 112.0974 |
| Crystarium gear 2 | 819 | -122.9644 | -1.0765 | 99.1908 |
| Old Sharlayan direct vendor | 962 | 43.2774 | 5.1500 | -74.5438 |
| Level 82 vendor | 958 | -425.7329 | 22.4297 | 450.5089 |
| Level 84 vendor | 959 | -21.4712 | -132.9464 | -462.4854 |
| Level 86 vendor | 961 | 140.9911 | 10.4610 | 163.3107 |
| Level 88 vendor | 960 | 468.3042 | 437.0017 | 327.8175 |
| Level 90 vendor | 1185 | -30.9625 | -10.0000 | 82.3698 |
| Level 92 vendor | 1188 | -450.8014 | 121.6334 | 276.1090 |
| Level 94 vendor | 1189 | 627.0523 | -137.1266 | 514.2490 |
| Level 96 vendor | 1190 | -285.4235 | 18.9721 | -96.3331 |
| Level 98 vendor | 1191 | -210.7973 | 31.0000 | 129.5844 |

NPC object coordinates remain separate lookup metadata and must not replace authored movement endpoints.

### Gear, market, combat, and communications invariants

- Gear shopping honors selected preview and gil reserve, purchases selected upgrades, plans/equips main hand first, rejects off-hand for a two-handed effective main hand, equips/verifies with one idempotent retry, updates the gearset, and moves every displaced item from Armoury to inventory before lease release. Keep EXP-item and sell/desynth protections.
- Exact owned-retainer price matching waits for the comparison window to close/settle, writes through the native price control, sends the complete `AddonRetainerSell` confirmation event, and verifies the saved price. Suspicious low clusters such as `1, 5, 10000` use 10000; if no safe reference exists, leave unchanged.
- Solo-duty rotation fallback may never rewrite the hard target or own encounter movement and must yield as soon as normal targeting resumes.
- Combat suggestions preserve all-job forward state, simple/advanced/manual behavior, actual bindings, positionals, and Hilda-style visual flow. Do not claim full healing-sequence support.
- VieriLink preserves the same permanent status post wherever possible and protects all tokens/channel/message IDs. Remote commands pass through the same local command/safety/ownership system.

## 15. Roadmap

### Near-Term

Firm next gates, in priority order:

1. **ACCEPTED 0.1.0.25:** Faezghim and Grand Company inn routes passed in both directions without false cancellation. Do not repeat the Routes acceptance suite without a specific regression signal.
2. **IMPLEMENTED IN 0.1.0.59 — native multi-job queue and stock quest retirement:** Nexus executes the complete migrated queue definition across Fast Job Switcher-confirmed job changes, preserves character-scoped progress and behavior settings, and uses stock Questionable as its only quest runtime. VieriCodex remains only as a guarded local import source. The next substantial provider-retirement slice is stock AutoDuty parity/recovery and removal of VieriAutoDuty from runtime selection.
3. **AUTODUTY DECOUPLING:** Routes, Gear & Inventory, Progression scheduling/Last Run, protected between-duty maintenance, narrow turn-in/storage provider orchestration, striking-dummy travel, operations settings, the compact overlay, and current-runtime telemetry/commands are now Nexus-owned. Finish safe in-duty leave/resume and the remaining duty-engine audit while keeping the stock duty engine external and updateable.
4. **FOUNDATION AS NEEDED BY REAL MODULES:** expand world/provider observations, framework-thread sequencing, commands, events, durable state, and watchdogs only where the next executable vertical slice requires them; do not return to synthetic navigation gate-by-gate releases.
5. **PLANNED:** generalize the transactional importer/store carefully and implement remaining source importers one bounded module at a time with complete field inventory, golden fixtures, behavior/IPC parity, and rollback.
6. **DEFERRED UNTIL SECURITY TESTS:** Communications/VieriLink importer only after same-account encrypted round-trip and secret-redaction tests.

The original Phase 0/1 foundation checklist is only partly complete. Do not jump straight from the shell to mass source absorption.

### Medium-Term

- Continue one real end-to-end `Reach Job Level` goal while exercising the same narrow contracts against stock Questionable and stock AutoDuty. Exact Class/Job/Role quests now participate as bounded tasks. The remaining acceptance scope is ordinary quest use plus future Hunting Log/general-side-quest selection, not another artificial replay of already accepted Routes, Gear, or duty behavior.
- Combat consolidation: migrate VieriRotationHelper without rewriting Wrath; preserve aliases; unify Avarice on the same forecast; retire both only after parity.
- Progression/questing: split Codex capabilities into module boundaries, keep quest data isolated, import Progression Queue as goals, preserve all route fixes, then retire Codex.
- Duties/gear/inventory: preserve stock paths and BossMod behavior, expose shared gear/inventory services, migrate maintenance and Last Run, then retire AutoDuty.
- Communications and Command Center: move VieriLink through events/command gateway and migrate Deck concepts/settings after secret and command security are proven.
- Custom UI/Overlay: migrate DelvUI-derived behavior under neutral names, marker priority, loading gates, edit/layout settings, and shared native occlusion; ensure disabled elements are cheap.
- Market: migrate last because native UI/retainer work is fragile; require strict checkpoints, leases, pricing regression tests, and saved-price verification.

All of these remain planned; no standalone product may be retired until configuration, behavior, IPC, release, and rollback parity is demonstrated.

### Long-Term / Product Vision

- VieriNexus becomes the only Vieri installation.
- Goals can compose progression, duties, quests, gear, inventory, market, travel, combat, communications, and UI while retaining explicit user control.
- Add domain modules for crafting, gathering, farming, dailies/weeklies, procurement/production chains, retainers/ventures, currencies, collections, reputations, hunts/events, relics, scheduling, and future content.
- New domains register goal types, capabilities, providers/executors, observers, settings, and UI contributions without central-controller branching.
- A future natural-language frontend may translate requests into structured goals; it is not required for the engine.
- Establish performance budgets, sanitized replay suites, clean-machine one-package validation, and a signed/controlled strategy for large quest/content data updates.

## 16. Recommended Next Steps

Do not execute these as part of recovery. The next normal development thread should:

1. Treat the Nexus-owned Routes provider and the 0.1.0.31 bounded Progression duty lifecycle as accepted. Do not repeat their focused tests unless a concrete regression touches them.
2. Treat the complete 0.1.0.32 gear-readiness transaction as accepted: Nexus bought gear, returned to the inn, and started the planned duty automatically. Do not repeat this gate unless a concrete regression touches shopping, equipment, travel, or Progression handoff.
3. Treat the 0.1.0.33 Nexus Shop for Upgrades UI, exact single-use approval, spending floor, full resource lease, and Stop as accepted by the valid live zero-upgrade MCH result. Do not rerun the accepted automatic Progression transaction.
4. Treat the repeated zero-upgrade MCH result as accepted. Do not ask for another lookup-only check.
5. Version 0.1.0.36 completes native physical shop/purchase/equip/gearset/displaced-item ownership. Its only Gear acceptance case is one future real upgrade transaction, preferably as part of the normal Progression handoff; do not split it into more micro-tests.
6. Version 0.1.0.37 preserves the complete VieriAutoDuty profile/operations state and lands the clean categorized Nexus overlay over working controls. Do not repeat this as many per-setting migrations.
7. Treat 0.1.0.38's operations working profiles, safe native maintenance, striking-dummy travel, and install-first migration flow as one completed implementation slice. Treat 0.1.0.39's exact Class/Job/Role quest lifecycle as another completed implementation slice. Validate both through ordinary use; do not split them back into setting-by-setting development gates.
8. Treat 0.1.0.46's quest-earned Aether Current action and duty-only Grand Company target dispatch as complete ownership slices after live publication and focused verification. Treat 0.1.0.47-0.1.0.48's protected between-duty item transactions plus their full prerequisite trips as one implementation slice; validate through ordinary use rather than separate micro-tests. In-duty withdrawal remains blocked on a real stock-provider leave/resume contract, not more local policy work.
9. Treat 0.1.0.49's overlay/chat/IPC control and telemetry gateway as the completed VieriAutoDuty command/status ownership slice. Version 0.1.0.50 completes the final-tree inventory and shared-identity handoff guard: stock is selected automatically when it is the sole loaded implementation, duplicates fail closed, and current upstream's home-world correction is integrated. The only isolated generic engine delta is stale-path/death/re-entry recovery; prove current stock equivalent or contribute that behavior upstream before retiring the fork. Do not duplicate the dungeon engine in Nexus or invent leave/pause semantics absent from stock.
10. Treat 0.1.0.51 as the equivalent Questionable identity handoff boundary, 0.1.0.52 as the Nexus-owned stock solo-duty rotation boundary, and 0.1.0.58 as the guarded five-route compatibility boundary. VieriCodex and stock Questionable may never both delegate work; stock is selected automatically after the fork is disabled. Nexus layers only the five exact corrections recorded in `docs/QUESTIONABLE_PROVIDER_MIGRATION_AUDIT.md`, rechecks them automatically after stock data updates, and does not submit them upstream. Future compatible Questionable updates should require a contract/content audit, not a manual fork merge or ordinary Nexus source change.
11. Treat 0.1.0.53 as the complete VieriDeck-to-Command-Center implementation slice. Validate import, favorites/hidden state, plugin open/toggle/settings, one discovered and one custom command, hotkey replacement, reload persistence, and guarded rollback through ordinary use rather than splitting them into more development slices. Only after that acceptance should VieriDeck be disabled and deliberately retired from the package/feed; Nexus never disables it automatically.
12. For every substantial change, update this file, `IMPLEMENTATION_STATUS.md`, migration/upstream policy, source lock, tests, package metadata, and release documentation consistently.

## 17. New Codex Thread Startup Procedure

Every future development task must follow this procedure:

1. Read `PROJECT_STATE.md` completely before making changes.
2. Read all applicable `AGENTS.md` files. None existed in this repository at recovery, but check again because that can change.
3. Inspect the relevant current implementation before modifying anything.
4. Check `git status` and preserve unrelated/user changes.
5. Review relevant recent Git history/diffs when needed, especially the latest migration/source-pin commits.
6. Treat the repository as the ultimate source of truth for current implementation.
7. Treat `PROJECT_STATE.md` as persistent context for product intent, architectural decisions, historical constraints, regression guardrails, and roadmap.
8. If repository behavior and `PROJECT_STATE.md` disagree, investigate the code, history, source lock, and predecessor source before changing anything. Update this file when the discrepancy is resolved.
9. Preserve working behavior unless the requested task explicitly requires altering it.
10. Do not redesign an existing subsystem merely because a fresh thread would personally implement it differently.
11. Do not mistake `MASTER_ARCHITECTURE_PLAN.md`, domain records, tests, module cards, or source pins for live implemented automation.
12. Keep standalone plugins authoritative until the relevant Nexus module has explicit configuration, behavior, IPC, conflict, ownership, recovery, in-game, and rollback parity.
13. Do not open VieriLink configuration during generic inspection. Protected values require the dedicated security-tested migration path.
14. When touching a migrated behavior, inspect the exact pinned sibling source and relevant regression rules; do not reconstruct behavior from memory.
15. When an existing Vieri product changes, pin its exact commit and carry the behavioral requirement/test into Nexus before considering the consolidation update complete.
16. Run validation proportional to risk, including pure tests, clean build/package, targeted in-game validation, and publication checks where authorized. Do not claim live validation that did not occur.
17. Update `PROJECT_STATE.md` whenever a substantial feature, architectural decision, dependency, migration source/version, roadmap item, known incident, project state, or release/distribution process changes.
18. Treat section 18 (Release, Hosting, and Dalamud Distribution Infrastructure) as mandatory production knowledge before any publish/deploy/version/package task.
19. Before a release, inspect the existing working release infrastructure and live/public feed rather than inventing paths, filenames, JSON fields, commands, or Discord-bot behavior.
20. Never expose secrets in this file. Tokens, webhook URLs, deployment credentials, private keys, cookies, or personal access tokens must remain in their existing secure mechanism; document only the safe lookup/usage path.
21. A normal published release must leave `PROJECT_STATE.md` synchronized with the released version/commit, meaningful implementation status, source pins, known issues, and next workstream.
22. Do not mark a release complete until Git/source, packages, public hosting/feed, Dalamud install/update behavior, validation/hashes, and release Discord-bot/changelog delivery have been individually verified or explicitly reported as not verified.
23. If the release pipeline changes, update this file and any dedicated release documentation in the same task.
24. If a task is local-only, do not publish/deploy/announce; still update durable project state when the task materially changes the project.

## 18. Release, Hosting, and Dalamud Distribution Infrastructure

This section is **durable production operating state**. Future Codex threads must preserve and maintain it. It exists so release knowledge does not live only in chat history.

### 18.1 Production identities and authorities

- **Plugin/product:** `VieriNexus`.
- **Nexus Git repository:** `https://github.com/iampilcrow/VieriNexus.git`.
- **Normal branch at recovery:** `main`.
- **Distribution domain:** `https://www.thedailypilcrow.com`.
- **Authoritative custom Dalamud repository URL configured by users:** `https://www.thedailypilcrow.com/dalamud/pluginmaster.json`.
- **Current production release:** `0.1.0.39`, source `1b2cbc100b933ee41848ae5a247515b10a846cd9`.
- **Current project version source verified in repository:** `src/VieriNexus.Plugin/VieriNexus.Plugin.csproj` contains `<Version>0.1.0.39</Version>` and uses `Dalamud.NET.Sdk/15.0.0` at this snapshot.
- **Plugin manifest:** `src/VieriNexus.Plugin/VieriNexus.json`; its internal name/API compatibility must remain synchronized with the runtime package/feed requirements.

The live `pluginmaster.json` and the source/deployment mechanism that produces it are production infrastructure. Do not treat the feed as disposable generated output unless the existing release implementation proves that it is safely generated from an authoritative source.

### 18.2 Release success definition

A release is **not complete** merely because:

- code compiles;
- tests pass;
- a ZIP exists;
- a commit exists;
- GitHub was pushed;
- local Dalamud can load a development build.

For a normal user-facing published release, completion requires the intended applicable subset of all of the following to be successful and verified:

1. intended code/docs/config changes are complete;
2. regression and risk-appropriate tests pass;
3. project version metadata is synchronized;
4. release build/package succeeds;
5. runtime Dalamud archive is created and validated;
6. source archive is created/validated when the established Vieri process requires it;
7. hashes are calculated/validated according to the established process;
8. `PROJECT_STATE.md` and other durable documentation reflect the release state;
9. source changes are committed to the intended branch;
10. commit is pushed to the intended Git remote;
11. runtime/source artifacts are published to the established Daily Pilcrow hosting location;
12. the custom repository/feed entry is updated without damaging other entries;
13. the Daily Pilcrow site/feed deployment is complete;
14. public artifact URLs resolve successfully;
15. the live `pluginmaster.json` resolves and contains the expected Nexus version/metadata/download target;
16. the feed's runtime download target returns the intended package rather than an older/stale file;
17. Dalamud sees the intended version and can install/update it through the custom repository;
18. produced/live artifacts correspond to the intended source commit/version;
19. release notes/changelog accurately summarize user-visible changes;
20. the existing release Discord bot sends/posts the release update with the new changes;
21. the Discord update is verified as successfully delivered through the established mechanism;
22. final release status is reported honestly, separating what was verified from what was not.

If any required production stage fails, do not claim the release is complete.

### 18.3 Custom Dalamud feed contract

The user-facing custom repository URL is permanently:

`https://www.thedailypilcrow.com/dalamud/pluginmaster.json`

Future release tasks must inspect the current live feed and its authoritative source before editing it. Preserve the feed's current schema and conventions rather than assuming a generic third-party-repository shape.

For the VieriNexus entry, the release process must verify all fields currently used by the live feed that affect installation/update behavior. This commonly includes concepts such as product/internal name, author, version, Dalamud API/applicability, description/punchline/changelog, install/update/testing download URLs, repository/source URL, and other metadata, but **the actual live/source feed is authoritative**. Do not create, rename, or remove fields based only on this illustrative list.

Feed safeguards:

- Parse/validate JSON before deployment.
- Preserve all unrelated plugin entries.
- Preserve unrelated plugin versions and URLs exactly unless the task explicitly updates them.
- Do not remove predecessor Vieri entries merely because Nexus is the eventual replacement.
- Ensure Nexus `InternalName`/assembly identity and feed identity match what Dalamud expects.
- Ensure the public runtime download URL referenced by the feed is reachable without local authentication.
- Avoid stale-cache mistakes: verify the live response after deployment and compare the expected version/artifact.
- If the current infrastructure uses separate install/update/testing links, preserve their established semantics.
- If the current infrastructure derives feed data from manifests/scripts, update the authoritative source rather than hand-editing only the generated output.
- Do not deploy a feed entry for an artifact that has not been uploaded and verified.

### 18.4 Build and runtime package contract

The repository currently uses `Dalamud.NET.Sdk/15.0.0`; the established SDK/packager behavior should remain the starting point for packaging unless a deliberate upgrade is required.

Before each release, Codex must determine the exact current build/package command and output path from the repository and working release scripts. Do not hard-code an old local path if the tooling has moved.

The runtime archive must be checked for:

- the correct `VieriNexus.dll`/entry assembly;
- the correct plugin manifest expected by the established packaging flow;
- required Nexus-owned assemblies and runtime dependencies;
- required assets, including `VieriNexusLogo.png` when the current build/package requires it;
- correct version metadata;
- absence of development-only junk, credentials, user configuration, logs, secrets, unrelated source trees, and unintended files;
- an archive structure compatible with Dalamud's custom-repository installation/update behavior.

Do not assume `dotnet build` output is identical to the final distributable ZIP. Use the package generated by the established Dalamud packaging process or the established Vieri release script.

### 18.5 Source archive contract

Recovered Vieri release behavior includes a source archive in addition to the runtime archive. Preserve that expectation unless the user explicitly changes the release policy.

The source archive should represent the intended release source and must not contain:

- local build output unless intentionally part of the established source package;
- `.git` internals;
- IDE/user-state folders;
- secrets;
- local Dalamud configuration;
- Discord credentials;
- website deployment credentials;
- user-specific data.

The exact archive naming convention, hosted path, and generation command must be discovered from the existing working release infrastructure and then recorded below under **Verified local release implementation** once confirmed.

### 18.6 Version synchronization

Before a release, inspect every location in the current code/release infrastructure that represents the plugin version. The currently verified Nexus source contains version `0.1.0.31` in:

`src/VieriNexus.Plugin/VieriNexus.Plugin.csproj`

Do not assume this is the only relevant version location. Also inspect, where applicable:

- `VieriNexus.json`;
- generated package manifest metadata;
- `pluginmaster.json` source/live entry;
- runtime/source archive names;
- website metadata;
- changelog/release copy;
- Discord release message;
- any release script variables.

A release must not publish mismatched versions. Determine the authoritative version source in the actual current tooling and update derived locations consistently.

### 18.7 Git publication contract

Normal Nexus source publication uses:

`https://github.com/iampilcrow/VieriNexus.git`

At the recovery snapshot the branch is `main`.

For normal release work:

1. inspect `git status` before changes;
2. preserve unrelated/user changes;
3. review relevant diffs;
4. do not use destructive reset/clean/checkout operations to hide unrelated work;
5. run appropriate tests/build/package validation;
6. ensure `PROJECT_STATE.md` and affected durable docs are updated;
7. commit the coherent release state with a meaningful message;
8. push to the intended remote/branch;
9. verify the remote commit/branch contains the intended commit;
10. record/preserve any predecessor source-lock changes required by migration policy.

Do not claim “published to Git” if the commit exists only locally.

### 18.8 Daily Pilcrow website/hosting contract

The Daily Pilcrow domain is part of production Vieri distribution because it serves the custom repository URL and hosted release artifacts.

Before the first release in any fresh environment/thread, locate the actual website/distribution project and determine, from current files/history/scripts/configuration:

- the repository/local workspace that owns the Daily Pilcrow distribution files;
- the source path for `/dalamud/pluginmaster.json`;
- the source/public paths for runtime ZIPs;
- the source/public paths for source archives;
- whether old artifacts remain hosted and how versioned filenames avoid accidental replacement;
- whether deployment is automatic from Git, performed by a script, performed through a hosting CLI, or uses another established mechanism;
- how to verify production deployment;
- whether a CDN/cache layer requires a cache-busting/versioned path or verification delay;
- how the live feed's download URLs map to hosted files.

**Do not guess these details.** If the active thread cannot find the website/distribution project, it must report that release infrastructure discovery is incomplete and stop before changing production.

When these details are positively identified, update the **Verified local release implementation** subsection below in the same task so future threads no longer need to rediscover them.

### 18.9 Artifact hashes and integrity

Recovered release history says runtime/source archives were validated with matching hashes. Preserve integrity validation.

The current route-migration code uses SHA-256 for migration receipts; this does not by itself prove that release archives use the same algorithm. For release artifacts, inspect the existing release process and record the actual hash algorithm/location before publishing.

Release integrity requirements:

- calculate hashes from the final artifacts that will actually be hosted;
- do not hash an intermediate ZIP and then modify/repack it;
- after upload/deployment, when practical compare the hosted artifact with the local release artifact by size/hash or equivalent strong verification;
- record/report hashes according to the established release process;
- never treat matching filenames as proof of matching contents.

### 18.10 Dalamud install/update verification

The purpose of the custom feed is not merely discovery; VieriNexus must remain installable and updateable through Dalamud.

After publishing a normal release, verify at minimum:

- `https://www.thedailypilcrow.com/dalamud/pluginmaster.json` is reachable;
- its JSON parses;
- the Nexus entry is present exactly once as intended;
- the advertised version is the release version;
- the entry's download/update target points at the intended public runtime package;
- the runtime URL is reachable publicly;
- the package is valid for the advertised Dalamud API level/current compatibility policy;
- Dalamud with the custom repository configured can discover Nexus;
- a clean install works where practical;
- an update from the previous published Nexus version works where practical;
- the installed plugin reports/loads the intended version;
- existing custom-repo users do not need to replace the repository URL for a routine Nexus update.

If only feed/HTTP validation is possible and in-game update validation was not performed, say so explicitly. Never report “Dalamud verified” when only local packaging was tested.

### 18.11 Release Discord-bot/changelog contract

The Vieri release process includes a Discord bot that sends/posts updates containing the new release changes. This **release-notification behavior is production release infrastructure** and is separate from the VieriLink gameplay/status/remote-command subsystem unless the actual implementation proves they share infrastructure.

For a normal published release:

- prepare a concise, accurate changelog from the actual released diff/work;
- include the correct plugin name/version and meaningful user-visible changes using the established existing message format;
- do not announce unreleased/local-only work as published;
- send/post through the existing authorized bot/release mechanism only after the public artifacts/feed are successfully deployed and verified, unless the established working process intentionally stages the message differently;
- verify that the bot action succeeded and the expected message/update exists;
- if the bot action fails, report the release as published-but-not-fully-announced rather than silently claiming completion;
- never expose bot tokens, webhook URLs, channel secrets, credentials, or protected IDs in this file, logs, commits, or release output;
- preserve the existing bot's format/channel/routing behavior unless explicitly asked to change it.

Before the first release from a fresh thread/environment, inspect the existing release tooling/history to identify the exact safe bot invocation path. Record **how to invoke it without recording secrets** under **Verified local release implementation** below.

### 18.12 Release order of operations

Unless the current verified release tooling requires a different safe order, use this as the release control sequence:

1. **Preflight** — read `PROJECT_STATE.md`, applicable `AGENTS.md`, release docs/scripts, Git status, relevant source pins, and current live feed.
2. **Scope** — confirm whether the user requested local-only work or a published release.
3. **Implement** — make the smallest coherent change and preserve regressions/architecture.
4. **Test** — run risk-appropriate unit/contract/replay/config/in-game checks.
5. **State/docs** — update `PROJECT_STATE.md` and other durable docs for the new implementation/release state.
6. **Version** — bump/synchronize all verified version locations.
7. **Clean validation build** — build with the established release configuration.
8. **Package runtime** — generate the actual Dalamud-distributable package.
9. **Package source** — generate source archive if required by established process.
10. **Validate artifacts** — inspect contents, versions, file sizes, and expected assets/dependencies.
11. **Hash** — generate/validate final-artifact hashes according to established process.
12. **Git diff review** — confirm no secrets/unrelated changes and that state/docs/version are consistent.
13. **Commit** — create a coherent release commit.
14. **Push** — push to the intended Git remote/branch and verify remote state.
15. **Update hosting/feed source** — place/update artifacts and the Nexus feed entry using the established Daily Pilcrow infrastructure while preserving all other entries.
16. **Deploy** — execute the established website/distribution deployment.
17. **Verify public artifacts** — confirm live URLs return the intended artifacts.
18. **Verify live feed** — parse production `pluginmaster.json`, confirm Nexus version/links/metadata and preservation of other entries.
19. **Verify Dalamud** — confirm discovery/install/update behavior to the extent practical.
20. **Discord release update** — send/post the release bot changelog containing the new changes through the established safe mechanism.
21. **Verify Discord** — confirm the expected release update was delivered.
22. **Final state check** — ensure `PROJECT_STATE.md` still accurately reflects the released version/current workstream; if production verification revealed a discrepancy, update it and commit/push the documentation correction.
23. **Report** — separately report code/test/build/package/Git/deploy/feed/Dalamud/hash/Discord status. Never collapse unverified steps into “release successful.”

### 18.13 Shared-feed safety during migration

Until predecessors are explicitly retired, the custom repository may need to keep multiple Vieri products installable/updateable. Therefore:

- Nexus publication must not remove predecessor entries;
- updating Nexus must not overwrite another plugin's runtime/source archive;
- do not reuse an ambiguous generic ZIP filename if the current infrastructure uses plugin/version-specific names;
- verify unrelated feed entries before and after feed updates;
- keep legacy URLs working where the established process relies on them;
- retirement/removal from the feed is a deliberate migration milestone requiring parity, config migration, compatibility, rollback/deprecation policy, and user approval—not ordinary release cleanup.

### 18.14 Secret/credential boundary for release operations

Never store in this file, Git, feed JSON, release archives, logs, or Discord messages:

- GitHub personal-access tokens;
- hosting/deployment tokens;
- Vercel/hosting credentials;
- Discord bot tokens;
- webhook secrets;
- private channel secrets;
- SSH/private keys;
- cookies/session tokens;
- Supabase/service-role keys or other backend secrets;
- user-specific FFXIV/Dalamud credentials or private configuration.

It is acceptable and desirable to document the **safe mechanism** by which existing tooling receives a credential (for example, environment variable name, secret-store integration, authenticated CLI profile, or CI secret name) only after verifying that mechanism. Never copy the secret value into project documentation.

### 18.15 Verified local release implementation

Foundation intake on 2026-09-08 positively verified the following from the real Nexus and Daily Pilcrow workspaces, committed release history, release scripts, public feed, hosted archives, and the completed 0.1.0.3 announcement workflow. Maintain these facts when the infrastructure changes.

- **Website/distribution repository or local path:** `D:\FFXIV Plugins\TheDailyPilcrow`; Git origin `https://github.com/iampilcrow/TheDailyPilcrow.git`, normal production branch `main`.
- **Source path that deploys to `/dalamud/pluginmaster.json`:** `D:\FFXIV Plugins\TheDailyPilcrow\public\dalamud\pluginmaster.json`. It is a committed static Next.js public asset and is served at `https://www.thedailypilcrow.com/dalamud/pluginmaster.json`.
- **Runtime ZIP generation command/script:** from the Daily Pilcrow root, run `scripts\publish-dalamud-plugins.ps1` with `-OnlyPlugins VieriNexus` and a non-empty `-ReleaseNotes` entry for `VieriNexus`. For Nexus the publisher runs a Release `dotnet build` of `D:\FFXIV Plugins\VieriNexus\src\VieriNexus.Plugin\VieriNexus.Plugin.csproj`, supplies the local Dalamud dev-library path, and uses the Dalamud packager's generated `latest.zip`. Do not run the publisher during inspection-only work because it builds, creates/copies archives, rewrites the feed, creates a changelog, and stages files.
- **Runtime ZIP output path:** packager output `D:\FFXIV Plugins\VieriNexus\src\VieriNexus.Plugin\bin\Release\VieriNexus\latest.zip`; publisher-staged artifact `D:\FFXIV Plugins\TheDailyPilcrow\public\dalamud\plugins\VieriNexus\VieriNexus-<AssemblyVersion>.zip`.
- **Runtime ZIP naming convention:** `VieriNexus-<four-part AssemblyVersion>.zip`; versioned artifacts are immutable and must not be overwritten.
- **Runtime public hosting path/URL convention:** source path `public/dalamud/plugins/VieriNexus/VieriNexus-<version>.zip`; public URL `https://www.thedailypilcrow.com/dalamud/plugins/VieriNexus/VieriNexus-<version>.zip`.
- **Source archive generation command/script:** the same publisher locates the Nexus Git root and invokes `git archive --format=zip --output=<sourcePackagePath> HEAD`. Commit the intended Nexus source first; the archive and changelog `SourceCommit` therefore correspond to `HEAD`.
- **Source archive output/naming convention:** `D:\FFXIV Plugins\TheDailyPilcrow\public\dalamud\sources\VieriNexus\VieriNexus-<version>-source.zip`.
- **Source archive public hosting path/URL convention:** `https://www.thedailypilcrow.com/dalamud/sources/VieriNexus/VieriNexus-<version>-source.zip`.
- **Feed source/update command or generation process:** the publisher reads the SDK-generated `bin\Release\VieriNexus\VieriNexus.json`, constructs the feed entry, and for a selective release replaces only the matching `InternalName` while preserving every other existing entry and its order. It writes `public/dalamud/pluginmaster.json`, creates `public/dalamud/changelogs/VieriNexus/<version>.json` for a genuinely new version, stages the feed, every feed-referenced runtime/source archive, and the new changelog, then runs focused validation with Git tracking required. Review the staged diff before committing.
- **Website/distribution deploy command/workflow:** commit the reviewed feed, new versioned runtime/source archives, and changelog together in `TheDailyPilcrow`, then push `main` to `origin`. The repository's Git-connected Vercel project builds/deploys the pushed commit; there is no required local production-deploy command in the verified plugin workflow. The Vercel build runs `pnpm run build`, whose first step is the whole-feed `--inventory` guard. Wait for the production deployment to be Ready before live verification.
- **Hosting provider/deployment trigger relevant to `/dalamud`:** Vercel hosts the Next.js project/static `public` assets and deploys from the GitHub repository's `main` branch; Cloudflare provides DNS/proxying for the Daily Pilcrow domain. A pushed release commit is the deployment trigger.
- **Release artifact hash algorithm/process:** SHA-256. `scripts/dalamud-release-validation.ts` hashes the final local artifact and the bytes downloaded from the advertised public URL and requires equality, then validates ZIP structure/CRC and the runtime manifest identity/version. Record the final runtime/source hashes in the release report and established Daily Pilcrow release log; do not hash an intermediate archive.
- **Live-feed/public-artifact verification commands:** from the Daily Pilcrow root, use `corepack pnpm dalamud:verify --plugin=VieriNexus` before publication and `corepack pnpm dalamud:verify-live --plugin=VieriNexus` after production is Ready. The local check parses the feed and validates both ZIPs, package structure, DLL/manifest identity, and version. The live check additionally requires the production feed to retain the expected entry count/metadata, downloads the advertised runtime/source URLs without authentication, requires HTTP success, matches SHA-256 against the local committed artifacts, and revalidates both ZIPs. `pnpm run build` also runs the fast whole-feed inventory/Git-presence guard without decompressing unchanged plugins.
- **Dalamud install/update verification procedure used by this project:** configure `https://www.thedailypilcrow.com/dalamud/pluginmaster.json` as the custom repository, confirm VieriNexus appears exactly once in the Dalamud Plugin Installer at the advertised version, perform a clean install where practical, use **Update Plugins** from the previous published version for update coverage, and confirm the loaded plugin reports the intended version. The HTTP/hash/archive verifier does not replace this manual in-game check. Dalamud installation/update, responsive Migration text, zero-route import, window reopen, on-disk persistence, and direct source/backup hash integrity are confirmed on 0.1.0.4. Its reload message recovery failed there, was corrected in 0.1.0.5, and is now user-confirmed; guarded rollback remains a user-side check.
- **Release Discord-bot safe invocation mechanism:** the publisher creates and stages an immutable `public/dalamud/changelogs/VieriNexus/<version>.json`. When that new file is pushed to `main`, `.github/workflows/announce-vieri-plugin-changelog.yml` runs automatically, receives `DISCORD_CHANGELOG_BOT_TOKEN` and `DISCORD_CHANGELOG_CHANNEL_ID` only from GitHub Actions secrets, and invokes `scripts/announce-dalamud-changelog.mjs`. The script waits until the exact feed version and runtime ZIP are live, checks recent channel history for its plugin/version footer marker, and posts only if not already announced. Safe retries use the workflow's manual `workflow_dispatch` with the existing versioned changelog path; never copy credentials into a local command, documentation, or chat. A payload-only preview may use `corepack pnpm dalamud:announce -- --file=<changelog-path> --dry-run` and requires no Discord secret.
- **Release Discord message/changelog format source/template:** source record `public/dalamud/changelogs/VieriNexus/<version>.json`; validation and embed template `scripts/announce-dalamud-changelog.mjs`; operating documentation `docs/DISCORD_PLUGIN_CHANGELOGS.md`. The embed title is `<Name> <Version> is now available`, links to the changelog's repository URL, renders the one-to-twenty release notes as bullets, includes the fixed Dalamud **Update Plugins** instruction, optional icon, timestamp, and idempotency footer `Vieri release • <Plugin> • <Version>`. Allowed mentions are disabled.
- **Dedicated release scripts/workflow:** `scripts/publish-dalamud-plugins.ps1`, `scripts/verify-dalamud-release.ts`, `scripts/dalamud-release-validation.ts`, `scripts/announce-dalamud-changelog.mjs`, and `.github/workflows/announce-vieri-plugin-changelog.yml`. Package aliases are `dalamud:verify`, `dalamud:verify-live`, and `dalamud:announce`.

Verification evidence for the existing 0.1.0.3 release: Daily Pilcrow commit `ecfa159` added the changelog, feed update, runtime ZIP, and source ZIP; Vercel production deployment `dpl_GvtyEW58yAU6ua6EGtKY8N6y15pu` was recorded Ready; the current focused local and live validators both pass; the public runtime and source downloads return HTTP 200, are valid ZIPs, and match local SHA-256 values `C3F52656C4359163E53C82C640FCD23465FA2E5082C41A1EA32059AE45825FEC` and `1FB675728D5FE839E51CDA7B7A5725F6D8116A18FFDEFFAEC7C939DC8FA4B51B`; GitHub Actions run `34188636818` completed successfully for source commit `ecfa159`. This verifies the established release mechanism, not the still-pending in-game migration/import/rollback test.

Verification evidence for 0.1.0.4: Nexus source commit `54510ce` and Daily Pilcrow release commit `684703f` are on their respective `main` branches. The focused local and live validators pass; the public runtime and source archives return HTTP 200, are valid ZIPs, and match local SHA-256 values `17C72FF131316DA719AABC152F6969CAC06A0BE88941E0217433633ED8E8032C` and `703DBDDFF38FB7B71854EBC6B105D2C41F6DAB2B846124816251731B7862043A`. The Vercel deployment check completed successfully, and GitHub Actions Discord run `34282262570` completed successfully for `684703f`. Dalamud installation/update, the responsive visual correction, zero-route import, window reopen, on-disk persistence, and source/backup hash integrity are user-confirmed. The reload-only message loss found in this version is corrected by 0.1.0.5; guarded rollback remains pending.

Verification evidence for 0.1.0.5: Nexus source commit `101058d` and Daily Pilcrow release commit `a2bc367` are on their respective `main` branches. All 21 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, inventory guard, and production build pass. Vercel production deployment `dpl_BnbzJgDxC1xQYp35B3tCqGKeffka` is Ready. The focused live validator confirms HTTP 200, valid ZIPs, and exact runtime/source SHA-256 matches `A172881796EF06D272268E3E1E0DAC2E61F585F309FC002DE851D1C86F9D2A23` / `10351C1E2EC08995D72B3A83FCE23C3E7A075ED3EAB469EAD1FE72EE8B055787`. GitHub Actions Discord run `34295565914` completed successfully. The user confirmed corrected disable/re-enable recovery; guarded rollback remains unverified.

Verification evidence for 0.1.0.6: Nexus source commit `460e730` and Daily Pilcrow release commit `3be445d` are on their respective `main` branches. All 23 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_2KVVzGiGwUAZQh7WGJp4WUL8LrDy` is Ready. Daily Pilcrow documentation commit `21b8895` is also pushed and its final production deployment `dpl_BhARY2wPQLM5an23dPevECVy6GhE` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.6 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `0E35214FB5E42EABF3CA509CF1FC3B4C1A3067B0AB97D92AC1F49452697FA377` / `C4E0374DA7738BFC3434BE4E63808A5422D8FFFCF6D339008AC93CA3CD5F67AA`. GitHub Actions Discord run `34299175777` completed successfully. Dalamud update and the zero/non-empty Routes-page/IPC behaviors remain user-side verification.

Verification evidence for 0.1.0.7: Nexus source commit `f073d94` and Daily Pilcrow release commit `ac73044` are on their respective `main` branches. All 23 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_5vFZEh1q21VqVQZKkjCBW3UyziPn` is Ready. Daily Pilcrow documentation commit `c672e91` is also pushed and its final production deployment `dpl_5RPnCDSxNaFUg3dmPaP4z2P5HGih` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.7 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `270F7F0B0002E03B1A9AAFFEAA2690FB2AD7F6C7E0CDCB2514E6909DCE057A04` / `CC8438F64DB4168611E254130CBE618B83521D9061FF0D27DF65F9E8C3163ED7`. GitHub Actions Discord run `34300556246` completed successfully. The corrected settings-panel layout, a non-empty route fixture, and read-only IPC remain user-side verification.

Verification evidence for 0.1.0.8: Nexus source commit `7df701c` and Daily Pilcrow release commit `221b975` are on their respective `main` branches. All 29 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_GUke8drcXKKab22w9d7o79wW2zSp` is Ready. Daily Pilcrow documentation commit `98172e5` is also pushed and its final production deployment `dpl_7sBTdoHMsLqtxMcGBekkGBCVEPew` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.8 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `AB48750EE1D6DD8AE093B2D0CAD131D241D42A8CB88DFF178E6FE1687F407680` / `272A717A3FFC6BB5672F723E29F811FDA39B5926C60C4CB8A6658B3113F80AA0`. GitHub Actions Discord run `34301968187` completed successfully. The Activation Safety panel/source detection and the five read-only navigation IPC calls remain user-side in-game verification.

Verification evidence for 0.1.0.9: Nexus source commit `e99faaf` and Daily Pilcrow release commit `8f08016` are on their respective `main` branches. All 35 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_5GqCHKMNSyqhmEKm3LurT5c7ZaRd` is Ready. Daily Pilcrow documentation commit `45bd900` is also pushed and its final production deployment `dpl_BkF9GrDZmX7bpowaXMAUo5am2WmL` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.9 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `D6D39E25AF72B991942ECCC47DB7CB374E10407196650E9ABE095102BFF8D85D` / `5F7F49720D16536FEEF48ED64DCCB6E436C9FCA02C0A4DDE7EFC0B4CB3EFE0A7`. GitHub Actions Discord run `34303920092` completed successfully. Dalamud discovery/update and the revised Activation Safety display remain user-side in-game verification.

Verification evidence for 0.1.0.10: Nexus source commit `685dca5` and Daily Pilcrow release commit `b872861` are on their respective `main` branches. All 43 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_ACdJrq7HN6ENtipicZungpjSE66n` is Ready. Daily Pilcrow documentation commit `e18585f` is also pushed and its final production deployment `dpl_2EYChq79JPvJCu9GxWQQ1VFMPUz3` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.10 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `684064BB745695E9BED759D2752CC8BC109B3ECF6DC3CB94BC5CBE74A092F377` / `80733CDDE32DAFD877A4B5DD4645FA639795B839E49F65E517B8B7947C72F6F8`. GitHub Actions Discord run `34306054991` completed successfully. Dalamud discovery/update and the manual-yield readiness display remain user-side in-game verification.

Verification evidence for 0.1.0.11: Nexus source commit `3900673` and Daily Pilcrow release commit `12ba98a` are on their respective `main` branches. All 59 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_E3gfwt2hYyXP9iRvgFkhR7K1EgQm` is Ready. Daily Pilcrow documentation commit `eafc715` is also pushed and its final production deployment `dpl_BPG4nKjfUWdZn1pWzm7T2T2hfYdT` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.11 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `9679B9E2EB70FB6EFE83FF4D9103F295EDA0E4DDF1B32A81EF88924E868943E4` / `A409323DE691B83CE2137BC9EA5970A3CBCEEF709F64C87B0736C5FBA4FF8496`. GitHub Actions Discord run `34308239307` completed successfully. Dalamud update and the reload/watchdog readiness display remain user-side in-game verification.

Verification evidence for 0.1.0.12: Nexus source commit `dc9242f` and Daily Pilcrow release commit `8b92bc4` are on their respective `main` branches. All 71 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_9jYWQzsMA38HwqHPPhJygzqvsusU` is Ready. Daily Pilcrow documentation commit `de27c5f` is also pushed and its final production deployment `dpl_753aGdDBr7ShesHXZiMZ3jumEdjg` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.12 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `4F5724B41CC6AC0FC9CCAB26C4C7082F5A6F27903703B15084D7FDDE56D7239C` / `8E75159464F2CD70CD5D255604609063D12B53A476A798C04FACA248E427707B`. GitHub Actions Discord run `34342268338` completed successfully. The user confirmed the Dalamud update, all three green safety guarantees, disabled approval while VieriNavPlotter is loaded, manual-unload enablement, and the no-movement session-authority handoff in game.

Verification evidence for 0.1.0.13: Nexus source commit `b12f2f3` and Daily Pilcrow release commit `730d3fc` are on their respective `main` branches. All 77 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_2ANyPZSoLMN2bHHejHRRoNLLgEH1` is Ready. Daily Pilcrow documentation commit `8fb2e27` is also pushed and its final production deployment `dpl_97gcBViWrmsNihGpPTJpSRTe4KJx` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.13 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `E4B4A80325C3151BC91436002559CFC789FDFBFD80895D384B9BF051181A34F8` / `5E16F62059975DF172C6A3556C9486D40771EB4BF704051FDE1BE2C7CA9845A8`. GitHub Actions Discord run `34344736882` completed successfully. The user confirmed the Provider Health/Safety Audit display and isolated 5/5 simulation in game while VieriCodex actively moved through a duty; movement was never interrupted.

Verification evidence for 0.1.0.14: Nexus source commit `723ef16` and Daily Pilcrow release commit `7a74a9d` are on their respective `main` branches. All 82 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_2wWdfsvANQyPyJh4WavnPqUCzo49` is Ready. Daily Pilcrow documentation commit `1421810` is also pushed and its final production deployment `dpl_3p3i4TWqaX1fZqH1xWgcbLDfGydr` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.14 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `C33EC28759C4EF0FBBA857EB9E2239D0B3F2D561C3E4E028536A71C0AB4FA457` / `C4CB1B7BC85C46EAA2CE37C6F8A66D3F0EAF811781C50F8E62829BEE115884EC`. GitHub Actions Discord run `34348284685` completed successfully. The user confirmed the isolated safety simulator passes 6/6 in game. The conditional stopped-intent checkpoint remains unforced because no route playback is enabled.

Verification evidence for 0.1.0.15: Nexus source commit `f5c6f91` and Daily Pilcrow release commit `2e5957a` are on their respective `main` branches. All 96 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_AzHaXQoGRPjHFTcAXXgwxa5TH6z7` is Ready. Daily Pilcrow documentation commit `f7dc587` is also pushed and its final production deployment `dpl_gCLKGwZ3T4FzgWVqgvyw834NUEFQ` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.15 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `F46FB370C26594B086E954B9BEB4D914C860580DBA6526B61425F8DE42A96216` / `3010FE1FA211698AAE6DF865CF301A46B9619038F831C957D9AEE59523D81AB8`. GitHub Actions Discord run `34353177958` completed successfully. The user confirmed working-library/point creation, preview, guarded Travel to Start and ordered playback, button Stop, manual takeover, stopped-intent acknowledgement, staging return, and manual VieriNavPlotter re-enable. The shared-provider replacement branch remains locked by automated no-global-Stop regression coverage rather than a forced live conflict test.

Verification evidence for 0.1.0.16: Nexus source commit `983948e` and Daily Pilcrow release commit `8998162` are on their respective `main` branches. All 109 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_GPTtNam5WBAjNbmN2X1jJTTLMLCv` is Ready. Daily Pilcrow documentation commit `26c2351` is also pushed and its final production deployment `dpl_AmoKc2NpssgjHUUhr2pCRdS7pZgh` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.16 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `B954249F32925544D452794373854900686F44197C3D3A49F4858CC2C5D9EC0B` / `6BE381534DDE51D77F1942B6E5163CF7859CD8E3C932D7D57F7D0526FC19AB5D`. GitHub Actions Discord run `34357630865` completed successfully. Dalamud update and the timed-recording/detailed-editing/clipboard acceptance flow remain user-side in-game verification.

Verification evidence for 0.1.0.17: Nexus source commit `448bea1` and Daily Pilcrow release commit `8483657` are on their respective `main` branches. All 109 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_87vVMtwQEz7qov5DqkG1tmEZjKFN` is Ready. Daily Pilcrow documentation commit `af88ddf` is also pushed and its final production deployment `dpl_DJvrUFqZeosXwHLwm1x1mAiBdUot` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.17 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `D0526057B0EF4DF26DD27A8E02A08C3B95A3D5D04986D1662B9463152AA989A8` / `D5996AC9EFDB4DE0127BA913D290147129073756E612DF86F1F771417D462E2D`. GitHub Actions Discord run `34376408900` completed successfully. Dalamud update, immediate control visibility, and the timed-recording/detailed-editing/clipboard acceptance flow remain user-side in-game verification.

Verification evidence for 0.1.0.18: Nexus source commit `c879c3f` and Daily Pilcrow release commit `82dcb03` are on their respective `main` branches. All 109 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_556SjcJWe7PoLTZQaNARqtmajPxp` is Ready. Daily Pilcrow documentation commit `8be2946` is also pushed and its final production deployment `dpl_EqtxtEeurK3Xx9VYzvptYigNbS7V` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.18 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `8313883E3311EACDB142CC8223317F2B0375029AD378DA67BC5EF7F51E7F38E8` / `50897328612B253C4A2D63DF6DC8177AB9185A217E6918C9086F4232326CE08B`. GitHub Actions Discord run `34378647358` completed successfully. The user confirmed the Dalamud update, both destructive confirmations and cancellation paths, and the single-scroll Routes layout.

Verification evidence for 0.1.0.19: Nexus source commit `cf4313c7495dae56a2556ef5362beba65692f482` and Daily Pilcrow release commit `1f2e4e52c89b31982a934aa231fc263df8aeaacc` are on their respective `main` branches. All 122 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_fHUdKtsGDeahepUuJRkDk3XbqN6s` is Ready on the canonical aliases. Daily Pilcrow documentation commit `57b2f65218bf13966c047833fead181723cb8ed7` is also pushed and final production deployment `dpl_FCtMirNZWUsb1bC481W7aJSsHJhh` is Ready. The focused live validator confirms the feed contains exactly one VieriNexus 0.1.0.19 entry and that runtime/source downloads return HTTP 200 as valid ZIPs with exact SHA-256 matches `304C545BD904A05A66309CF03A33E005C556BC023500C3DC985B261D1BEAB6F0` / `8BCD52022F3FDCB3FCE8BF20B02300979304DAE70232041BA5F2A8B41B547404`. GitHub Actions Discord run `34397580461` completed successfully. Dalamud update and the built-in template copy/assignment/persistence batch remain user-side in-game verification.

Verification evidence for 0.1.0.20 and VieriAutoDuty 1.0.0.438: Nexus source `5b908e91fbded0e912a8f1811d2685aa88d55fae`, VieriAutoDuty source `cffd9a021fe4fac1b74188d8e96ee19d41ebba43`, Daily Pilcrow release `ebd0301dcfc556fd9602b50b65ffce8a8074d454`, and Daily Pilcrow documentation commit `90fe7ed` are pushed. All 133 Nexus tests, 328 VieriAutoDuty tests, the zero-warning Nexus Release build, the successful VieriAutoDuty build, all 205 website tests, typecheck, focused validation for both packages, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_sEFozsqZhLR3Ktej38Jd8xLJ6axa` and final documentation deployment `dpl_8wYp7CAXm1VysukNdWNdgDbdLdw6` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.20 and VieriAutoDuty 1.0.0.438 exactly once; both public runtime/source pairs return HTTP 200 as valid ZIPs and match local SHA-256. VieriAutoDuty runtime/source: `2AC0815ECB5308DF11C16FB829C710CB5DD1F1E7F2933CD0D8875437298495FA` / `E3A6176293CF3CE7EEC4EB8A766F4F9AB18C54586AA4E29CB911D11C24620DA3`. Nexus runtime/source: `915522F203EB38976B9E8FE1722278ED6C48867389ADE71E7C611ED80E9C4D7D` / `48C639E3F41977D30E50807FB9577EEBEDE5E3142E434261DCA71A79906B110A`. GitHub Actions Discord run `34404997766` completed successfully. Dalamud update plus the target-capture, filtered-live-path, cross-zone delegation/Stop, Nexus Gear override, and unrelated-provider-isolation checks remain user-side in-game verification.

Verification evidence for 0.1.0.21: Nexus source `f359e900e07e26db7d7f75acfb87f9b1e28c5d4e`, Daily Pilcrow release `16ea250dd82eacb450343b1b0a44d19048393c99`, and Daily Pilcrow documentation commit `3b7a3551eab1e2813142c386b5b43ece123e35aa` are pushed. All 133 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_7Njr1RqJhdWWnVtgEPpHbivbwPWi` and final documentation deployment `dpl_6CxW1J7NtAqsgHRxj3z5BuQPYr8t` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.21 exactly once; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `E1EC998539C85672E5BD57315E1A084B24A864A4C7F00B78F24DDB1D13332835` / `A79EB8CB23FD41C5C768A8EEC8E66C02CE2DC9051C2ACC2DEF47D54C59076443`. GitHub Actions Discord run `34410254108` completed successfully. The persistent fresh-route action and the remaining 0.1.0.20 closure batch remain user-side in-game verification.

Verification evidence for 0.1.0.22: Nexus source `d91be400b0b8c3741f87e64968372adb8091da7d`, Daily Pilcrow release `90aef0118406bfb44e13bcb3425981ec137f81c8`, and Daily Pilcrow documentation commit `45d84411d31f3e3efcec2159c61dd7f583687d4d` are pushed. All 136 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_J917MaCr7zxFZcj7qsRmmaYQnJii` and final documentation deployment `dpl_H1vpZKdz36ZnBeAVP1BSCvZdtUYf` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.22 exactly once; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `209528F99AEE59563143291328770721C5A85B371E87A54EBF34F064DAAE5345` / `F7A33F2BD74DC7250CD112FADC3FF537F5AEFB6D8AF5EFBC49F0DD60CB16F634`. GitHub Actions Discord run `34423862188` completed successfully. Dalamud update and the focused Play → Stop → Play in-game check remain user-side verification.

Verification evidence for 0.1.0.23 and VieriAutoDuty 1.0.0.439: Nexus source `544bdc8584538b758b155bdf89d2ebacad28268f`, VieriAutoDuty source `3ae9957838110d457554b39f7c71469bf904727d`, Daily Pilcrow release `3dd91a474831c80cfc184eeababc7157410c897e`, and Daily Pilcrow documentation commit `ccaa438b52b398f87c769c905cdb1c39064f0dac` are pushed. All 137 Nexus tests, 332 VieriAutoDuty tests, the zero-warning Nexus Release build, successful AutoDuty build with its known upstream warnings, all 205 website tests, typecheck, focused validation for both packages, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_EKUADL8PhqDf93pc7ZTyspNtesaU` and final documentation deployment `dpl_9qHXkYjuuq8ka8qjTBq8QTxXiGYF` are Ready on both canonical aliases. The live feed advertises both versions exactly once; all four public runtime/source downloads return HTTP 200 as valid ZIPs and match local SHA-256. AutoDuty runtime/source: `CA183C192E98540502B593444BA4B184E7C456CEB07093F4B5DCF92E184A66DF` / `A7311B6DF6FFFE56A4118E887C90695348C5FBF92A9A1CADCC16BAD6BBDF001A`. Nexus runtime/source: `C528E877E1F79A824EBF7E1B278C8CA1C7C25DA0E728809885CBC87AD105AEE5` / `3745AD4473D1B9458078D1269CE5FD8E01C9EF53CCB763BA4F2236B50368BAFE`. GitHub Actions Discord run `34426996224` completed successfully. Dalamud update, inn entry followed by authored playback, and manual takeover during the provider-led approach remain the only user-side checks.

Verification evidence for 0.1.0.24 and VieriAutoDuty 1.0.0.440: Nexus source `4cfc89624b4c53b4a1af85a0ea31477b19a98681`, VieriAutoDuty source `a5e1e757e35bd77191a647add7124210cdf86122`, Daily Pilcrow release `b6dd50f1efc941cf7e0948be47ec217a877fb338`, and documentation commit `1234f53e2c32167f5df581aa02f64f1323b247a6` are pushed. All 138 Nexus tests, 337 AutoDuty tests, both builds, all 205 website tests, typecheck, focused package validation, inventory guard, and production build pass. Release deployment `dpl_A2Vq7QJJMZ4kGNx1NEkw2mjJM8iW` and final deployment `dpl_9S2t5JtUPaGbCZPuZt1MpaMjcutV` are Ready on both canonical aliases. The live feed advertises both versions exactly once; all four public runtime/source ZIPs return HTTP 200 and match SHA-256. AutoDuty: `3F2050D07E654F481B2ACD4D8E741DC31F6E77CEEA461801ED62A995F87C7C74` / `531F1121300A13E5AF88B1012312B0FEA40857A4B223713D5379CCB72A21DC56`. Nexus: `8DB78B2A309A22EA2347F4C8D82A28FA4B7FB353F74214BFFDF86345F0DFE658` / `6F9692741433E1E499D157F3A99CB174CE7F1970C48919856E6720940466120C`. Discord workflow `34429358396` succeeded. Only the focused one-click inn trip and manual-stop check remain in game.

Verification evidence for 0.1.0.25: Nexus source `8c3829ec1af2c25bacb00d204cb5a245033f2d83`, Daily Pilcrow release `509a2dc14a59bca290e782952cd7e2e9506bd86d`, and Daily Pilcrow documentation commit `406caa6083a908958dc66c25a094dfea58fee7e4` are pushed. All 138 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Vercel release deployment `dpl_CCuCv4vzXa5cSGqSND4BmVRGLfxW` and final documentation deployment `dpl_94SvkUgKzFyvTjgpvDPh5i7xFLMC` are Ready. The live feed advertises VieriNexus 0.1.0.25 exactly once; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `8EC4F33FE1DEBEB67F88EF0AD72DC8F8C8BDD393587D8029E78128761B0D98CC` / `598786B08EC1FEB9D6D0A8DB67F2E2CF944BB23C6B7F09522F0314D15A73F89A`. GitHub Actions Discord run `34431922374` completed successfully. The user then confirmed Faezghim and Grand Company inn routes work in both directions with no issues; the focused in-game acceptance gate is complete.

Verification evidence for 0.1.0.26: Nexus source `ecde39a0ad1a3af5a2200cef5b575e03f7b6b010`, Daily Pilcrow release `92460bd6f83ea758cb34112af40b982ad63aecad`, and Daily Pilcrow verification docs `748ec93` are pushed. All 149 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Release deployment `dpl_hdrNfaWSarivsbKuk5cdMxKy7Kub` and final documentation deployment `dpl_GedkZx5S7m355rBz7RfokXAKpMV5` are Ready. The live feed advertises VieriNexus 0.1.0.26 and the public runtime/source archives return HTTP 200 as valid ZIPs with exact SHA-256 `072EC21F7F68C633F7BAF940D3B9481ACEAB887EDA69D974B9A4425EA4C85E3C` / `77ED1FC16B99A18A5C5ADCBB7CD151B99286749507353415132B513CF931587E`. GitHub Actions Discord run `34468325542` completed successfully. Progression is planning-only: the current-job draft, provider capability observations, conflict rejection, and bounded preview are live, but no quest or duty provider operation can be invoked.

Verification evidence for 0.1.0.27: Nexus source `4fe9acfd0251487332fdf23f5ea411cf0e3b4923`, Daily Pilcrow release `d913d2de5fe46bcad0bef25edf047b9f515f3326`, and Daily Pilcrow verification docs `cbeab3de158f110b9f2fd336864cb0be415f1fc9` are pushed. All 155 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Release deployment `dpl_5dYmP8py41GQsP5xa3HSupS8Q7XY` and final documentation deployment `dpl_FtP2u7cixAKpBTo4qsWppvCk2Jxx` are Ready on the canonical aliases. The live feed advertises VieriNexus 0.1.0.27; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `97CD0D251F5E2A2CDF152991F9C471EE2729F65CDFFB31902661590E9F7E3E42` / `7A5CECDBDF9A52777C13C6B772DAB8126685C445FDF4341F27EE06F81C079BCE`. GitHub Actions Discord run `34474905407` completed successfully. Routes now use Nexus-owned complete-trip coordination over stock Lifestream and vnavmesh and no longer invoke VieriAutoDuty's fork-only route IPC; focused same-zone, cross-zone, and Grand Company inn parity remains the user-side acceptance gate.

Verification evidence for 0.1.0.28: Nexus source `7593defc07a5926614dae8eb97f1136149929a15`, Daily Pilcrow release `d4c44a2b5cdf95a5eb8b26167657e7662d86b747`, and Daily Pilcrow verification docs `1441939eb9f5dca87c121b59feed0099e046e706` are pushed. All 158 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Release deployment `dpl_CdYPsTnyw4S9644UzWSfxUYsYJtw` and final documentation deployment `dpl_H7vftqH3a8pinjL5Wzzx1oGCQCum` are Ready on the canonical aliases. The live feed advertises VieriNexus 0.1.0.28; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `21494E950C2744891C58093BFA4955126E0C52878B487E604D099DC56C81FA4C` / `FB4CCE94518AC6729CAE04A97A15B19C128160D07010071F82B1789DD71A7E7A`. GitHub Actions Discord run `34477148601` completed successfully. Mesh-assisted authored points now use a cancellable vnavmesh path calculation rather than literal straight-line `Path.MoveTo`; the focused Faezghim route and Stop/no-delayed-resume behavior remain the user-side acceptance gate.

Verification evidence for 0.1.0.29: Nexus source `b2be70b9c3c2c65e21b64311a2d21e2545bcdc26`, Daily Pilcrow release `866b86e58b242d61a22849182ee4e131fb3ccbb0`, and Daily Pilcrow verification docs `bba7b30c9cdb254323def0d0ea7f67286333463c` are pushed. All 160 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, whole-feed inventory guard, and production website build pass. Release deployment `dpl_H7mw3uLPmakKAzDvSBxbmJ5FJScm` and final documentation deployment `dpl_5R5k1m7qakF2y3ABB6AXLLdq7C98` are Ready on the canonical aliases. The live feed advertises VieriNexus 0.1.0.29; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `FF12B5F1DA20E6EA6BE3574E43F8F2A3639A8F9ECB135BA7D39C4DD7072C3F8A` / `394A843C941D7AEB9293425D94236A12A6BCFF4A3CB8FA74E148E87B9A0C0B05`. GitHub Actions Discord run `34478706443` completed successfully. Territory-aware flight selection corrects the logged Limsa `Nav volume was not built` failure and retries unavailable outdoor flight paths once on the ground; the focused Faezghim route remains the user-side acceptance gate.

Verification evidence for 0.1.0.30: Nexus source `efb69b7d5fe6ed9b14abed3e9c61748240096894`, Daily Pilcrow release `e39d6096d1091c841f90764949fbc47b68adec9d`, and Daily Pilcrow verification docs `289f805a1c036f5517b7947ed5007c18b3d20b9c` are pushed. All 174 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_6mE5rkZd79vBHtDQZf4NrMFjrrto` and final documentation deployment `dpl_BiUg3FMLvF1R3CwrTxBsRbopyMrX` are Ready on the canonical aliases. The live feed advertises VieriNexus 0.1.0.30; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `C6A085E58A370102A1A3FB4A2BA1BD7A669C955B1E4E599D93BB98CFF4B9A9AF` / `76B1786639869ED7B6060012D467ED98A01358B4CF78DCDE9955F849D613AFCE`. GitHub Actions Discord run `34488407059` completed successfully. The focused user-side gate is one bounded duty with Last Run armed, followed by Resume and explicit Stop; no long leveling session or Routes retest is required.

Verification evidence for 0.1.0.31: Nexus source `dcc0235b01db2ea142cfa1e65cc5ab257ba47842`, Daily Pilcrow release `ca9c74b10bd464be72127298b753d0f4b935d102`, and Daily Pilcrow verification docs `99de5a7cbaf4524d39ffabca95adad42d67da104` are pushed. All 174 Nexus tests, the zero-warning Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_CjxJnVm5XVEkDb7eQe1mvMmcPhBB` and final documentation deployment `dpl_D7Qn5aRAMUpaPTvd3RRe319Q3vaq` are Ready on the canonical aliases. The live feed advertises VieriNexus 0.1.0.31; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `AF2771D14E53C2F16DCD88B79362FDE8E2DDC36B2A77736F0CBEAAB1AC85303A` / `1F347B790FAB6F5490D9BF2E0E5B32D2790AC034D18F66A18E284FB78A9092C5`. GitHub Actions Discord run `34491064025` completed successfully. After removing the duplicate stock AutoDuty session collision, the user accepted the full in-game bounded-duty lifecycle: initial Start, explicit Stop, Resume with a fresh plan, and a second normal Mt. Gulg dispatch all worked. The Progression duty gate is complete.

Verification evidence for 0.1.0.32: Nexus source `85b47df77301bc27593a8ae692a686def09a41c0`, Daily Pilcrow release `e9e4c3c0903c765b1cd201307f3c97e1e8c796dd`, and Daily Pilcrow verification docs `bf5e9795984540db66e887649a083b7a337c9d5b` are pushed. All 177 Nexus tests, the zero-warning clean Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_HvpYkZzJVeywURdNbzuY1WeJ1aUk` and final documentation deployment `dpl_3yYdENLLVTub96TMvVCqtDFahgJW` are Ready. The live feed advertises VieriNexus 0.1.0.32 exactly once; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `254F91403C8840E3A16FCE46DAAE322E877C603B2C9AA5F633676CAB465AEB20` / `626416EA184DA43EA5FC316149235B0C1809E3009C7A034FB56F59721BA3727E`. GitHub Actions Discord workflow `34534482568` completed successfully. The user then accepted the complete live transaction: Nexus bought gear, returned to the inn, and automatically started the planned duty. The 0.1.0.32 acceptance gate is complete.

Verification evidence for 0.1.0.33: Nexus source `e24082af7e70abf3b2a4d4ddadf89f87f61ebb05`, VieriAutoDuty source `d0c422cdf668817fd59c77c11e5303a040d283db`, Daily Pilcrow release `d8a842c0b29f214f8c2095c8368808cdbb4f9425`, and Daily Pilcrow verification docs `0d47c5b4c7e3da945949b251529498c3c640409e` are pushed. All 186 Nexus tests, the zero-warning clean Nexus Release build, all 342 VieriAutoDuty tests, its unchanged 32-warning upstream/dependency build baseline, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_H8nmhDQ3BMDZAcG2CYFotV31cNgk` and final documentation deployment `dpl_GwZyqrrBokZ61wxPLCFsLEQtULNU` are Ready on the canonical aliases. The live feed advertises VieriNexus 0.1.0.33 and VieriAutoDuty 1.0.0.441; all four public runtime/source downloads return HTTP 200 as valid ZIPs and match their local SHA-256 values. Nexus runtime/source hashes are `EF10AB9D982AAB01D866EE02BED9F26E1CC9A91DB7F42AD3CAF605C342A3BF03` / `1577F20DCA1E6A172656450FC9B5A724AC9EBD363BB241B7C86B2BCE2A2488A5`; AutoDuty runtime/source hashes are `C473092425647461C933BEDA6497175839DC69DC05D99F2BB554A0750456639B` / `50674EA8CD46DA9CC7C4BB3795899A6B56821D7CF69B918823FA4E96732D656D`. GitHub Actions Discord workflow `34544115950` completed successfully. The focused user-side gate is one Gear & Inventory preview and either one approved purchase or a valid no-upgrade result; accepted Routes, bounded-duty, and automatic gear-readiness gates should not be repeated.

Verification evidence for 0.1.0.34: Nexus source `89e79634c6aeb5deceb255fb32faa3451d667e8b`, VieriAutoDuty source `0a81501e7f00d682ad66211080fb2a2f1ae92fe0`, Daily Pilcrow release `0bb070d86e119a5ef1f8c155f574d04af2b0dc31`, and Daily Pilcrow verification docs `5258339ab854a9a07613f7da7d7eb7238bb92722` are pushed. All 191 Nexus tests, the zero-warning clean Nexus Release build, all 342 VieriAutoDuty tests, its unchanged 32-warning upstream/dependency build baseline, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Production deployment `dpl_D1jHitvFTb6SKCCPLfCnGvnbz7Lo` and final documentation deployment `dpl_PjUAzXoNpbxcFQvuLRLiwLnHisiE` are Ready on the canonical aliases. The live feed advertises VieriNexus 0.1.0.34 and VieriAutoDuty 1.0.0.442; all four public runtime/source downloads return HTTP 200 as valid ZIPs and match their local SHA-256 values. Nexus runtime/source hashes are `FC90241965071A56BF6F7E2DE38E5D1823F243D5CD5B245B08E1AE13F08D6CA8` / `9818C085332BA337831D6779AC074D8F7BEC6521433469F94805B369EDFDC2B0`; AutoDuty runtime/source hashes are `523ABEAADA2988ACAD98F98B059E32B804E3EB897D08FBED3513EF2A1823BA96` / `11D2925529BAE73FD1C0CDC2565FF96553542DF61AE46E4C2C7AB45BF62956FD`. GitHub Actions Discord workflow `34553375434` completed successfully. The user's 0.1.0.33 `Found 0 verified upgrade option(s) for MCH.` result is accepted; the focused 0.1.0.34 gate is one refresh proving the same answer now comes through Nexus-owned role-aware ranking. No purchase, Routes, duty, or automatic Progression retest is required.

Verification evidence for 0.1.0.35: Nexus source `8dec5406291d44bed621b1f0de5821773e004bde`, Daily Pilcrow release `e2a1f5ff7d420a9a341c112585469748a889274d`, and Daily Pilcrow verification docs `54bfb40a0e3fc4256bb90ca8802259ecef3d6c0e` are pushed. All 194 Nexus tests, the zero-warning clean Nexus Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Production deployment `dpl_FBMRgFqf9dgvcLLW23XWgPnpKmVQ` and final documentation deployment `dpl_6ExUtjjmReQmK557jLicmGtJMY9Z` are Ready on the canonical aliases. The live feed advertises VieriNexus 0.1.0.35; its public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `1DDED65D3CD5D489F1417CCD3613E349BA4915A1CDB328EA9848F435C9DEBA5E` / `7FCF06A97210227A7D42461350311999F8B876249B05951B9E66EDF9FDD4CD1E`. GitHub Actions Discord workflow `34554855142` completed successfully. Manual and automatic gear planning now read equipment and supported vendor catalogs directly in Nexus; the focused user gate is one MCH refresh returning the already-accepted zero result. No purchase, Routes, duty, or Progression run is required.

Verification evidence for 0.1.0.36: Nexus source `7a90146cabf97fb067807c1f21467cd28e65ecfa` and Daily Pilcrow release `8cd32075cf5428e1fbae5434d6ce75c2d48bd40f` are pushed. All 198 Nexus tests, the zero-warning clean Nexus Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Production deployment `dpl_DCoQeRH6WpAb5jF6AoipaTGyhWsF` is Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.36; its public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `9C05FAD8645DCBBEBBA04366020194A9C0C22492C55F34F02E50186B6D4DCFED` / `C18AF7F00F63542D9178992B9301FE903589CD4ACE4848102F68E04027A66F30`. GitHub Actions Discord workflow `34560239948` completed successfully. Gear & Inventory execution is now Nexus-owned end to end: exact approved vendor and territory are pinned through travel, purchase, equip, gearset update, and displaced-item cleanup; VieriAutoDuty is not used for gear. Failed or stopped gear transactions cannot be mistaken for completion and cannot dispatch the next Progression duty. The repeated zero-upgrade lookup is no longer a development gate; the only remaining user-side acceptance is one naturally occurring real-upgrade transaction during ordinary play.

Verification evidence for 0.1.0.37: Nexus source `105b27e74e5408121b18ee95d309e058978cb4da` and Daily Pilcrow release `cd525a53b9c0b07fa803d6f4fae88cd3da8c06bf` are pushed. All 202 Nexus tests, the zero-warning clean Nexus Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Production deployment `dpl_4ncXLH8rWboS6aP7KD45EqNVSebF` is Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.37; its public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `72DC7A831216F25DA411F113572D2740DE17E9C8F709B140CF6711A2CDE7091D` / `13927878F15BEED04F41FD148F140E6AB8579C57727F0A6318B52DC3EEE37AB9`. GitHub Actions Discord workflow `34561678803` completed successfully. The complete saved VieriAutoDuty profile and operations policy can now be backed up and imported transactionally into Nexus staging, and the optional compact Nexus overlay organizes only working controls under Goto, Gear, Inventory, and Extras. Native maintenance execution remains the next implementation slice, so the staged policies cannot cause duplicate automation. No special user-side retest is required for this preservation/UI release.

Verification evidence for 0.1.0.38: Nexus source `00a73be28cd387a34123f642ff027e5e153ec7b7`, Daily Pilcrow release `fd9c2af18c32050161b267f714620e7ff538f99b`, and Daily Pilcrow documentation `c6fb97ff65e61d31809e40d2dee656295a780299` are pushed. All 208 Nexus tests, the zero-warning clean Nexus Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_8E6kFMwzVL6hSgxjpz9ciWhxLMtR` and final documentation deployment `dpl_ED9JryGXE46KLYKBR3bg9bxjihUG` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.38; its public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `CFA604F6C7C488737FC67EFB8ECEE352847B0CEF557D33D1B8EACFB14A498379` / `22B59BA1CC18C8AA94C097E496BE0073C2B2DD34189A5C6CAD4372443BA8D379`. GitHub Actions Discord workflow `34563867173` completed successfully. Nexus now owns independent operations working profiles, safe native maintenance, the striking-dummy menu/travel, global Stop coverage, and install-first local migration for every computer. Destructive maintenance/storage and in-duty withdrawal remain deliberately blocked and are the remaining AutoDuty migration boundary.

Verification evidence for 0.1.0.39: Nexus source `1b2cbc100b933ee41848ae5a247515b10a846cd9`, Daily Pilcrow release `5091724368f0d4bbb1d8d0d46f7104705da0268b`, and Daily Pilcrow documentation `295389ce91a7b902c687912575f5701ef1f48dc8` are pushed. All 218 Nexus tests, the zero-warning clean Nexus Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_2sKZgXGgwQG1h5QfEkSatYFj8hjo` and final documentation deployment `dpl_4G6zrA9rkH4vJC7VfGpQSy8Y9RbS` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.39 exactly once; its public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `46A00C8BAD75D9CFF184C725911510BBB0379B4933EF79E74A29236A51559249` / `B30DF8C429702D2EF068FB9B68B85F2D5B56E6B8034D070E081AB36D95014565`. GitHub Actions Discord workflow `34591024542` completed successfully. Nexus now owns exact Class/Job/Role quest-family selection and the bounded pinned quest lifecycle while Questionable supplies the narrow quest mechanics. Hunting Log and general side quests remain intentionally unavailable pending their own Nexus-owned policies. Every additional computer must install Nexus and run **Migration > Set Up This Computer** before disabling predecessor plugins so its own local configurations can be preserved; current automated importers cover NavPlotter and AutoDuty operations, while the remaining predecessor-specific settings importers are still pending.

Verification evidence for 0.1.0.40: Nexus source `1f827816bfd7aeae0f865d54b0aeebdd2b91e363`, Daily Pilcrow release `c364c57f39418e3319f48ee2849cea9ba911b963`, and Daily Pilcrow verification docs `c913d0ada021c199796e683ad482bba8d7bcacc2` are pushed. All 230 Nexus tests, the zero-warning clean Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_3dBrCnqaBdnd4QKzXFCcGqRhhyRZ` and final documentation deployment `dpl_ECRAi73YcK8Fcev3SqzKcbEvwRUP` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.40; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `39E85ACEAD5968D8A5891AB70C64BB6BCA9FA5E7A93AF7E1D4F88A97569DC0EA` / `5EBC1C17CE4DE573FCEDF0698C80D9ED5FCC342B4A7E51F3DF5EB3E9A395A664`. GitHub Actions Discord workflow `34595555614` completed successfully. Nexus now owns ordinary general side-quest classification, current-job eligibility, exact selection, durable execution identity, completion verification, bounded pathless-candidate fallback, and replanning while stock Questionable supplies only the selected quest's mechanics. Hunting Log remains the next distinct Nexus-owned target/combat boundary. Friend/second-computer setup remains the final rollout step after Nexus is complete, not a current development gate.

Verification evidence for 0.1.0.41: Nexus source `36dcad16677768342c578da4eda6db6342ce18b3`, Daily Pilcrow release `7eeceaa929bfda020873f725ac75333568336e74`, and Daily Pilcrow verification docs `39dfd266d238922cffae7811a618fb470783caa3` are pushed. All 232 Nexus tests, the zero-warning clean Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_HsXVLJUFsNyeo8B6j91kuNYq9y1Z` and final documentation deployment `dpl_AGGr6NvDhuhAQ3BayDXFjzPYaFTg` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.41; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `57E510FFB85168241261D9A7C22044ECCCB8A91BD1FF672CFD5545B8FF50B7CF` / `58D569BAE0A4854A6564A5FC189F132483D129E96AF76875E8BA095F6CB5837C`. GitHub Actions Discord workflow `34597116626` completed successfully. The real explicit-null operations configuration no longer throws from startup or Migration rendering. Hunting Logs, Aetherytes, Aether Currents, world exploration, achievements, and the unified Progress Atlas remain required Nexus-owned VieriCodex migrations before final rollout.

Verification evidence for 0.1.0.42: Nexus source `aa502eadda446003c1dd0df287f9ab500a8e16f2`, Daily Pilcrow release `370e1527e20cb972be215cfc8d89ba82cc8ad93e`, and Daily Pilcrow verification docs `4f6d0b168b37949a06a08ba19548a3e7a36b7b7a` are pushed. All 239 Nexus tests, the zero-warning clean Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_8JoccyNuK1Muu6rtm5eYtiqV2omu` and final documentation deployment `dpl_Hy98TYbQADfUUpBbGS4UGmaz48pj` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.42 exactly once; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `23E6E1260B8E8405084D17B10EBD849B496DF110CA35ED377BDA1E5986A73F3F` / `DD99ACB7BCFDD02405257277157C294DA50268C8D8F1BB883D57C449E51A54E9`. GitHub Actions Discord workflow `34598685570` completed successfully. Nexus now owns live character completion for Aetherytes/Aethernet, all Aether Currents, and achievements without VieriCodex; every visible current-job label uses the localized full name and abbreviation. Hunting/Grand Company Logs and world exploration remain the next Atlas and execution slice.

Verification evidence for 0.1.0.43: Nexus source `a993710552360b88b6032ed6ad488b31d7de9e98`, Daily Pilcrow release `eccc72a8ee2f04f5cea3689799aa439f7ea4aa28`, and Daily Pilcrow verification docs `bfb9f78dc4d04c25575fcfb356c0ed2f5c34a4e2` are pushed. All 251 Nexus tests, the zero-warning clean Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_BhUZV1nUxVTmoiHwRzAwKH5CUCBp` and final documentation deployment `dpl_C1RRL8ckvsdN4G1eGLXarJR7MViF` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.43; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `A0C8711924C1751E69D8E4C69EC4043EAFC6D4F43DAB6B05B126332B33C52928` / `61A6A7F9EC0B286C1179CD7D741EB37E61695D7CB2A98715FD35A0B600D025DE`. GitHub Actions Discord workflow `34600837053` completed successfully. Nexus now owns Mapping/Remapping exploration completion, the complete 12-log/666-target Hunting and Grand Company catalog, exact live per-target kill progress, current incomplete-target display, and provider-neutral deterministic next-target selection.

Verification evidence for 0.1.0.44: Nexus source `4d6ad5bfc40c560177d65312f4e9b9532f68e875`, Daily Pilcrow release `a6b016d0d56d365c127f6ca7259e984ffe1d1f7b`, and Daily Pilcrow verification docs `7a12230eb578e6a1797b9b583c2b6a1f93048bba` are pushed. All 254 Nexus tests, the zero-warning clean Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_6xExFUgzoh7qHHr1hrtitx9AWWvJ` and final documentation deployment `dpl_CmgtP3KvYyWtEnPo8eabyHh8ito2` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.44; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `F8EF0C65F8EEEA8D2843D362731370DF2E675EA14A2A3AEB5C843A629CB1B9A6` / `B2075B3250813304526E13C49B05BA213EFE7B358C10201C2D93456E1D509488`. GitHub Actions Discord workflow `34609438619` completed successfully. Nexus now owns exact open-world Hunting Log target selection, camp travel/search, combat approach, repeated kills, exact live completion verification, Stop, recovery, and restoration of prior Boss Mod state without calling VieriCodex.

Verification evidence for 0.1.0.45: Nexus source `6a8a128b6405b801828a32cfca4e4420211bc5c2`, Daily Pilcrow release `57e3774671e344127143c6399871c09cd0bedb90`, and Daily Pilcrow verification docs `67520e1c17050240de34b3daaaf5e2d5b5392ec0` are pushed. All 255 Nexus tests, the zero-warning clean Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_8ddEctE7hvM1hejhNPV3dHRXgxiX` and final documentation deployment `dpl_D3TfiNP7zwHR2F8cS6Uodry7EqmK` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.45; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `3E8AD7A73B770730DB3D1324C9409603784BBB2E271895FA979D5B3AAF6EF9D8` / `879D4AC3D3601D46513590C537E88CBC8680CA5379A25CDA64E8815DDCFD1C27`. GitHub Actions Discord workflow `34611393626` completed successfully. Nexus now owns one-click bounded travel-node attunement, field-current collection, and world-region exploration with exact live completion verification and without calling VieriCodex.

Verification evidence for 0.1.0.46: Nexus source `fc5860d4a8502d588685aac346778735174b4b57`, Daily Pilcrow release `8188b330e7ccd7de9bf71acf046489f89259eeb0`, and Daily Pilcrow verification docs `6f59ae6a2b9c7e76a513f45e8e6562b82058c8fe` are pushed. All 257 Nexus tests, the zero-warning clean Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_8JkCYgBqU7zTfmYwMvz9T1ykm9aa` and final documentation deployment `dpl_BhB1QpsveNX9VLPHMtEfqQh9qxHf` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.46; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `33B1BE077528C7854EB5EFA2829EB8EE3BBE28346A7944D0E1FDE46A2B8401B8` / `608E4091A780D7B3DE57AEA4053D49A114B1760EE66E88E43E521AF9766E15CD`. GitHub Actions Discord workflow `34617703795` completed successfully. Nexus now owns one-click exact quest-earned Aether Current execution and exact duty-only Grand Company target dispatch/verification, with confirmed-inactive Stop reconciliation for both provider paths.

Verification evidence for 0.1.0.47: Nexus source `83d3b6b80914b13c099e1e47828e8f27b50d71c2`, Daily Pilcrow release `402eaa1e08fd5247100c4d479a6cc7599b1fc5ef`, and final Daily Pilcrow documentation `70c8030bc3f76976d0d4a7f724554304aecc5687` are pushed. All 261 Nexus tests, the zero-warning clean Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_3SqiWE8oMW7nV5MkPwo8Brien4WZ` and final documentation deployment `dpl_5fGPMTfDF3nHAb42obYFLKJnYGx5` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.47; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `FF96D3DC96E14852541C1EAFA776C0575E06275B02FEB5AD13EF75FA3D6BDF1D` / `59AD9C10EC921C2E5E709BE4C3E7137918A59E16D43F13B6EB76E6145113A669`. GitHub Actions Discord workflow `34641520725` completed successfully. Nexus now owns the protected between-duty item-transaction boundary while AutoRetainer and Glamour Log remain narrow replaceable mechanics providers and AutoDuty remains absent from maintenance.

Verification evidence for 0.1.0.48: Nexus source `0b7cecd86992299e72966b0a877a98f9966a7d16`, Daily Pilcrow release `623032b3b9caf54aac44c296abfa75e6896ad97d`, and final Daily Pilcrow documentation `abb1e6c7b7051d7a7654ff2771bf9fc4aa4a3023` are pushed. All 261 Nexus tests, the zero-warning clean Release build, all 205 website tests, typecheck, focused package validation, thirteen-entry inventory guard, and production website build pass. Release deployment `dpl_6wWUzHkcLzdqagXBNbkZ39QGy7oG` and final documentation deployment `dpl_AoWhHJZ2iZAbbDRA7X6tkyPVz5Ko` are Ready on `https://thedailypilcrow.com`. The live feed advertises VieriNexus 0.1.0.48; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `29B89BC937C7EDDDACEAE016C43627A570C467C84D767465353657BE4C772123` / `FA958FADBF0E46ED02CEE97BAEE24AA8D8F59A689862BC812154936DF054B09B`. GitHub Actions Discord workflow `34644886958` completed successfully. Nexus now owns the full destination/travel/interaction prerequisite around Grand Company and collection-storage mechanics, including verified empty-set completion and final storage rechecks; AutoDuty remains absent from maintenance.

Verification evidence for 0.1.0.59: Nexus source `7faf4cd5faad282a13e747f386c5195e91f44e67`, Daily Pilcrow release `6d938c60b11fdae6cc6eb8dc7e061eea3acff072`, and final Daily Pilcrow verification documentation `9f6033e35281d5e2704940e242ceeaa9bc168917` are pushed. All 320 Nexus tests, the clean zero-warning Release build, focused package validation, all 205 website tests, typecheck, the thirteen-entry inventory guard, and the production website build pass. Release deployment `dpl_13s74CPfaz96ckdVm3XH3uKsWkaU` and final documentation deployment `dpl_Buu1tuhUXmv4FPBDjqKEzQwCh9bj` are Ready. The live feed advertises VieriNexus 0.1.0.59 exactly once; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `F428F781D124A34B73F76007AEF7F262C00AE23BE719BC4902B2125D02811CEB` / `C3875E339ED24C88B3DD695AF96CD6AC52578398CE633CB0D129A99865DDDF6E`. GitHub Actions Discord workflow `34728132786` completed successfully. Nexus now owns the complete multi-job queue, uses required Fast Job Switcher commands with exact job confirmation, and has retired VieriCodex from all quest runtime selection and IPC.

Verification evidence for 0.1.0.60: Nexus source `9ab70eaa8e00ae7866738432b3f97e8d3521428e`, Daily Pilcrow release `5adaeb22669132e8d6205b90aead0221db644189`, and final Daily Pilcrow verification documentation `4642e9b58d91168e1cf49d3c13764e00873a13ec` are pushed. All 323 Nexus tests, the clean zero-warning Release build, focused package validation, all 205 website tests, typecheck, the thirteen-entry inventory guard, and the production website build pass. Release deployment `dpl_EZ4cHHPrmNrigEGxjP3pNxuiHxhD` and final documentation deployment `dpl_3iLHqwPSg42jjCKekBBfaJGWJk1k` are Ready. The live feed advertises VieriNexus 0.1.0.60 exactly once; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `86043D2057B3411286A53990E5381E4CAF688075D133D54BB9E859BF9CA22B28` / `57335E216C4228FFDD5088A4A2D0943094DB000AF5AF520F2CFF9C79F6CE3C18`. GitHub Actions Discord workflow `34732939709` completed successfully. All player-facing class/job labels now use proper title-cased names and uppercase abbreviations while provider-only Fast Job Switcher commands remain lowercase.

Verification evidence for 0.1.0.61: Nexus source `045ddf52cc7b2909be3aeb4eaf513119ca6e5cc6`, Daily Pilcrow release `929b1e128a3e9b1e72982963b7cd2268c30c2f6d`, and final Daily Pilcrow verification documentation `d6fa76f5f772fb372a61c0c54efd437683b97f68` are pushed. All 324 Nexus tests, all focused imported-engine suites, the five-archive package verifier, all 205 website tests, typecheck, the thirteen-entry inventory guard, production build, and focused local/live package validation pass. Release deployment `dpl_BVV59pZhHvrSByCD5uaY4TsEuSXe` and final documentation deployment `dpl_BeXnh9Ru2s2xNwG4ZXZytE5ZGbXM` are Ready. The live feed advertises VieriNexus 0.1.0.61 exactly once; public runtime/source downloads return HTTP 200 as valid ZIPs and match SHA-256 `6C3004725313E6448A5AB2EB8824DA7DC6AF94B6F84F36E8948379A4895A11C2` / `02946628EFDAA753C58B19F8F6B9F6AA8BAD1A52B1D4B91747988D795BBB2D10`. GitHub Actions Discord workflow `34735320942` completed successfully. The remaining user-side gate is one broad in-game settings/runtime handoff before any predecessor entries are deliberately retired from the feed.

### 18.16 Release report format

For any published release, the final Codex response should explicitly report:

- **Version:** intended published version.
- **Source commit:** exact commit SHA.
- **Tests:** passed/failed/not run with scope.
- **Build:** verified/not verified.
- **Runtime package:** path/name and validation status.
- **Source package:** path/name and validation status when applicable.
- **Hashes:** algorithm + values/status if part of process.
- **Git push:** remote/branch/commit verified or not.
- **Daily Pilcrow artifact upload:** verified or not.
- **Live feed:** URL + expected version verified or not.
- **Dalamud discovery/install/update:** verified, partially verified, or not verified.
- **Discord release bot/changelog:** sent/verified, failed, intentionally skipped, or not authorized.
- **`PROJECT_STATE.md`:** updated/committed/pushed status.
- **Known remaining issues:** anything preventing full release confidence.

This prevents a vague “done” from hiding an incomplete production step.

## 19. Durable Project-State Maintenance

`PROJECT_STATE.md` is permanent project infrastructure, not a historical snapshot that may be allowed to rot.

### 19.1 Update trigger

Every substantial task must ask: **did this change durable project truth?**

If yes, update this file in the same task. Durable project truth includes changes to:

- product vision/scope;
- architecture/layer boundaries;
- module responsibilities;
- current implementation state;
- classes/interfaces/services/contracts/IPC;
- dependencies/provider requirements;
- source pins/upstream provenance;
- migration state/parity/retirement status;
- configuration/persistence schemas;
- user/character/secret boundaries;
- automation ownership/recovery/safety rules;
- UI/UX decisions and regression guardrails;
- known bugs/reliability incidents/workarounds;
- tests/acceptance matrices;
- technical debt that materially affects future work;
- roadmap priorities/status;
- release version/current commit;
- Git/release/distribution infrastructure;
- package/feed/website/Dalamud update mechanics;
- release Discord-bot/changelog mechanism;
- current workstream and recommended next steps.

Do not require the user to remind Codex to update this file.

### 19.2 What not to record

Do not turn this file into a raw diary. Exclude:

- routine command transcripts;
- giant diffs;
- temporary debugging chatter;
- build logs;
- ephemeral token/context information;
- secrets/credentials;
- transient plans that were immediately abandoned unless the rejection itself is an important design decision.

Record durable facts, decisions, invariants, current status, and why important constraints exist.

### 19.3 Source-of-truth hierarchy

For future threads:

1. **Current implementation:** repository code/config is authoritative.
2. **Current production distribution:** live feed/artifacts plus authoritative website/release source are authoritative.
3. **Product intent/history/regressions/roadmap:** this file is the durable handoff unless a later explicit user decision supersedes it.
4. **Target architecture:** `MASTER_ARCHITECTURE_PLAN.md` plus this file; planned architecture is not current implementation.
5. **Predecessor behavior:** exact pinned predecessor source + regression requirements.
6. **Git state/history:** authoritative evidence for what was actually committed/pushed.

When sources disagree, investigate and reconcile rather than silently choosing whichever is convenient.

### 19.4 End-of-task state discipline

At the end of a substantial task, before final reporting:

1. inspect the final diff;
2. update current implementation/status sections in this file;
3. update version/commit/release facts when applicable;
4. update roadmap items from PLANNED to IMPLEMENTED only when code proves it;
5. add new regression guardrails discovered during debugging;
6. update source pins when predecessor behavior changed;
7. update release/distribution details if tooling changed;
8. update current workstream/recommended next steps;
9. remove or supersede stale statements that now contradict reality;
10. ensure no secret was written;
11. validate that a fresh Codex thread could continue without the current chat.

### 19.5 Thread lifecycle recommendation

Do not intentionally keep one Codex development conversation alive indefinitely.

Preferred model:

- repository + durable docs hold project memory;
- each thread handles a coherent workstream;
- substantial work updates this file;
- when a thread becomes large/noisy or the workstream changes materially, start a fresh thread using this file and repository state.

This reduces dependence on model context and prevents project knowledge from being trapped in one chat again.

## CONTEXT THAT MAY STILL BE LOST

- The recovered predecessor session is `019fcfdd-805d-7ae3-a1c1-cecda2939642`, titled **“Add movable MarkerIcon overlay.”** The title reflects its first task, not its final VieriNexus work. It ran from 2026-08-05 through the last successful response at 2026-09-08 05:01 UTC, with failed compaction attempts later that day.
- The session used one rollout file: `C:\Users\curci\.codex\sessions\2026\08\04\rollout-2026-08-04T23-00-27-019fcfdd-805d-7ae3-a1c1-cecda2939642.jsonl`. It contained eight internal compaction records/windows early in the month, but no second rollout file was found for the same session.
- The JSONL contained one malformed/partially invalid metadata line near the final failed-compaction attempts. The last successful user and assistant messages, repository changes, release results, and four terminal context-window errors were still readable.
- Compaction payload summaries were encrypted and not directly readable. However, the rollout retained user-visible messages, assistant conclusions, tool-visible results, repository references, and the durable architecture/status/policy documents. Private chain-of-thought was neither needed nor reconstructed.
- The thread covered many FFXIV projects before VieriNexus. This recovery preserved Nexus-relevant requirements and the behaviors Nexus must absorb, but it did not attempt to turn every unrelated historical plugin conversation into Nexus implementation truth.
- The three UI inspiration images and original architecture brief were still locally available and were inspected during recovery. Their exact intended pixel-level behavior was not specified; they are visual direction, not a binding layout.
- At recovery there was no user reply confirming the 0.1.0.3 route import/rollback. The user later confirmed Dalamud installation/update, responsive text, a successful staging import of settings with zero personal routes, window reopen persistence, and a reload-only message loss. The scoped Nexus route/receipt/configuration files were then inspected to diagnose that report; they proved the staged data survived and that the current VieriNavPlotter source plus timestamped backup match the recorded SHA-256. No unrelated or protected VieriLink configuration was opened. Version 0.1.0.5 restores the staged message from the verified saved receipt, and the user confirmed that disable/re-enable recovery. The user then confirmed guarded rollback/re-import and correct Routes-page state transitions on 0.1.0.6. A non-empty route fixture remains pending; 0.1.0.6 adds read-only inspection/IPC over the verified snapshot, and 0.1.0.7 corrects its last clipped settings row.
- The transcript says 0.1.0.3 and its website/Dalamud/source/runtime/Discord release were successfully published and verified. The user subsequently supplied the authoritative custom Dalamud repository URL `https://www.thedailypilcrow.com/dalamud/pluginmaster.json`. Although the low-level release details were not fully preserved in the recovery transcript, the 2026-09-08 foundation intake independently verified the website source paths, archive naming/generation, feed update/deployment mechanism, SHA-256 workflow, public validation, and safe Discord release-bot invocation from the real infrastructure; section 18.15 now records those durable facts.
- The exact coexistence/deprecation duration for old plugins remains undecided.
- SQLite is the architectural recommendation for durable orchestration state but was not explicitly approved or implemented.
- The exact first proof goal (proposed Viper current level +2), manual-override resume UI/authorization mechanism, broader Custom UI default-enabled set, large content-data update mechanism, and remote-command allow/deny lists still require explicit product decisions before their corresponding implementation.
- No `AGENTS.md` existed in the VieriNexus repository at recovery. Future threads must still check again.
