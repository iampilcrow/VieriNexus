# PROJECT STATE

Recovery snapshot: 2026-09-08 (America/New_York)  
Repository: `D:\FFXIV Plugins\VieriNexus`  
Current product version: `0.1.0.21`
Current Git state at recovery: `main`, `HEAD ecaa8c7`, synchronized with `origin/main`, clean before this file was added.

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

The current production line is an active migration foundation. Nexus can be installed alongside existing Vieri products, transactionally imports VieriNavPlotter into immutable staging, explicitly creates a separate Nexus-owned working library, records and edits personal routes, previews saved and generated paths, and performs guarded local or delegated cross-zone travel after manual source unload and explicit session authority. Version 0.1.0.20 also captures exact route targets without implicit approval and lets VieriAutoDuty consume an enabled Nexus vendor override through a fail-closed versioned contract with transition fallbacks. Version 0.1.0.21 keeps fresh-route creation visible after the first route exists. It does not yet replace VieriNavPlotter completely or enable other gameplay modules.

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
- VieriCodex remains authoritative during Progression migration. Nexus will own its Vieri-specific planners, policies, safety fixes, custom-route overlay, and UI, then use stock Questionable through a narrow capability-versioned adapter for ordinary supported quest execution. The full fork retires only after parity and fallback validation.
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

At startup, `Plugin.Plugin()` loads `Configuration`, creates `DependencyService`, `LegacyConfigurationInventory`, `NavigationMigrationService`, the Nexus working-library, recording, preview, guarded execution, authority, recovery, diagnostics, neutral built-in `ModuleRegistry`, `WorldStateStore`, `WorldSnapshotObserver`, `NexusWindow`, and `NexusIpcProvider`; registers `/vierinexus` and `/nexus`; and attaches Dalamud UI callbacks.

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

Several target concepts already have types or tests but are not general live subsystems. In particular, `NexusGoal`, `NexusTask`, `SoloDutyCombatPolicy`, and `NexusCommandDto` remain foundation contracts/primitives. The plugin now instantiates `ResourceLeaseManager`, a navigation-only planner/recorder/executor, reload/watchdog reconciler, and bounded session transition audit. It does not yet instantiate the general goal planner, scheduler, cross-module executor, command gateway, durable general audit log, or SQLite store, and it does not register the declared command IPC endpoint.

## 3. Repository Map

### Root

- `VieriNexus.slnx` — solution containing the four production projects and the application test project.
- `Directory.Build.props` — common `net10.0-windows`, latest C#, nullable, implicit usings, warnings-as-errors, deterministic builds.
- `README.md` — concise current-release description and safety guarantees.
- `IMPLEMENTATION_STATUS.md` — detailed implemented/not-enabled status and pinned predecessor behavior.
- `MASTER_ARCHITECTURE_PLAN.md` — authoritative target architecture, migration sequence, milestone, risks, and non-relaxable rules. Treat its architecture as planned unless code proves otherwise.
- `docs/DEPENDENCY_AUDIT.md` — required/recommended provider inventory and present-day Vieri consumer evidence.
- `docs/MIGRATION_AND_UPSTREAM_POLICY.md` — non-negotiable migration, safety, route, gear, market, rotation, and upstream-update guardrails.
- `upstreams/source-lock.json` — exact pinned commits for all nine migration-source repositories plus upstream provenance where applicable.
- `.gitignore` — excludes build and IDE output (`bin/`, `obj/`, `.vs/`, `dist/`, `artifacts/`). Generated folders may exist locally but are not architecture or source.

### `src/VieriNexus.Domain`

- `Identifiers.cs` — `GoalId`, `TaskId`, `AttemptId`, `CharacterKey`, `GoalKind`, `TaskKind`, `CapabilityId`, and `ProviderId`. `CharacterKey` uses content ID plus home world and formats as hexadecimal content ID plus world ID.
- `Goals.cs` — `GoalStatus`, `ConstraintStrength`, `GoalConstraint`, and versioned `NexusGoal` desired-state record.
- `Tasks.cs` — `NexusTaskStatus`, `FailureKind`, `ResourceKind`, `TaskFailure`, and `NexusTask`.
- `WorldSnapshot.cs` — `KnowledgeState`, generic `Observed<T>`, session/character/provider slices, and immutable revisioned `WorldSnapshot`.
- `Modules.cs` — `ModuleDescriptor` and current minimal `INexusModule` registration contract.

### `src/VieriNexus.Application`

- `DependencyCatalog.cs` — all six required and sixteen recommended external dependency descriptors.
- `ModuleRegistry.cs` — case-insensitive, duplicate-rejecting module registry.
- `ResourceLeaseManager.cs` — atomic in-memory acquisition, implied-resource expansion, lease heartbeat/expiry, exactly-once watchdog expiration drain, snapshot, and `IDisposable` release.
- `NavigationStopCoordinator.cs` — provider-neutral, idempotent verified-Stop state machine that retains a tracked execution lease unless movement is explicitly confirmed inactive.
- `ManualMovementSafetyCoordinator.cs` — monotonic, fail-closed manual-takeover state machine with start inhibition, quiet-period handling, verified Stop integration, and explicit-resume latching.
- `NavigationExecutionIntentStore.cs` — minimal versioned execution/route/lease/state journal with validation and same-directory atomic replacement; no resumable instruction pointer.
- `NavigationExecutionSafetyCoordinator.cs` — every-draw reload/shutdown and lease-expiry reconciler that can Stop and checkpoint but cannot replay movement.
- `NavigationAuthorityCoordinator.cs` — reversible session-only authority approval, atomic Navigation/Movement availability probe, source/safety revocation, and the sole future execution-entry gate that acquires resources and arms no-replay Stop before any provider call.
- `NavigationDiagnosticsMonitor.cs` — pure bounded session monitor that records provider state/code transitions without per-frame audit flooding.
- `NavigationSafetySimulator.cs` — isolated memory-only six-scenario exercise of the production navigation safety coordinators, including provider loss/retry; its provider has Stop observation only and no movement operation.
- `NavigationRecoveryCoordinator.cs` — combines durable stopped-intent and manual-yield latches into one explicit acknowledgement gate that requires confirmed Stop, released ownership, and elapsed input quiet period; it cannot resume movement.
- `NavigationLibraryStore.cs` — atomic Nexus-owned working-library persistence, validation, and previous-file recovery copy, deliberately separate from immutable migration staging.
- `NavigationRoutePlanner.cs` — side-effect-free Review, Travel to Start, and Playback planning with ordered points, distance, validation, and territory gating.
- `NavigationBuiltInRouteCatalog.cs` — immutable 27-route catalog of verified VieriAutoDuty vendor standing points with target/territory provenance and disabled assignments.
- `NavigationRouteTargetBinding.cs` — target/kind binding policy that never enables an override implicitly and rejects cross-territory capture for populated routes.
- `NavigationSuiteRouteRequest.cs` — additive JSON suite-travel request plus a separate 27-target NPC fallback catalog that never replaces authored movement points.
- `NavigationSuiteTravelCoordinator.cs` — provider-neutral tracking for cross-zone travel explicitly dispatched by the current Nexus process; unrelated provider activity is never adopted, drawn, or stopped.
- `NavigationRouteOverrideResolver.cs` — pure exact-target assignment editor/resolver with one-winner enforcement and fail-closed invalid/ambiguous results.
- `NavigationRouteRecordingCoordinator.cs` — non-moving timed-capture state machine with cadence, spacing, territory, and route-availability guards.
- `NavigationRouteClipboardCodec.cs` — bounded versioned route exchange plus compatible legacy NavPlotter JSON ingestion with regenerated identity and disabled automation assignment.
- `NavigationRouteExecutionCoordinator.cs` — guarded same-zone movement entry, lease heartbeat, natural completion, explicit Stop/manual interruption handling, and shared-provider destination ownership yielding.
- `WorldStateStore.cs` — current immutable snapshot reference plus change event and monotonic revision check.
- `SoloDutyCombatPolicy.cs` — preserved provider-neutral policy for the VieriCodex 1.12.2.74–76 targeting incident.
- `MigrationModels.cs` — route-library snapshots, issues, receipts, write results, and verified staged-state read results.
- `NavigationRouteMigrationImporter.cs` — deserializes and validates the VieriNavPlotter JSON shape and maps every supported field.
- `TransactionalMigrationStore.cs` — timestamped source backup, prior-target backup, atomic Nexus write, SHA-256 receipt, guarded rollback, and hash-verified staged-state reload.

