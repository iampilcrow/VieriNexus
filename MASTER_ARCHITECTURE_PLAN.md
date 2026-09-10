# VieriNexus Master Architecture Plan

Status: Architecture only. No live plugin behavior is changed by this document.

## 1. Executive decision

VieriNexus should become one installed Dalamud plugin suite with one public identity, one release, one primary window, one configuration root, and one ownership model.

It should not become one giant C# project or one giant controller. Internally it should remain a modular monolith: several assemblies and strictly bounded modules packaged in one Dalamud distribution.

The migration should use a strangler approach during development:

1. Build the Nexus foundation and temporarily wrap existing Vieri plugins through their current IPC.
2. Prove the architecture with one real end-to-end progression goal.
3. Move one subsystem at a time into Nexus while preserving its old IPC contract.
4. Retire each old Vieri plugin after its in-process module has reached behavioral parity.

The migration adapters are temporary scaffolding, not part of the desired installed topology. The completed product requires only VieriNexus plus genuine third-party dependencies.

Navigation is a first-class shared service. User-authored named routes are reusable assets rather than hard-coded overrides, and progression, duties, gear, market, gathering, crafting, farming, and future modules may consume them through one ownership-controlled route library.

An all-at-once merge is rejected. It would combine several mature state machines, UI hooks, game-thread assumptions, and update pipelines before Nexus has proved its scheduler or recovery model.

## 2. Product boundary

### Nexus owns

- Goals, constraints, planning, task scheduling, cancellation, pausing, and reconciliation.
- Exclusive ownership of automation resources.
- Shared character/world snapshots and freshness tracking.
- Unified status, dependencies, settings, history, and diagnostics UI.
- The complete custom HUD, automation overlays, hotkeys, command palette, communications, and QoL experience currently spread across Vieri products.
- Versioned public IPC and compatibility aliases.
- Cross-module policy such as gear readiness before progression.
- One release package and update manifest.

### Modules own

- Domain-specific facts and business rules.
- Eligibility checks and candidate strategies.
- Execution of their own operations after Nexus grants resources.
- Domain-specific recovery suggestions.
- Module settings and diagnostic details.

### External providers own

- Their native pathing, quest, duty, combat, market, or crafting implementation.
- Their own internal state machines.
- Capability health and version compatibility signals exposed to Nexus.

### Nexus does not own

- Reimplementing Questionable, AutoDuty's stock duty engine, BossMod, vnavmesh, Lifestream, TextAdvance, Marketbuddy, or Allagan Market. VieriCodex and VieriAutoDuty remain authoritative during migration; their Vieri-specific planners, policies, safety fixes, custom route/travel, gear, maintenance, coordination, and UI migrate into Nexus. Stock Questionable and stock AutoDuty then become replaceable module-scoped providers after capability/parity validation.
- Per-frame combat decisions. Those remain inside the embedded Wrath engine.
- General ownership of third-party dependencies or their update channels.
- Arbitrary remote control without an explicit allowlist and local safety checks.

### Final installed topology

```text
Installed Vieri software
  VieriNexus only

Installed external dependencies as required by enabled modules
  BossMod
  vnavmesh
  Lifestream
  TextAdvance
  Marketbuddy
  Allagan Market
  Questionable (when Progression/Questing is enabled)
  AutoDuty (when Duties are enabled)
  other explicitly supported third-party providers added later
```

The eight migration sources are VieriAutoDuty, VieriAutoMarket, VieriAvarice, VieriCodex, VieriDeck, VieriDelvUI, VieriLink, and VieriRotationHelper. None remain separately installed in the completed state. Their names are used in this plan only to identify migration sources and compatibility contracts.

## 3. Recommended solution layout

The user installs one `VieriNexus` package. The package may contain multiple internal assemblies.

```text
VieriNexus.Plugin
  Dalamud entry point, DI composition root, framework-thread dispatcher

VieriNexus.Contracts
  Stable public IPC DTOs, capability IDs, commands, read-only status contracts

VieriNexus.Domain
  Goals, constraints, plans, tasks, resources, failures, identities, events

VieriNexus.Application
  Planner, scheduler, executor, reconciliation, policy, command gateway

VieriNexus.Infrastructure.Dalamud
  Game-state observers, persistence, IPC transport, logging, dependency probes

VieriNexus.UI
  Shell, dashboard, goal builder, plan/activity/dependency/settings pages

VieriNexus.Modules.Progression
VieriNexus.Modules.Questing
VieriNexus.Modules.HuntingLog
VieriNexus.Modules.Duties
VieriNexus.Modules.Gear
VieriNexus.Modules.Inventory
VieriNexus.Modules.Combat
VieriNexus.Modules.CombatGuidance
VieriNexus.Modules.CustomHud
VieriNexus.Modules.Overlay
VieriNexus.Modules.Market
VieriNexus.Modules.Communications
VieriNexus.Modules.QoL

VieriNexus.Providers.QuestEngine
VieriNexus.Providers.AutoDuty
VieriNexus.Providers.BossMod
VieriNexus.Providers.Vnavmesh
VieriNexus.Providers.Lifestream
VieriNexus.Providers.Marketbuddy
VieriNexus.Providers.AllaganMarket

VieriNexus.Tests.Domain
VieriNexus.Tests.Application
VieriNexus.Tests.Contracts
VieriNexus.Tests.Replay
VieriNexus.Tests.Packaging
```