### `src/VieriNexus.Contracts`

- `PublicContracts.cs` — contract version 1, global status/dependency names, the declared-but-unregistered command endpoint, six read-only `VieriNexus.Navigation.V1.*` endpoints including exact gear-vendor override resolution, immutable DTOs, and stable navigation JSON serialization.

### `src/VieriNexus.Plugin`

- `Plugin.cs` — Dalamud entry point/composition root, command registration, draw lifecycle, setup/open behavior, and disposal.
- `Configuration.cs` — global presentation/setup settings, character-scoped safety settings, and per-source migration state.
- `VieriNexus.Plugin.csproj` — `Dalamud.NET.Sdk/15.0.0`, version `0.1.0.21`, assembly/internal root `VieriNexus`.
- `VieriNexus.json` — Dalamud API level 15 manifest, author `Valentina Vieri`, permanent internal name `VieriNexus`.
- `Assets/VieriNexusLogo.png` — permanent Home hero artwork.
- `Services/BuiltInModuleCatalog.cs` — nine neutral module registrations and capability identifiers.
- `Services/DependencyService.cs` — installed-plugin detection and focused Dalamud Plugin Installer actions.
- `Services/GameplayReadyGate.cs` — post-login/zone stable-world gate.
- `Services/LegacyConfigurationInventory.cs` — read-only path discovery for nine predecessor sources; VieriLink is marked protected.
- `Services/NavigationMigrationService.cs` — source location, cached preview, import/rollback orchestration, Nexus storage paths, saved-receipt recovery across plugin reloads, and in-memory access to the verified staged snapshot.
- `Services/NavigationActivationService.cs` — composition of working-library readiness, installed/loaded source ownership, dependency readiness, Navigation/Movement lease state, verified Stop, manual-yield readiness, reload/watchdog readiness, and session-only authority approval into the pure activation policy.
- `Services/VnavmeshNavigationStopProvider.cs` — provider adapter that requests `vnavmesh.Path.Stop` and independently observes `vnavmesh.Path.IsRunning` for verified completion.
- `Services/GameManualMovementInputSource.cs` — reads FFXIV's configured movement actions for remapped keyboard, mouse-steer, gamepad, jump, and autorun intent.
- `Services/ManualMovementSafetyService.cs` — character-scoped runtime composition of the input source, configured protection/quiet period, and pure manual-yield coordinator.
- `Services/NavigationDiagnosticsService.cs` — live mapping of navigation providers, predecessor/authority state, resource ownership, and safety coordinators into the shared provider-health snapshot and transition audit; owns the isolated simulator result.
- `Services/NavigationRecoveryService.cs` — character-aware runtime composition of the no-replay checkpoint gate and manual-input quiet period for the conditional Routes-page acknowledgement panel.
- `Services/NavigationLibraryService.cs` — promotes verified staging into a separate working file and provides atomic route creation/edit/save/delete operations without touching the predecessor or receipt.
- `Services/NavigationRoutePreviewService.cs` — persistent current-territory world drawing for explicitly selected Nexus route plans.
- `Services/NavigationLivePathService.cs` — generated vnavmesh waypoint overlay gated to current-process Nexus local or delegated suite ownership.
- `Services/AutoDutyRouteTravelProvider.cs` — capability-checked adapter over the existing public AutoDuty suite-travel/Stop/visualization contract.
- `Services/NavigationRouteRuntimeService.cs` — plugin-facing planning, static/live preview, guarded local execution, delegated cross-zone execution, Stop, and per-frame runtime composition.
- `Services/NavigationRouteRecordingService.cs` — live position observation and atomic capture persistence around the provider-neutral recording coordinator.
- `Services/NexusIpcProvider.cs` — registered read-only status, dependency, and Nexus-namespaced navigation IPC.
- `Services/WorldSnapshotObserver.cs` — throttled Dalamud client/player/object/condition observation plus the current read-only provider-health snapshot.
- `UI/NexusWindow.cs` — entire current shell and pages.
- `UI/NexusTheme.cs` — dark/red/gold ImGui theme and shared status/section helpers.

### `tests/VieriNexus.Application.Tests`

There are 133 automated tests across:

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
- `NavigationBuiltInRouteCatalogTests.cs`
- `NavigationRouteOverrideResolverTests.cs`
- `NavigationRouteRecordingCoordinatorTests.cs`
- `NavigationRouteTargetBindingTests.cs`
- `NavigationSuiteRouteRequestTests.cs`
- `NavigationSuiteTravelCoordinatorTests.cs`
- `NavigationSafetySimulatorTests.cs`
- `ResourceLeaseManagerTests.cs`
- `SoloDutyCombatPolicyTests.cs`
- `TransactionalMigrationStoreTests.cs`
- `WorldStateStoreTests.cs`