Future feature assemblies follow the same contract without changing the core. Likely additions include `Crafting`, `Gathering`, `Farming`, `Dailies`, `Procurement`, `Retainers`, `Currencies`, `Collections`, and `Schedules`. These are extension points, not foundation dependencies.

Dependency direction is inward:

```text
Plugin/UI/Infrastructure/Modules/Providers
                  |
                  v
             Application
                  |
                  v
                Domain
```

`Domain` references no Dalamud, game structures, ImGui, or third-party plugin. Providers never call UI. Modules never reach into another module's private implementation.

## 4. Findings from the current products

### VieriCodex

Keep its quest database, quest validation, hunting-log intelligence, rank gating, FATE synchronization, alternate mob locations, and hard-won navigation recovery.

Do not promote `QuestController` into the Nexus orchestrator. It is already responsible for too many specialized concerns. Initially expose Codex as Questing, Hunting Log, Achievement, and Side Quest capabilities. Later move those capabilities behind separate Nexus module boundaries.

The current Progression Queue contains useful policies, but its persisted queue state should eventually be replaced by durable Nexus goals and disposable plans.

### VieriAutoDuty

Treat the fork as a migration source and temporary compatibility provider. Wrap stock AutoDuty through a narrow capability/version adapter as the permanent Duty provider; keep its supported duty paths and internal duty state machine native and updateable through its own channel.

Move Vieri-specific route travel, gear shopping/readiness, equipment cleanup, repair/extraction/desynthesis/selling/turn-in policy, maintenance scheduling, Last Run, progression loops, telemetry, command coordination, and UI into Nexus-owned modules. Prefer one bounded stock duty run per Nexus task so Nexus—not a custom endless provider loop—decides whether another duty is needed.

Do not copy or casually modify stock dungeon routing as part of Nexus. Generic duty-engine corrections should be contributed upstream or proven present through provider-contract/replay tests before the fork retires. The exact current inventory and retirement gates are recorded in `docs/AUTODUTY_PROVIDER_MIGRATION_AUDIT.md`.

### VieriRotationHelper

VieriRotationHelper already contains the embedded Wrath engine, switch behavior, prediction bars, positional guidance contracts, and legacy Wrath-compatible IPC. Treat it as the initial Combat kernel.

Per-frame rotation evaluation must remain isolated from the slower goal planner, database, Discord, and UI work. Nexus grants or denies combat authority; the combat engine decides actions.

Combat-control behavior already incorporated into RotationHelper migrates with that module. Legacy Wrath and Switch IPC aliases remain available where outside integrations require them, but there is no separate Switch product to absorb into Nexus.

### VieriAvarice

Move its positional verdict, drawing, and useful ground/occlusion behavior into Combat Guidance. It should consume the same combat snapshot and forecast as RotationHelper instead of maintaining an independent rotation opinion.

### VieriDeck

Reuse its command-palette, favorites, keybind, and navigation ideas. Retire its plugin-launcher role as features become Nexus modules. The resulting command palette should invoke registered Nexus commands, not private module methods.

### VieriLink

Keep Discord transport, editable status cards, duplicate recovery, token protection, and notification policies. Convert it to a communications adapter that consumes domain events and submits allowlisted commands through the command gateway.

It must not directly mutate another module.

### VieriAutoMarket

Keep its pure pricing decisions, pacing, ownership awareness, reports, and UI adapter. Move it later into Market/Economy because market operations are UI-fragile and need strong checkpoint rules.

### VieriDelvUI

Absorb its useful HUD implementation into a neutrally named `CustomHud` module. The final system must not require a separately installed VieriDelvUI or present VieriDelvUI as a product inside Nexus.

This is a source and behavior migration, not permission for the Custom HUD to bypass module boundaries. It consumes shared read models and registered commands, owns only its presentation settings, and cannot directly control progression, combat, or inventory systems. HUD elements should be independently enabled and lazily initialized so users are not forced to display every component.

Any upstream DelvUI-derived source, licensing, and notices remain traceable in build metadata even though the user-facing module has a neutral name.

Marker-icon priority is already part of the VieriDelvUI codebase and therefore migrates as an existing `Custom UI` feature, not as a ninth plugin or separate migration project.

VieriHildaLayer is obsolete and is not a migration source. Any generally useful loading gates or overlay-visibility patterns must come from active code or be implemented directly in Nexus; no HildaLayer compatibility or migration work is planned.

### End-state module naming

The predecessor names are never used as Nexus page, module, service, or marketing names. They appear only in migration code, release notes, configuration importers, and temporary compatibility diagnostics.

| Migration source | Neutral Nexus destination |
|---|---|
| VieriCodex | Progression, Questing, Hunting Log, Achievements, Exploration |
| VieriAutoDuty | Duties, Gear, Inventory, Travel Utilities |
| VieriRotationHelper | Rotation Engine, Combat Suggestions, Keybinds |
| VieriAvarice | Positional Guidance |
| VieriDeck | Command Center |
| VieriLink | Communications |
| VieriAutoMarket | Market |
| VieriDelvUI | Custom UI, Nameplates, Overlay Presentation |

These are internal feature boundaries, not separately installable products. The UI may combine related destinations further when that produces a cleaner user experience.

## 5. Core domain model

### Stable identifiers

Use string-backed, versioned identifiers rather than one global enum that every module must edit:

```text
GoalKind       vieri.progression.reach-job-level/v1
TaskKind       vieri.gear.ensure-readiness/v1
CapabilityId   vieri.capability.duty.run/v1
ResourceId     vieri.resource.movement/v1
ProviderId     vieri.provider.autoduty-stock/v1
```

Typed payloads are registered with serializers and migrators. Unknown kinds remain inspectable and recoverable instead of crashing deserialization.

### Goal

A goal is durable desired state, not a list of button presses.

Required fields:

- Goal ID, kind, schema version, character scope, creation source, and timestamps.
- Desired-state payload.
- Constraints and preferences.
- Priority and lifecycle state.
- User-facing title and rationale.
- Current plan revision and last reconciliation result.

Recommended goal states:

```text
Draft -> Ready -> Active -> Satisfied
                    |         ^
                    v         |
                  Paused ------
                    |
                    v
                  Blocked

Any nonterminal state -> Cancelled
```

`Blocked` means Nexus cannot make meaningful progress until a condition changes. Temporary provider delay or resource contention is not automatically a blocked goal.

### Constraints

Constraints are typed and explainable. Examples:

- Allowed or excluded activity categories.
- Minimum gil reserve.
- Maximum spend.
- Stop time or run count.
- Duty, PvP, party, or unsynced restrictions.
- Manual-control policy.
- Inventory reserve.
- Preferred provider or provider exclusion.
- Safety/risk profile.

Hard constraints filter plans. Preferences influence deterministic scoring. A preference may never silently override a hard constraint.

### Plan

A plan is a versioned, derived proposal for reaching desired state. It is intentionally disposable.

Use hierarchical planning with small dependency graphs:

- A goal handler decomposes desired state into requirements.
- Strategy providers propose candidate steps.
- The planner filters them against actual state and constraints.
- A deterministic scorer chooses a strategy.
- Only the near-term executable frontier needs detailed tasks.
- Reconciliation can replace the remaining plan whenever reality changes.

Do not build a giant once-only DAG for an entire 1-to-100 journey. The game state, unlocks, gear, plugin health, and user actions will invalidate it.

### Task

A task is a bounded, observable operation with declared preconditions, resources, verification, cancellation behavior, and checkpoint policy.

Required task contract:

```text
Describe        What and why
Evaluate        Ready / satisfied / blocked / unsupported / unknown
Prepare         Create an immutable execution request
Acquire         Atomically acquire all required resources
Execute         Start or advance provider operation
Observe         Report progress and heartbeat
Verify          Prove the intended postcondition
Checkpoint      Persist only a safe boundary
Cancel          Cooperative cancellation
Recover         Offer domain-specific recovery strategies
```

Recommended task states:

```text
Proposed -> Waiting -> Ready -> Acquiring -> Running -> Verifying -> Succeeded
                       |          |          |             |
                       +----------+----------+-----------> Failed
                                             |
                                             +-----------> NeedsReconciliation
Any active state -> Cancelling -> Cancelled
```

An execution attempt is immutable history. Retrying creates a new attempt under the same logical task.

## 6. Shared world state

Publish an immutable `WorldSnapshot` with a monotonically increasing revision and capture time.

Core slices:

- Session: logged in, loading, zoning, cutscene, occupied conditions, framework readiness.
- Character: content ID, home/current world, level/job, party, combat state.
- Location: territory, position, movement, mount, flight, navigation state.
- Progression: quests, unlocks, duties, hunting logs, achievements, job levels.
- Equipment: equipped slots, armory candidates, item level, durability, spiritbond.
- Inventory: free slots, protected items, currencies, gil.
- Combat: targets, enemy count, rotation authority, forecast, positional guidance.
- Providers: installation, load, version, health, current operation.
- User control: recent movement, target, hotkey, manual pause, active UI interaction.