The 0.1.0.21 source passes all 133 tests plus a zero-warning full plugin build.

## 4. Major Systems and Features

### 4.1 Dalamud lifecycle and commands — IMPLEMENTED

`src/VieriNexus.Plugin/Plugin.cs` registers:

- `/vierinexus`
- `/nexus`
- Subcommands: `home`, `splash` (alias of Home), `show`, `hide`, `dependencies`, and `migration`.

Dalamud's Open Main UI and Open Config UI callbacks open the appropriate page. Disposal unregisters callbacks, removes both command handlers, unregisters IPC, removes windows, and saves configuration.

Safeguard: no Nexus window is drawn until the gameplay-ready gate passes. On first eligible session, first-run users are taken to Dependencies; returning users optionally open Home when `OpenOnLogin` is enabled.

### 4.2 Dependency setup gate — IMPLEMENTED

`NexusDependencyCatalog.All` defines six required and sixteen recommended integrations. `DependencyService.Snapshot()` matches Dalamud `InstalledPlugins` by internal name and currently reports only `Missing`, `Disabled`, or `Healthy`. `OpenInstaller(...)` opens Dalamud's installer focused on the exact install/manage search.

`NexusWindow.PreDraw()` locks non-setup pages when first-run setup is incomplete or any required provider is not loaded. The Dependencies page shows progress, separates Required and Recommended, and enables **Continue to Vieri Nexus** only when all six required providers are healthy.

Limitations:

- The richer planned states (`Incompatible`, `Starting`, `Degraded`, `Faulted`), version ranges, repository-configuration checks, IPC health, and per-module dependency gating are not implemented.
- The six providers are globally required by the foundation gate even though live modules are not active.
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
| `command-center` | Command Center | `vieri.capability.command.invoke/v1` |
| `navigation` | Routes & Navigation | `vieri.capability.navigation.route/v1` |

The registry prevents duplicate IDs case-insensitively. These are display/registration descriptors only; every current module page says the predecessor remains authoritative.

### 4.4 World observation and readiness — IMPLEMENTED FOUNDATION

`WorldSnapshotObserver.Update(long now)` samples at most every 250 ms and publishes:

- login/area transition/player availability/territory;
- observed character key, name, class/job row ID, level, and combat state;
- current navigation provider/safety health for vnavmesh Stop, manual movement observation, reload/watchdog state, predecessor ownership, Navigation/Movement ownership, and Nexus authority.

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

`ManualMovementSafetyCoordinator` gives physical player intent priority over Nexus movement. The runtime input source uses FFXIV's configured movement action IDs, so remapped keyboard movement, two-button mouse steering, gamepad movement, jump, and autorun are covered without treating camera or ordinary UI input as takeover. Input blocks starts through the configured quiet period. During tracked navigation it latches verified Stop, keeps retrying while Stop is unconfirmed, and never auto-resumes; the explicit stopped-intent acknowledgement can clear the latch only after movement is stopped and input remains quiet. Disabling character automation or movement protection during tracked navigation also fails closed and stops.

The manual observer and route execution monitor run each plugin draw even when the Nexus window is closed.

`NavigationExecutionSafetyCoordinator` connects durable movement intent, reload/shutdown reconciliation, active lease expiry, normal completion, and superseded-provider yield. Before provider movement, `BeginExecution` atomically saves schema/execution/route/lease identity and state, then registers the lease with verified Stop; it deliberately persists no instruction pointer. Reload Running/StopPending intent can only request Stop, confirm inactivity, and enter `AwaitingExplicitResume`. It cannot replay movement. Missing-provider, rejected-Stop, active/unknown movement, corrupt-journal, and unwritable-journal outcomes remain fail-closed. `Superseded` records independently verified replacement by another provider without calling global Stop.

The safety coordinator sweeps lease expiry every plugin draw, including while the UI is closed. A missed tracked Navigation/Movement heartbeat persists StopPending, invokes the same verified Stop coordinator, and stays blocked until inactive confirmation. Plugin disposal also arms StopPending before requesting Stop. The current build never creates such an execution; the implementation and disk journal are testable readiness foundations only.

### 4.6 Goal/task/failure contracts — IMPLEMENTED AS DOMAIN TYPES ONLY

`NexusGoal` is a versioned, character-scoped desired-state record with constraints, priority, lifecycle, plan revision, and status detail. `NexusTask` is a bounded unit with capability, selected provider, required resources, lifecycle, payload, and structured failure. Stable string-backed kinds/capabilities/providers avoid a global enum that every module must edit.

There is no current goal repository, goal builder, planner, task graph, executor, reconciler, or task persistence.

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

### 4.9 Routes & Navigation transactional migration — IMPLEMENTED, STAGING ONLY

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

The Routes page preserves the verified staged snapshot recovered from the saved receipt or produced by import as immutable evidence. An explicit action creates a separate Nexus-owned working library in `NexusData/navigation-library.v1.json`; each atomic replacement retains `.previous`. With zero routes, the user can create one at the live character position, import compatible route JSON, or copy one of 27 immutable verified gear-vendor templates. Template copies receive a new identity, remain independently editable, and begin with their assignment disabled. With routes present the page supports search and persisted selection; editable names/tags/notes/movement settings; explicit exact territory/vendor override enablement with atomic one-winner enforcement; timed observation-only recording with configurable interval and spacing; add/replace/reorder/remove/undo/reverse points; duplicate; bounded Nexus/legacy NavPlotter JSON exchange; confirmed point clearing and route deletion; ordered-point inspection; automatically refreshed current-territory world preview; Travel to Start; ordered Play Route; and Stop. Recording/display controls render above the library/editor split. The list and editor contribute their natural content height to the outer Routes page, leaving one page scrollbar instead of nested scroll regions. Destructive confirmations are drawn in the same ImGui ID scope as their triggering buttons so Clear all points and Delete route reliably open their modals. Imports and duplicates receive new identities and cannot enable automation assignment. These operations never write the staged file or live VieriNavPlotter source.

`NavigationActivationPolicy` is a pure fail-closed assessment covering verified staging, installed/loaded source ownership, required dependencies, ownership-service connection, Navigation/Movement lease conflict, verified Stop, manual override, reload reconciliation/watchdog readiness, explicit user approval, and current Nexus execution state. `NavigationActivationService` reports Stop ready only while the vnavmesh adapter is loaded, manual yielding ready only while FFXIV input observation is available and the current character protection setting is enabled, and reload readiness only after the durable journal and active watchdog have completed their first safe observation.

`NavigationAuthorityCoordinator` owns the explicit decision. Nexus never disables or enables VieriNavPlotter; approval is unavailable until the user unloads it manually, creates a working library, and every safety gate is ready. Approval is session-only, is lost on reload, atomically probes the full Navigation/Movement bundle, starts no route automatically, and can be returned to staging. Source reappearance, provider/safety loss, character authorization loss, or an outside resource conflict revokes authority. `NavigationRouteExecutionCoordinator` is now the only live movement entry: it rechecks current territory, manual quiet state, provider readiness, source/safety state, and character automation authorization; atomically acquires Navigation and Movement; journals route/lease identity before movement; registers verified Stop; then invokes vnavmesh. It heartbeats the lease, completes only after inactive observation, and routes explicit Stop/manual takeover/reload/watchdog failure through no-replay recovery.

The shared-provider ownership rule is non-negotiable: while vnavmesh reports movement, Nexus compares the active waypoint-chain destination to its planned final point. If a different plugin replaces the path, Nexus writes the intent as `Superseded`, releases only its internal lease, and does not call global Stop. This specifically prevents Nexus from interrupting VieriCodex, AutoDuty, or another legitimate vnavmesh owner after they take over. Natural Nexus completion also uses observation-only release and sends no redundant global Stop.

`NavigationDiagnosticsService` adds a read-only Provider Health panel and bounded Safety Audit to the Routes page. It observes vnavmesh Stop availability/version, manual movement readiness, reload/watchdog status, VieriNavPlotter ownership/version, outside or tracked Navigation/Movement ownership, and Nexus authority. The same six observations populate `WorldSnapshot.Providers`. Only state/code transitions enter the session audit; it is not durable general task history and contains no credentials. `NavigationSafetySimulator` is user-triggered and runs the actual safety coordinators against isolated memory-only leases and journals plus a provider with no movement method. Its six scenarios cover guarded start/verified Stop, manual takeover, no-replay reload, provider loss/retry, source-owner return, and lease expiry without touching live ownership or the live journal.

`NavigationRecoveryCoordinator` exposes the previously internal explicit acknowledgement as a conditional Stopped Intent Checkpoint panel. It can clear a durable `AwaitingExplicitResume` record and/or manual-yield latch only when no execution remains tracked, Stop has therefore released Navigation/Movement, and any manual-input quiet period has elapsed. The action persists `Completed` for stopped intent and clears the manual latch; it has no route/provider/resume operation and does not approve Nexus authority. Partial acknowledgement failure remains fail-closed.

Critical limitation: migration staging alone provides no authority or active behavior; the user must explicitly create the separate working copy and approve session authority. Built-in vendor templates, current-target capture, exact-target override resolution, filtered generated-waypoint drawing, and cross-zone suite travel are implemented. VieriAutoDuty 1.0.0.438 consumes a Nexus vendor override only while Nexus is authoritative, then falls back to VieriNavPlotter and finally its built-in route. Nexus does not disable VieriNavPlotter or activate duplicate navigation.

### 4.10 Public IPC — PARTIALLY IMPLEMENTED

Runtime registration in `NexusIpcProvider` currently provides:

- `VieriNexus.Status.V1.Get` -> `NexusStatusDto`
- `VieriNexus.Dependencies.V1.Get` -> `DependencyDto[]`
- `VieriNexus.Navigation.V1.GetApiVersion` -> `int`
- `VieriNexus.Navigation.V1.GetStatus` -> `NavigationLibraryStatusDto`
- `VieriNexus.Navigation.V1.ListRoutes` -> compatibility-shaped route-summary JSON
- `VieriNexus.Navigation.V1.GetRoute` -> compatibility-shaped full-route JSON or `null`
- `VieriNexus.Navigation.V1.ResolveGearVendorOverride` -> read-only exact territory/vendor resolution JSON; absent, invalid, and ambiguous assignments fail closed
- `VieriNexus.Navigation.V1.GetActivationStatus` -> `NavigationActivationStatusDto`

Status reports ready only when required dependencies are healthy and a player snapshot is available. It always reports no active goal and `IsPaused = false` because automation is not implemented.

Navigation status reports whether execution is actively running and reports source authority only when VieriNavPlotter is actually loaded. List/detail calls expose the Nexus working library when present, otherwise the verified staged snapshot. Their JSON property shape mirrors the predecessor's read-only route list/detail payload closely enough for consumers to adapt without taking the unversioned `VieriNavPlotter.*` names while both plugins coexist. `NavigationContractJsonTests` lock the empty-list, full-route, and resolution-envelope shapes, including ordered points and a disabled override. Activation status exposes source install/load state and every policy blocker. Nexus registers only its own versioned exact-target resolver, not predecessor IPC aliases, preventing name collisions during coexistence. User commands now include `routes`, `preview <name>`, `play <name>`, and `stop` under `/nexus`/`/vierinexus`.

`VieriNexus.Commands.V1.Execute` and `NexusCommandDto`/`NexusCommandResultDto` are declared in Contracts but no command call gate is registered. Planned Goals, Combat, Positional Guidance, legacy Wrath/Switch/AutoDuty/Codex/NavPlotter aliases, handshakes, conflict enforcement, and activation commands are not present.

### 4.11 Release and upstream provenance — IMPLEMENTED AS DOCUMENTED/PINNED PROCESS

`upstreams/source-lock.json` records exact source revisions for all nine migration sources. At recovery, every sibling repository existed locally, was clean, and its `HEAD` exactly matched the Nexus pin:

| Source | Nexus-pinned/current commit |
| --- | --- |
| VieriAutoDuty | `cffd9a021fe4fac1b74188d8e96ee19d41ebba43` |
| VieriAutoMarket | `e08a70f7a9fece486962843cbe89ea9e2b969871` |
| VieriAvarice | `d9f17fd1aa8c15f69608797ff95573ef01f16b3b` |
| VieriCodex | `5c03483bccd2551257b61c45dcf9a5d53fbab844` |
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

Nexus currently detects installation/load state only. It has no health handshake or gameplay IPC with these providers.

### Recommended integrations