Every state value that can be unavailable must express `Known`, `Unknown`, `Stale`, or `Unavailable`. Unknown is never equivalent to false, zero, incomplete, or completed.

Observers update at appropriate rates. High-frequency combat and movement slices are lightweight and event/frame based. Slow progression scans and provider health checks are throttled. Consumers read snapshots; they do not perform game scans independently.

All authoritative state transitions occur on one sequenced framework-thread dispatcher. Background work may perform network, persistence, or pure computation and must marshal results back.

## 7. Capability and provider model

A capability describes an outcome Nexus can request. A provider describes one implementation.

Examples:

```text
Travel.ToLocation         Lifestream + vnavmesh
Quest.Execute             Nexus progression/quest engine
HuntingLog.CompleteEntry  Codex hunting-log engine
Duty.Run                  Nexus Duties module
Gear.EnsureReadiness      Vieri gear system
Combat.Control            Embedded Wrath engine
Market.ScanAndReprice     Nexus Market module + Marketbuddy + Allagan Market
Notify.Status             Nexus Communications module
```

Each provider publishes:

- Capability IDs and contract versions.
- Required dependencies and tested version range.
- Current health and readiness.
- Eligibility for a supplied immutable request.
- Required resources.
- Estimated duration/cost/risk where meaningful.
- Cancellation and checkpoint semantics.
- Structured failure details.

Planning never causes provider side effects. Execution receives a prepared immutable request after all resources are acquired.

Provider selection is deterministic: hard eligibility, safety, health, explicit user preference, expected cost, then stable provider ID as tie-breaker. The chosen reason is visible in the UI and audit trail.

### Expansion contract

Every feature module implements one registration boundary, conceptually `INexusModule`. At startup it may register only declared contributions:

- Goal schemas and satisfaction evaluators.
- Strategy planners and task handlers.
- Capability providers and dependency manifests.
- World-state observers and read-model projections.
- Resource requirements and conflict declarations.
- Settings schemas and migrations.
- Dashboard cards, settings panels, goal editors, and command-palette actions.
- Audit formatters and sanitized support diagnostics.

The application core discovers registrations; it never contains a switch statement listing Crafting, Gathering, Farming, Dailies, or every future domain. Cross-domain workflows communicate through desired-state requirements and capabilities. For example, a farming plan may request `AcquireItem`, which can be satisfied by combat, gathering, crafting, vendor, retainer, duty, or market strategies without Farming directly controlling those modules.

Modules ship inside the single Nexus package and are enabled as features, not installed as additional Vieri plugins. A later external provider may be added without moving that provider's implementation into Nexus.

## 8. Resource and ownership system

The lease manager is the most important cross-product safety boundary.

Initial resource set:

```text
Movement
Navigation
Teleport
Targeting
Combat
Rotation
UiInteraction
DutyQueue
JobChange
InventoryMutation
Retainer
Market
```

Tasks request a bundle atomically in a canonical order. Partial acquisition is forbidden, preventing common deadlocks. Conflict rules are explicit; for example, Market implies Retainer, UI Interaction, and Inventory Mutation authority.

A lease contains owner goal/task/attempt IDs, priority, reason, acquisition time, heartbeat, expiry, cancellation token, and safe-release behavior.

Rules:

- No arbitrary force-preemption during unsafe UI, duty, teleport, or purchase operations.
- Higher priority requests ask the current owner to reach a safe checkpoint.
- Missing heartbeats expire through a watchdog and trigger reconciliation.
- A provider may be capable but cannot act without authority.
- Gear shopping acquires everything it needs before it interrupts questing or duty preparation.
- Rotation and combat ownership must preserve the existing Wrath automation/manual behavior.

Manual user control is an inhibition layer above leases, not merely another automation lease.

## 9. Manual override contract

User intent always wins, but it must be predictable.

- Manual movement pauses movement/navigation tasks immediately and starts a quiet-period timer.
- Manual target changes pause automated targeting without necessarily stopping combat rotation.
- Manual F1/Switch control overrides automation requests until explicitly released.
- Closing or interacting with a critical game window pauses the owning UI task.
- Stop cancels the active goal and requests safe provider cancellation.
- Pause preserves the goal but releases resources at the next safe checkpoint.
- Last Run disarms the duty loop and the parent progression goal without abandoning the current duty.

Resume always re-evaluates actual state and replans. It never continues from an old instruction pointer.

## 10. Reconciliation and recovery

Nexus continuously compares desired state with fresh actual state.

Reconciliation occurs on:

- Startup or plugin reload.
- Login, logout, character change, zoning, duty entry/exit.
- Provider load/unload or health change.
- Manual intervention.
- Task completion, timeout, failure, or lost heartbeat.
- Inventory, gear, quest, unlock, or level changes relevant to the goal.

On startup, persisted `Running`, `Verifying`, or `Retrying` work becomes `NeedsReconciliation`. Nexus checks the postcondition before deciding whether to mark it complete, retry safely, select another strategy, or ask the user.

Failure taxonomy:

```text
TransientExternal
RateLimited
PreconditionChanged
DependencyUnavailable
ResourceConflict
UserIntervention
UnsafeState
Unsupported
PermanentData
Cancelled
```

The central recovery policy controls retry budgets, backoff, provider fallback, replanning, and escalation. Modules supply domain-specific recovery candidates. Retries must be bounded by both attempt count and elapsed time.

Unsafe transient operations do not auto-resume. Examples include confirming a market price, buying an item, discarding/selling, accepting a duty, or interacting with a retainer after state identity is uncertain.

## 11. Events, commands, and audit

Use a typed in-process event bus for facts, not commands.

Examples:

- `GoalActivated`
- `PlanRevised`
- `TaskStarted`
- `TaskProgressed`
- `TaskFailed`
- `LeaseGranted`
- `ProviderHealthChanged`
- `WorldSnapshotChanged`

Commands flow through one command gateway that performs authorization, validation, character scoping, and deduplication. UI, hotkeys, IPC, and Discord all use this gateway.

Do not event-source the entire application. Persist ordinary current state plus an append-only, bounded audit log. Sensitive fields are redacted before logging.

Every visible task should answer:

- What is Nexus doing?
- Why did it choose this?
- Which provider owns it?
- What resources are held?
- What happens next?
- What is blocking progress?

## 12. Persistence and character isolation

Use two stores:

- Versioned JSON for human-scale settings, profiles, layout, and dependency preferences.
- SQLite for goals, plan revisions, tasks, attempts, checkpoints, audit history, and provider observations.

Use atomic writes, schema migrations, backups before migration, and bounded retention.

Character identity is based on content ID plus world ID, never character name. Account-wide settings, character settings, and shared profiles are distinct scopes. No friend can ever see another character's goals, achievements, progression, or status unless an explicit shared view is later designed.

Never persist game pointers, live object references, ephemeral addon indexes, access tokens in task payloads, or a promise that a transient UI step is safe to replay.

## 13. Configuration model

Precedence:

```text
Code defaults
  -> Global/account settings
  -> Character settings
  -> Named profile
  -> Goal constraints/preferences
```

Runtime task leases and temporary provider overrides are not saved as user configuration.

Each module owns its settings schema and migration. The Nexus shell composes settings pages. Settings that affect safety or external purchases require clear explanations and conservative defaults.

Importers migrate current Vieri configurations once and record the source version. Existing files remain untouched until the user confirms the new suite works.

## 14. Dependency management

Each module/provider supplies a manifest:

- Required and optional dependencies.
- Minimum and maximum tested versions.
- Install/update source.
- Health probe and degraded-mode description.
- Capabilities unavailable when missing.

Dependency states:

```text
Missing
Disabled
Incompatible
Starting
Healthy
Degraded
Faulted
```

The Dependencies page offers explicit install/open/update actions. Nexus must never silently install, enable, disable, or update another plugin.

BossMod, vnavmesh, Lifestream, TextAdvance, Marketbuddy, and Allagan Market are the initial core external providers. Stock Questionable joins for ordinary supported quest execution after the Vieri-specific Progression layer migrates, and stock AutoDuty joins as the Duties provider after Vieri route/gear/maintenance/control behavior migrates. Both are module-scoped rather than global setup blockers and require capability/version/parity validation. Recommended integrations are listed separately and include AutoRetainer, Glamour Log, Anti-AFK, Pandora's Box, Gearsetter, Stylist, Fast Job Switcher, CBT, Artisan, AutoHook, Mogmail, NotificationMaster, SelectString, QuestMap, YesAlready, and Skippy.

## 15. Unified UI

Start with six destinations rather than a permanent tab for every module:

1. Dashboard
2. Goals
3. Plan
4. Activity
5. Dependencies
6. Settings

The Dashboard shows current goal, current task, reason, provider, held resources, next step, pause/stop controls, and any actionable blocker.

The Goal Builder is schema-driven. Goal modules contribute editors for their typed desired state and constraints. The user sees plain language, validation, and a preview of likely strategy before starting.

The Plan view shows the current near-term plan and why alternatives were rejected. Activity provides filtered history and a support export with secrets removed.

Modules may register contextual panels and actions, but not create uncontrolled top-level tabs. The current command-palette behavior becomes global Nexus search/navigation and an action launcher.