These do not block setup: AutoRetainer, Glamour Log, Anti-AFK (`AntiAfkKick-Dalamud`), Pandora's Box (`PandorasBox`), Gearsetter, Stylist, Fast Job Switcher, CBT (`Automaton`), Artisan, AutoHook, Mogmail, NotificationMaster, SelectString, QuestMap, YesAlready, and Skippy. Their intended consumers/capabilities are recorded in `docs/DEPENDENCY_AUDIT.md` and `DependencyCatalog.cs`.

### Intentionally not dependencies

- Questionable: VieriCodex remains authoritative during migration; after its Vieri-specific layer moves into Nexus, stock Questionable becomes the replaceable ordinary quest-execution provider behind a capability/version adapter.
- The nine Vieri migration sources: temporary coexistence/migration inputs, not third-party dependencies in the final topology.
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

- Overview: Home, Control Center, Automation, Progression, Queue
- Modules: Combat, Routes, Market, Custom UI, Communications
- Setup: Dependencies, Migration, Settings

Only Home, Control Center, Dependencies, Migration, and Settings have specialized current implementations. Other destinations render an honest staged-module placeholder explaining that no live behavior has moved.

### Current pages/workflows

- **Home** — permanent large logo hero, required-service status, Foundation/Automation/Next status cards. This replaced the rejected standalone splash popup.
- **Control Center** — idle/dependency/character cards, neutral module grid, and explicit safety state. It is informational only.
- **Dependencies** — required/recommended catalog, health, version, purpose, installer/manage buttons, and first-run Continue gate.
- **Migration** — read-only predecessor discovery, the live Routes & Navigation preview/import/rollback card, and credential-safety notice.
- **Settings** — UI scale (0.8–1.5), open Home after login, compact navigation, and per-character automation/manual-movement/manual-target settings.
- **Staged module pages** — status/explanation only; no controls are connected to gameplay.

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

`Configuration` implements Dalamud `IPluginConfiguration`, currently schema `Version = 2`:

- Global: `FirstRunComplete`, `OpenOnLogin`, `CompactNavigation`, `UiScale`, `SelectedPage`.
- Per-character dictionary keyed by `CharacterKey.ToString()` (`content ID + home world`): profile name, allow automation, pause on manual movement, pause on manual target, manual quiet period (default 1500 ms).
- Per-source `LegacyImports`: reviewed/imported flags, source version, import time, receipt ID, imported count, ready-for-activation, activated.

`Initialize(...)` clamps UI scale, restores dictionary comparers/null safety, sets schema version 2, and attaches the plugin interface. There is no explicit older-version migration switch yet.

Nexus configuration is saved through Dalamud. NavPlotter staging is separate JSON under `NexusData`; backups and receipts live beside it under the Nexus config directory.

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

There is no live Nexus automation. No task starts movement, duties, combat, questing, inventory mutation, retainers, market work, Discord work, HUD replacement, or remote commands. Standalone plugins remain authoritative.

The current code provides only reusable contracts/primitives:

- desired-state goal/task/failure/resource models;
- an atomic in-memory lease manager;
- basic world snapshot publication;
- one preserved solo-duty combat policy;
- migration staging/rollback.

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
- Manual movement pauses movement/navigation immediately; manual target changes suspend automated targeting; explicit F1/manual rotation control wins; interacting with a critical native window pauses its owner.
- Resume re-observes and replans; it does not continue an old instruction pointer.
- Stop cancels the active goal safely. Pause preserves intent and releases at a safe checkpoint. Last Run completes the current duty and disarms both the loop and its parent progression goal.
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
| VieriDeck | Command Center |
| VieriLink | Communications |
| VieriAutoMarket | Market |
| VieriDelvUI | Custom UI, Nameplates, Overlay Presentation |
| VieriNavPlotter | Routes & Navigation |

- **ABANDONED/SUPERSEDED — leave DelvUI separate:** the first architecture response proposed leaving VieriDelvUI separate. The user explicitly corrected this. VieriDelvUI must be absorbed as neutral Custom UI; it is not a permanent dependency.
- **ABANDONED — standalone Hilda layer and marker plugin:** VieriHildaLayer is obsolete and excluded. MarkerIconPriority already lives inside VieriDelvUI and must not reappear as a ninth original product or separate install.
- **IMPLEMENTED IN ROTATIONHELPER / PLANNED FOR NEXUS — WrathSwitch:** WrathSwitch functionality is already part of VieriRotationHelper, including F1/manual ownership behavior and legacy aliases. Do not create a separate Nexus WrathSwitch module.

### Migration strategy

- **RECOVERED DECISION — strangler migration:** coexist with working standalone plugins, wrap/observe them where necessary, migrate one bounded subsystem at a time, prove parity and rollback, then retire its predecessor. An all-at-once source merge is rejected.
- **RECOVERED DECISION — migrate behavior, not files/classes:** do not build `Codex.cs`, `AutoDuty.cs`, etc. Shared navigation, state, ownership, dependencies, recovery, inventory interpretation, and logging should become shared infrastructure. Domain behavior remains in bounded modules/providers.
- **IMPLEMENTED — current authority:** every standalone plugin remains authoritative by default. Routes & Navigation can receive explicit session-only authority only after VieriNavPlotter is manually unloaded; source reappearance or safety/conflict loss revokes Nexus immediately. Nexus never toggles the predecessor. Explicit same-zone Nexus route execution is now available through the guarded entry only.
- **RECOVERED DECISION — configuration safety:** every existing setting, option, keybind, route, profile, and hard-won fix must be mapped or explicitly retired. Source files stay intact. Import uses preview, backup, staging, atomic commit, receipt, validation, and rollback.
- **RECOVERED DECISION — friend/multi-user behavior:** another user installs the same product but imports and uses their own local settings. Character data is isolated by content ID/world. Never copy one user's config/secrets into another user's package.
- **IMPLEMENTED — first importer and working consumer:** Routes & Navigation is the first live slice because route data is structured and non-secret. Import/reload/rollback staging stays immutable; a separate working library supports authoring, preview, guarded same-zone playback, Stop, and compatibility-shaped read-only IPC. Remaining NavPlotter parity must land before standalone retirement.
- **DEFERRED — Communications import:** VieriLink configuration may not even be opened until a dedicated encrypted-value adapter and same-Windows-account round-trip tests exist. File existence is the only allowed generic discovery signal.

### Dependencies and external ownership

- **CURRENT TRANSITION RULE:** Questionable must not be enabled beside VieriCodex during migration and is not yet listed as a Nexus dependency. After Progression parity, the approved target is to retire the full fork and use stock Questionable as a capability-versioned ordinary quest provider while Nexus owns all Vieri-specific behavior.
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
- **RECOVERED DECISION:** user intent always wins. Manual movement, targeting, F1 rotation control, camera/UI interaction, Pause, Stop, and Last Run must have explicit predictable semantics. Automation must not fight a working encounter/targeting provider.
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
- **VieriAutoDuty -> Duties/Gear/Inventory:** stock duty paths, duty loops and Last Run; gear planning/shopping/equipping/gearset update/displaced-item cleanup; empty/weak slot handling; EXP-item protection; vendor choice and redundant-trip avoidance; repair/extract/desynth/sell/turn-in/coffers/armoire/Triple Triad; striking-dummy travel; recovery; Codex/Wrath/BossMod/Link coordination; local and Discord status.
- **VieriRotationHelper -> Combat:** embedded Wrath engine for all supported combat jobs, auto-rotation/action replacement, Single Target/AoE/Dynamic Hilda-style suggestions, multi-action forecasting, simple/advanced modes, actual hotkeys, cooldown/GCD/weave/charge state, positionals, range/enemy counts, F1/manual/in-combat ownership, legacy Wrath/Switch commands and IPC, configuration import, duplicate-hook protection. Damage suggestions are mature; comprehensive healing/utility sequences were explicitly not implemented as of the recovered conversation.
- **VieriAvarice -> Positional Guidance:** rear/flank/any verdict, shared same-frame combat forecast, green positional/range guidance, confirmed miss versus unknown distinction, BossMod/AutoDuty coordination, native UI occlusion.
- **VieriDeck -> Command Center:** command palette, favorites, hotkeys, custom/preferred commands, quick opening/navigation, attached command panel, layout/UI scale. Its generic plugin-launcher role should shrink as Nexus modules replace peers.
- **VieriLink -> Communications:** Discord status/alerts/remote commands, editable permanent status cards, independent per-user/per-channel state, duplicate recovery, authorization, acknowledgements, audit, protected secrets. Retry edits after timeout/rate limit/outage/permission errors; create a replacement post only when Discord confirms deletion; recover an existing post when a local message ID is lost. Do not automatically delete historical duplicates.
- **VieriAutoMarket -> Market:** Marketbuddy and Allagan Market integration, full retainer scans, HQ/NQ and owned-retainer identity, duplicate listing coordination, exact owned-retainer matching, external undercut rules, cooldown pacing, bounded retry/stop/reporting, and verified saved prices.
- **VieriDelvUI -> Custom UI:** useful HUD behavior, independently enabled/lazily initialized elements, layouts, nameplates, marker priority, and overlay presentation. The 2.8.0.0 integration adds Beastmaster HUD/job mapping and current distance/ready-check API compatibility without changing Vieri branding, commands, profiles, highlighting, nameplate stacking, native enemy markers, Duty Support companion roles, or saved settings. DelvUI-derived licenses/notices/provenance must remain traceable even though user-facing names are neutral.
- **VieriNavPlotter -> Routes & Navigation:** timed/manual recording, named routes, notes/tags/search, point editing/reordering, duplication, import/export, resizable library, exact current-position copy, stable IDs, assignments/overrides, connected/numbered previews, live vnavmesh chain, travel to start/destination, ordered playback, stop, and cross-zone suite travel.

## 10. Current Development State

### Git and release state

- Branch: `main`.
- Current released implementation commit: `f359e900 Keep fresh route creation available`.
- `origin/main` contains the released implementation commit.
- Recovery implementation commit: `ecaa8c7 Add transactional route migration`; the working tree was clean before `PROJECT_STATE.md` was created.
- No tags exist in this repository.
- Origin: `https://github.com/iampilcrow/VieriNexus.git`.
- Plugin project and live-feed version: `0.1.0.21`, Dalamud API 15.
- Production Dalamud custom-repository URL: `https://www.thedailypilcrow.com/dalamud/pluginmaster.json`.
- Distribution website/domain: `https://www.thedailypilcrow.com`.
- The exact source/deployment repository/path for the live feed and hosted archives must be discovered from the current working release infrastructure if it is not already present in the active local workspace; do not infer it from the Nexus repository alone.

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

The user then accepted the 0.1.0.19 vendor-template persistence gate in game: after copying a vendor template and enabling `Use as gear vendor override`, disabling and re-enabling Nexus preserved that assignment. No automatic movement was reported. Template copying, explicit assignment, and reload persistence are therefore accepted.

A read-only VieriCodex/Questionable architecture review found that stock Questionable exposes a useful but bounded IPC surface for starting/stopping supported quests and gathering work, querying current quest/step and quest eligibility/status, and managing its quest-priority list. It does not expose VieriCodex's Progression Queue, Progress Atlas, Hunting Log planner and target data, exploration/Aether Current/Aetheryte planners, one-click/local transport, gear-readiness and AutoDuty sequencing, solo-duty combat handoff, named VieriNavPlotter routes, custom UI/hotkeys/settings, or arbitrary custom quest-path injection. The user approved a capability-versioned hybrid target: retain VieriCodex as authoritative during migration, move the Vieri planning/policy/UI layer into Nexus, and prove stock Questionable as the external provider for ordinary supported quest execution before retiring whole-fork upstream merges. Custom or unsupported route data remains in a small Nexus-owned overlay/executor or is accepted upstream.

### Current workstream

The user had said all vendor routes were in a good place and instructed development to continue piecing VieriNexus together while preserving every setting and Discord key. The assistant chose Routes & Navigation as the first safe vertical migration slice and completed staging/rollback.

The user subsequently confirmed that VieriNexus installs and updates through Dalamud, that the Migration card successfully staged a valid VieriNavPlotter configuration containing zero personal routes, and that closing/reopening the window retained the staged message. Disabling/re-enabling 0.1.0.4 made only the in-memory message disappear. Direct inspection confirmed that Nexus configuration, `routes.v1.json`, backups, and receipts remained present; the current VieriNavPlotter source and its timestamped backup both still match the receipt's original SHA-256. The zero-route result is expected because recording/display/pane/selection settings are still migrated. The earlier fixed-size button and status clipping was corrected in 0.1.0.4.

The explicit next gate is:

1. **CONFIRMED IN GAME ON 0.1.0.15:** working-library creation, points, preview, Travel to Start, ordered playback, button Stop, manual takeover, no-replay acknowledgement, return to staging, and manual source re-enable all passed.
2. **CONFIRMED IN GAME ON 0.1.0.18:** the Routes page has only its outer page scrollbar; Clear all points and Delete route both open confirmations, execute after approval, and remain unchanged when cancelled.
3. Verify selecting a point then replacing it with the current position, moving it up/down, and removing it immediately updates the saved route and active preview.
4. Verify duplicate receives a distinct name and disabled assignment; copy/import receives a new identity and disabled assignment.
5. Verify recording stops when changing territory or disabling Nexus and does not resume automatically after reload.
6. **CONFIRMED IN GAME ON 0.1.0.19:** a copied vendor template starts as an independent personal route, its exact-target override can be enabled explicitly, and the `Use as gear vendor override` assignment survives disabling and re-enabling Nexus without starting movement.
7. On 0.1.0.20, validate filtered live generated-waypoint rendering, current-target capture, cross-zone suite travel/Stop, and one real VieriAutoDuty Gear lookup using the enabled Nexus override. Confirm ordinary duty, Questionable, and unrelated vnavmesh paths remain hidden.

The import, responsive Migration text, on-disk persistence, direct source-integrity checks, saved-receipt recovery, guarded rollback/re-import, correct Routes-page state transitions, source-owner assessment, all three navigation safety guarantees, disabled approval under loaded-source ownership, session-authority handoff, non-empty manual route authoring/preview/playback, both Stop paths, acknowledgement, safe return to VieriNavPlotter, destructive-action confirmations/cancellation, the corrected single-scroll layout, and vendor-template assignment persistence are confirmed. Navigation IPC, timed-recording shutdown, clipboard compatibility, and general preference persistence remain user-side/in-game verification items.

### Completed versus unfinished

**Completed foundation:** solution layering, shell/Home/dependency/setup UI, basic character/world readiness, neutral module descriptors, domain contracts, tested lease/verified-Stop/manual-yield/reload-watchdog/authority/recovery primitives, live navigation provider health and bounded transition audit, isolated six-scenario non-moving safety simulation, read-only status/dependency/navigation/activation/override-resolution IPC, nine-source read-only discovery, exact source lock, first transactional importer, immutable verified staging, a separate atomic working-library store, provider-neutral route planning, manual and timed route authoring, detailed point editing and safe route exchange, the immutable 27-route vendor catalog, exact-target one-winner assignment and current-target capture, static and ownership-filtered generated-path preview, guarded same-zone execution/yield, cross-zone suite travel, and VieriAutoDuty Gear consumption with transition fallbacks.

**Partially complete:** world state, dependency health, module contract, IPC surface, configuration migration framework, ownership, solo-duty policy, navigation migration.

**Not implemented:** any action that resumes interrupted movement; durable general audit/history; other eight transactional importers; Communications secret adapter; general planner/scheduler/executor/reconciler; command gateway/event bus; goals UI; SQLite history; diagnostics/support export; legacy IPC aliases; migrated combat/progression/duty/gear/market/communications/command-center/custom-UI runtimes; standalone retirement.

There is no known external blocker. The old conversation's context window, not the repository, caused the handoff.

## 11. Known Bugs, Edge Cases, and Reliability Concerns

### Current Nexus implementation concerns

- **Partially verified live migration:** Dalamud installation/update, responsive Migration text, a zero-route import, window reopen, on-disk persistence, source/backup hash integrity, receipt/payload recovery, guarded rollback/re-import, correct Routes-page staging transitions, corrected layout, all three safety guarantees, source-owner blocking, session-only no-movement handoff, the 0.1.0.13 5/5 isolated simulation during active VieriCodex duty movement, the 0.1.0.14 6/6 isolated simulation, a non-empty personal route, guarded travel/playback and both Stop paths, stopped-intent acknowledgement, destructive-action confirmation/cancellation, the one-scroll Routes layout, and 0.1.0.19 vendor-template assignment persistence are confirmed. Read-only navigation IPC, timed-recording shutdown, clipboard compatibility, and general preference persistence remain unverified in game.
- **Partial command contract:** `VieriNexus.Commands.V1.Execute` is public but unregistered. Consumers must not assume it works.
- **Partial knowledge model:** `KnowledgeState.Stale/Unavailable` exist but are never emitted by the current observer; provider state is always empty.
- **Readiness semantics:** `SessionSnapshot.IsLoading` currently mirrors the between-area flags rather than representing every loading/occupied state.
- **Same-zone execution is verified in game:** guarded Travel to Start and ordered playback, button Stop, manual-movement takeover, stopped-intent acknowledgement, staging return, and manual VieriNavPlotter re-enable all passed without automatic replay. Cross-zone and automatic consumers remain disabled pending their own controlled slices.
- **Migration validation boundaries:** route validation rejects non-finite coordinates/tolerances and duplicate/empty IDs, but does not currently impose semantic ranges for interval, pane width, tolerances, binding kind, or territory/target combinations. Empty routes intentionally remain editable drafts.
- **Migration service cache:** source preview invalidates by path and last-write time. Extremely unusual same-timestamp external rewrites could leave a stale preview until reload/mtime change.
- **Control Center remains static:** Routes has a live runtime, but the overview still shows no active goals/resources and other module pages remain placeholders.
- **Dependency health is shallow:** installed/loaded state is not the same as compatible version or healthy IPC. The new navigation diagnostics label vnavmesh as loaded/available but deliberately do not invoke Stop or movement-state IPC merely to probe health.
- **Assessment is not enforcement:** loaded-source and Navigation/Movement conflicts are detected and reported; verified Stop and manual takeover can safely end a tracked execution, but there is no activation command or executor to acquire/track that lease yet. The same policy and interlocks must guard the future transition atomically.

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
- `ResourceLeaseManager` is instantiated and inspected by navigation activation assessment, but remains an in-memory primitive rather than a runtime ownership system. No execution lease is acquired. Priority is stored but unused; safe checkpoint/preemption/cancellation/manual inhibition are absent.
- `SoloDutyCombatPolicy` duplicates preserved behavior as constants/tests but is not connected to a provider adapter or replay fixture.
- World state is minimal and now contains six navigation-specific provider/safety observations. Other domains remain empty; future modules must not add independent ad hoc scanners as a shortcut.
- Dependency catalog is centralized and static rather than contributed by modules/providers; compatibility/version/IPC health is absent.
- UI navigation and status strings are hardcoded; most pages are honest placeholders. No schema-driven goal builder or registered page/panel contributions.
- `Configuration.Version` is forced to 2 without a formal migration pipeline.
- IPC defines an unused command endpoint and lacks handshake, goals, combat, positional, legacy alias, conflict, and deprecation support.
- Migration storage is JSON and generic only in name: `TransactionalMigrationStore.Apply(...)` currently accepts `NavigationLibrarySnapshot`, so later importers will require a safe generalization rather than duplicated transaction code.
- There is no durable general logging/audit/history/support-export implementation despite `IPluginLog` and `IChatGui` being injected. The bounded session-only navigation transition audit is deliberately narrower.
- No SQLite dependency/store, event bus, command gateway, framework-thread dispatcher abstraction, background-work boundary, replay harness, provider contract harness, or packaging test project.
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
- Manual input takes priority. Never “solve” contention by two controllers repeatedly reasserting state.
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
10. **Atomic ownership.** No provider acts without all required leases; no partial acquisition; no unsafe force-preemption; manual input wins.
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