The `Custom UI` settings area owns the migrated HUD editor and element configuration. It should feel like a native Nexus feature, not a VieriDelvUI window embedded inside another plugin. HUD modules consume the same shared snapshots as the dashboard, which prevents duplicate game-state scanners and inconsistent values.

Combat suggestions, Switch controls, positional guides, and other in-world overlays remain lightweight independent windows. A shared overlay visibility service hides all Nexus overlays during loading, logout, screenshots/cutscenes where configured, and incompatible native UI states.

## 16. IPC and backward compatibility

Publish new versioned APIs under `VieriNexus.*.V1` with immutable DTOs and a capability/version handshake.

Initial public groups:

```text
VieriNexus.Status.V1
VieriNexus.Commands.V1
VieriNexus.Goals.V1
VieriNexus.Combat.V1
VieriNexus.PositionalGuidance.V1
VieriNexus.Dependencies.V1
```

Preserve legacy aliases while outside dependents and the migration builds transition, including the necessary `WrathCombo.*`, `WrathSwitch.*`, `AutoDuty.*`, `VieriCodex.*`, Avarice, and positional-guidance endpoints. These are compatibility identifiers only; they are not module or page names. All aliases route to the same underlying Nexus services, and there must never be two competing automation engines.

When an old plugin and its replacement Nexus module are both loaded, Nexus must detect the conflict. The safe default is to leave the Nexus module inactive, explain the conflict, and offer migration steps. It must not install duplicate hooks.

Compatibility shims receive a published deprecation window and usage telemetry limited to local diagnostic counters.

## 17. Security and remote commands

- Discord tokens and other secrets stay in an encrypted, dedicated secrets boundary.
- Secrets never enter logs, goals, task payloads, exports, or IPC DTOs.
- Remote commands are disabled by default and individually allowlisted.
- Destructive, purchasing, market, inventory, and account-affecting commands require local policy approval and fresh character/session checks.
- Commands use nonce/deduplication IDs, timestamps, rate limits, and an audit record.
- A remote sender cannot bypass manual override, resource ownership, or safety checks.

## 18. Performance rules

- No database, network, reflection scan, or large LINQ allocation on the framework update path.
- Combat prediction and overlays retain their dedicated low-allocation hot path.
- World-state observers publish only changed slices.
- Planner work is debounced and cancellable.
- Persistence batches noncritical audit writes but commits safety checkpoints immediately.
- Provider health checks use backoff and do not poll every frame.
- Quest and content data should be a module/data assembly, not loaded wholesale by the domain core.
- Establish frame-time, allocation, database-size, and startup-time budgets before migration begins.

## 19. Testing strategy

### Pure automated tests

- Goal satisfaction and constraint evaluation.
- Planner determinism, idempotence, and alternative selection.
- Resource bundle acquisition, deadlock prevention, expiry, cancellation, and fairness.
- Recovery policy and retry budgets.
- Configuration and database migrations with golden fixtures.
- Character isolation.
- IPC contract compatibility.
- Market pricing and gear-selection policy.

### Provider contract tests

Every provider runs against a fake harness proving readiness, cancellation, progress, timeout, verification, and structured failure behavior.

### Replay tests

Record sanitized world snapshots and domain events from known incidents. Replay them deterministically to prevent regressions such as teleport loops, gear-shopping contention, stale duty paths, unsafe market retries, and premature UI display during loading.

### In-game validation matrix

- Login/logout/reload and character switching.
- Manual movement/target/F1 overrides.
- Quest, hunting log, side quest, and achievement entry/exit.
- Duty queue, duty completion, Last Run, disconnect, and wipe.
- Gear shopping from empty, weak, mixed, unique-ring, and armory-contained states.
- Inventory-full maintenance.
- Provider missing, disabled, updated, or restarted mid-task.
- Every supported combat job with simple/advanced modes and healing where applicable.

### Release tests

- Clean install and upgrades from every supported Vieri predecessor.
- Manifest, repository, ZIP content, dependency declarations, icon, changelog, and source archive.
- One-plugin load without duplicate hooks or assembly collisions.

## 20. Release architecture

VieriNexus has one public version, one Dalamud manifest, one update channel, one changelog, and one source archive. Internal module and upstream-source versions are recorded in build metadata and the diagnostics page.

Pin third-party source snapshots used by embedded systems. Maintain licenses, notices, patches, and upstream commit references. Prefer capability-versioned external providers so routine Questionable, Boss Mod, vnavmesh, Lifestream, and similar updates require no Nexus changes while their public contracts remain compatible. Embedded/custom overlays still require focused diff, contract, replay, configuration, and package validation.

Builds must be reproducible from a clean checkout. Publishing is blocked if migrations, package validation, IPC compatibility, or critical replay tests fail.