1. **IMPLEMENTED / PARTIALLY VERIFIED:** hash-verified staging, rollback/re-import/page synchronization, corrected layout, compatibility-shaped IPC, a separate editable working library, manual route authoring, preview, and guarded same-zone execution exist. Validate the new working route and IPC in game.
2. **IMPLEMENTED AS FAIL-CLOSED ASSESSMENT:** source/dependency/lease conflicts and the Stop/manual override/reload/approval prerequisites are modeled, tested, visible, and queryable. Runtime activation does not exist.
3. **IMPLEMENTED FOUNDATION:** verified Stop is idempotent, provider-neutral, connected to vnavmesh, retains ownership for every unconfirmed outcome, and releases only after explicit inactive confirmation. It is not exposed until an executor exists.
4. **IMPLEMENTED FOUNDATION:** manual movement uses configured FFXIV actions, blocks starts, latches verified Stop on takeover, and requires explicit resume after the quiet period. It is not exposed until an executor exists.
5. **IMPLEMENTED WITHOUT EXECUTION:** navigation reload/shutdown reconciliation, active lease enforcement/watchdog, session-only authority approval, and the atomic source/safety/resource execution boundary are connected. The handoff never toggles the predecessor and no provider movement is exposed.
6. **IMPLEMENTED WITHOUT MOVEMENT:** six live navigation health observations populate the Routes diagnostics and shared world snapshot; a bounded session audit records transitions; an isolated six-scenario simulator exercises the production safety coordinators, including provider loss/retry, without a movement operation or live-state access.
7. **IMPLEMENTED FAIL-CLOSED:** explicit stopped-intent acknowledgement requires confirmed Stop, released Navigation/Movement ownership, and elapsed manual-input quiet period. It clears no-replay/manual latches but cannot resume or approve navigation.
8. **PLANNED:** generalize the transactional importer/store carefully and implement the remaining source importers one at a time, each with complete field inventory, golden fixtures, behavior/IPC parity, and rollback.
9. **DEFERRED UNTIL SECURITY TESTS:** Communications/VieriLink importer only after same-account encrypted round-trip and secret redaction tests.
10. **REQUIRED FOUNDATION WORK:** the `WorldStateStore.Publish` exchange-before-validation defect is fixed with regression coverage and navigation provider health is now truthful. Expand other world/provider domains; implement framework-thread sequencing, command gateway, event facts, and durable logging/audit before authoritative automation. The navigation-only active watchdog is now present; other resource domains still need equivalent enforcement as they become executable.

The original Phase 0/1 foundation checklist is only partly complete. Do not jump straight from the shell to mass source absorption.

### Medium-Term

- Prove one real end-to-end `Reach Job Level` goal through adapters to existing Codex/AutoDuty/RotationHelper behavior. The proposed acceptance case is Viper current level +2 with allowed job quests/side quests/duties, 1,000,000 gil reserve, gear readiness, pause/reload/manual override/provider restart/Last Run coverage, and verified completion.
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

1. **CONFIRMED IN GAME ON 0.1.0.12:** the Activation Safety panel reports Verified Stop, Manual movement yielding, and Reload recovery/lease watchdog as connected without clipping; loaded VieriNavPlotter disables approval with manual-unload guidance; manual unload enables the session-only approval; approval starts no movement and exposes return to staging.
2. Validate a real non-empty route with a disabled override. Verify search/detail/point rendering and all six read-only navigation IPC calls. Guarded rollback, re-import, reload recovery, current source/backup hash integrity, and automated payload-shape coverage are already confirmed without opening unrelated or protected VieriLink data.
3. Optionally exercise the explicit session-only handoff: manually unload VieriNavPlotter, approve Nexus authority, verify that no route starts, return to staging, then manually re-enable VieriNavPlotter.
4. **CONFIRMED IN GAME ON 0.1.0.13:** provider-health/audit visibility and the isolated simulator passed 5/5 while VieriCodex was moving through a duty, without interrupting movement.
5. **CONFIRMED IN GAME ON 0.1.0.14:** explicit stopped-intent acknowledgement/reset requires safe conditions, and the isolated simulator including provider-loss/retry passed 6/6.
6. **CONFIRMED IN GAME ON 0.1.0.15:** create working copy/route/points, preview, guarded travel/playback, button Stop, manual takeover, no-replay acknowledgement, staging return, and VieriNavPlotter re-enable all passed.
7. **CONFIRMED IN GAME ON 0.1.0.18:** single-scroll Routes layout and confirmation-protected clear/delete, including both cancellation paths.
8. **CONFIRMED IN GAME ON 0.1.0.19:** a copied vendor template remained assigned as `Use as gear vendor override` after Nexus was disabled and re-enabled, with no automatic movement.
9. Validate the 0.1.0.20 Routes closure batch: target capture remains disabled until approval; generated waypoints appear for Nexus-owned local/delegated route travel but not unrelated movement; cross-zone travel and Stop work; VieriAutoDuty Gear prefers an active Nexus override and retains both transition fallbacks.
10. Begin the Progression foundation in a substantial slice: freeze VieriCodex custom behavior as Nexus-owned capabilities, add the capability-versioned stock Questionable provider boundary, and keep VieriCodex authoritative until parity is proven.
11. Then choose the next low-risk importer. Do not choose Communications until encrypted-value tests exist; do not choose Market as an early runtime proof.
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
- **Current release:** `0.1.0.21`.
- **Current project version source verified in repository:** `src/VieriNexus.Plugin/VieriNexus.Plugin.csproj` contains `<Version>0.1.0.21</Version>` and uses `Dalamud.NET.Sdk/15.0.0` at this snapshot.
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

Before a release, inspect every location in the current code/release infrastructure that represents the plugin version. The currently verified Nexus source contains version `0.1.0.21` in:

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