## 21. Migration sequence

### Phase 0 — Freeze contracts and measure behavior

- Inventory every IPC endpoint, command, configuration file, hotkey, dependency, and public UI action.
- Capture current behavior and regression scenarios.
- Establish package/version baselines and legal notices.
- Define the final VieriNexus internal name before users receive a build.

Exit: a compatibility matrix and replay fixture set exist.

### Phase 1 — Foundation shell

- Create Domain, Application, Contracts, Infrastructure, UI, and test projects.
- Implement world snapshots, command gateway, event sequencing, resource leases, persistence, audit, provider registry, and dependency health.
- Add Dashboard/Goals/Plan/Activity/Dependencies/Settings shells.
- Do not migrate combat, quests, duty internals, or market internals yet.

Exit: fake providers can run, pause, cancel, crash, reload, reconcile, and resume safely.

### Phase 2 — Proof vertical slice

Implement `Reach Job Level` for one selected combat job through the temporary Vieri sources while proving the same capability contracts against stock Questionable and stock AutoDuty.

The real workflow may:

- Validate character/job and goal constraints.
- Switch job through the existing safe service.
- Request gear readiness as an exclusive prerequisite.
- Select eligible job quests, hunting log, side quests, or duty leveling according to policy and unlocks.
- Delegate ordinary supported quest work through the Questionable provider boundary and one bounded duty run through the AutoDuty provider boundary; use the Vieri sources only as temporary compatibility providers until parity gates pass.
- Grant Combat/Rotation to the existing embedded Wrath engine.
- Replan after every level, unlock, gear change, duty completion, or manual intervention.

Do not promise that every route from level 1 to cap is handled in this milestone. The proof succeeds when one bounded level range survives pause, reload, manual override, provider restart, gear interruption, and Last Run without two systems fighting.

Exit: one real goal demonstrates desired state, planning, ownership, verification, persistence, recovery, UI explanation, and legacy IPC.

### Phase 3 — Combat consolidation

- Move VieriRotationHelper into the Nexus package without rewriting the Wrath engine.
- Preserve Wrath and Switch IPC aliases.
- Merge Avarice positional guidance onto the shared combat forecast.
- Retire VieriRotationHelper and VieriAvarice after combat, control, suggestion, positional, keybind, and IPC parity testing.

### Phase 4 — Progression and questing

- Separate Codex questing, hunting log, achievements, side quests, and progression policies into module boundaries.
- Keep content data isolated and validated.
- Replace the old Progression Queue UI/state with Nexus goals while preserving import.
- Retire VieriCodex only after feature and content parity.

### Phase 5 — Duties, gear, and inventory

- Bring VieriAutoDuty-specific capabilities into Nexus incrementally while stock AutoDuty remains the replaceable duty executor.
- Preserve stock paths and BossMod behavior; upstream generic duty fixes or prove current stock parity instead of embedding the duty engine.
- Make gear/inventory shared services available to every progression workflow.
- Retire the VieriAutoDuty fork only after dungeon-provider, shopping, maintenance, Last Run, configuration, IPC, recovery, and rollback parity.

### Phase 6 — Communications and command palette

- Migrate VieriLink to the event/command gateway.
- Migrate VieriDeck UI concepts and settings.
- Retire their standalone packages after migration and secret/config verification.

### Phase 7 — Custom UI and presentation

- Migrate the VieriDelvUI-derived HUD into the neutral `CustomHud` module.
- Migrate shared loading gates, overlay visibility, native-UI occlusion, layout locking, and edit-mode behavior into `Overlay`.
- Preserve the marker-priority behavior already contained in the Custom UI source as an optional Nameplates feature.
- Import existing layouts and settings without keeping the predecessor product names in the Nexus UI.
- Verify that disabled HUD elements have negligible update cost.
- Retire VieriDelvUI after visual, configuration, nameplate, and performance parity.

### Phase 8 — Market and remaining QoL

- Migrate VieriAutoMarket with strict UI checkpoints and ownership leases.
- Migrate any remaining Vieri-branded utility after an explicit capability and settings audit.
- Retire VieriAutoMarket and the last standalone Vieri packages after parity.

### Phase 9 — Standalone-product removal

- Confirm every Vieri feature, configuration, hotkey, command, IPC consumer, and release channel has a Nexus destination.
- Remove temporary cross-Vieri IPC adapters that are no longer needed by genuine outside consumers.
- Publish guided uninstall instructions for every predecessor plugin.
- Validate a clean machine with only VieriNexus and its external dependencies installed.

Exit: VieriNexus is the only installed Vieri plugin required for the complete experience.

### Phase 10 — Expansion domains

Only after the executor is proven should Nexus add new production and routine domains. The expected roadmap includes:

- Crafting goals, recipes, quality requirements, consumables, and Artisan execution.
- Gathering goals, node availability, timed windows, collectability, and route providers.
- Farming goals that choose among combat drops, gathering, duties, vendors, retainers, and the market.
- Daily and weekly routines with reset-aware eligibility, dependency ordering, and per-character completion.
- Procurement and production chains that recursively satisfy material requirements.
- Retainer, venture, currency, collection, reputation, hunt, and event goals.
- Scheduling policies that prioritize expiring opportunities without silently taking control from the user.

Every new domain plugs into the same goal, capability, provider, resource, snapshot, command, persistence, UI registration, and recovery contracts. Adding a domain must not require new branches in a central all-knowing controller.

## 22. First milestone recommendation

Use a real but bounded Viper leveling goal as the proof, because it exercises job state, gear readiness, quests, duties, combat authority, travel, and persistence without requiring level-1 class onboarding.

Suggested acceptance scenario:

```text
Goal: Raise Viper from its current level to current level + 2
Allowed: job quests, eligible side quests, duties
Required: keep 1,000,000 gil; maintain gear readiness
Stop: after target level or Last Run
```

Acceptance criteria:

- UI explains current and next action.
- Gear readiness fully pauses competing travel/queue work.
- Exactly one owner controls movement, UI, duty queue, and combat at a time.
- Manual movement and F1 behavior are respected.
- Reload during any safe checkpoint reconciles correctly.
- Reload during an unsafe transient step does not blindly repeat it.
- Dependency loss blocks or falls back with an actionable explanation.
- Last Run completes the current duty and disarms the parent goal.
- Goal completion is verified from character state, not inferred from a provider message.
- Existing Vieri plugins continue to work through compatibility endpoints during the proof.

## 23. Decisions required before code begins

1. Confirm `VieriNexus` as the permanent Dalamud internal name, not only the display name.
2. Approve one installed package containing several internal assemblies.
3. Approve SQLite for durable goal/task/history storage.
4. Define how long old standalone Vieri plugins must coexist with Nexus.
5. Confirm Viper current-level-to-plus-two as the first proof scope.
6. Choose the exact default manual override quiet period and resume behavior.
7. Define the Custom HUD migration boundary and which legacy HUD elements ship enabled by default.
8. Decide whether large quest/content data ships only with plugin releases or may have its own signed data update.
9. Define remote-command allowlists and which commands can never be remote.
10. Freeze the current configs and IPC inventory before moving any subsystem.

## 24. Principal risks and mitigations

| Risk | Mitigation |
|---|---|
| Giant coordinator replaces many smaller coordinators | Enforce module contracts and dependency direction in build tests |
| Two systems control the same game feature | Atomic resource leases and conflict detection |
| Stale plans perform wrong actions | Short planning horizon and continuous reconciliation |
| Reload repeats a purchase or destructive UI action | Safe checkpoints and postcondition verification |
| Combat performance regresses | Keep the Wrath hot path isolated and benchmarked |
| Old integrations break | Versioned Nexus API plus legacy shims backed by the same services |
| Character data crosses users | Content-ID/world partitioning and isolation tests |
| Gear/quest/duty flow races | Shared snapshots and one sequenced state-transition loop |
| Upstream update breaks an embedded subsystem | Pin commits, record patches, contract/replay test before release |
| Package becomes enormous or slow | Separate data assemblies, lazy module initialization, explicit budgets |
| Unknown game state is treated as completed | First-class unknown/stale state and conservative evaluation |
| Remote command compromises safety | Allowlist, local validation, rate limits, deduplication, audit |

## 25. Architectural rules that must not be relaxed

1. One installed product does not mean one undivided code project.
2. Goals describe desired state; tasks describe bounded work.
3. Plans are replaceable; verified progress is durable.
4. No provider acts without resource authority.
5. Events report facts; commands request actions.
6. Unknown state never counts as success.
7. Manual input takes priority and causes reconciliation.
8. Persistence resumes intent, not unsafe instruction pointers.
9. The combat hot path never waits on the director.
10. Third-party engines remain providers until replacing them has a proven benefit.
11. Every active action must be explainable in plain language.
12. Migration parity is demonstrated before a standalone plugin is retired, and all standalone Vieri plugins are ultimately retired.

## 26. Final recommendation

Proceed with VieriNexus, but begin by building the control plane rather than merging feature code. The first deliverable should be a tested shell that can observe the world, own resources, plan a small leveling goal, temporarily delegate to existing Vieri capabilities, survive interruption, and explain itself.

The final deliverable is one complete VieriNexus installation. Every current Vieri product becomes a neutrally named internal feature module, and only external dependencies remain separate.

Once that foundation is trustworthy, the current products can move inside it one at a time without throwing away the systems that already work.
