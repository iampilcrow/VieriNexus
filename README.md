# VieriNexus

VieriNexus is the in-progress unified home for the Vieri FFXIV plugin suite.

Its ninth migration source is VieriNavPlotter: a reusable named-route library for recording, manually plotting, previewing, and assigning navigation paths across Nexus modules.

Install it alongside the existing Vieri plugins during migration. On each computer, use Migration > Set Up This Computer while the old products and their local settings still exist. Nexus creates verified working copies and never disables a predecessor automatically. Only after the relevant Nexus replacement is ready should that user disable the old product.

The shared foundation provides the application shell, Home experience, module registry, dependency gate, character-scoped settings, shared world snapshots, resource ownership, migration discovery, and versioned IPC contracts needed for safe consolidation.

Plugins is the complete Nexus-owned replacement for VieriDeck. Quick Access provides Dalamud Plugins, Dalamud Settings, the Nexus operations overlay, Nexus Settings, and refresh; yellow filled stars identify favorites above the remaining installed plugins. Each plugin expands its own registered and saved commands directly in place, with left-click execution, right-click copy, plugin actions, and no second page scrollbar or distant command panel. Each computer can transactionally import its own filters, favorites, hidden entries, preferred/custom commands, hotkey, and layout reference while leaving VieriDeck unchanged and rollbackable; a new user can also start fresh. Open it with `/nexus plugins`; the older Nexus command aliases remain compatible. The exact retirement mapping and acceptance gate are documented in `docs/VIERIDECK_PARITY_AUDIT.md`.

Progression now owns a durable gear, quest, Hunting Log, and duty execution lifecycle. For the current job, it persists a target level, allowed Class/Job/Role-quest, Hunting Log, general-side-quest, and duty methods, a hard minimum-gil reserve, bounded task history, completion checkpoints, Stop-after, Stop, and plan revision in character-scoped atomic storage. Nexus discovers exact eligible quests from current game data and asks the selected Questionable-compatible provider to run only that quest. Nexus also selects one exact live Hunting Log target: field targets use stock Lifestream, vnavmesh, and Boss Mod mechanics, while duty-only Grand Company targets use one exact unlocked AutoDuty path. Nexus retains ownership until the pinned quest, field kill count, or dungeon-only kill count is confirmed and the provider is inactive. Unsupported quest paths are skipped under a bounded session limit; failed, stopped, or unverified work cannot be counted or silently replayed.

Progress Atlas reads Aetherytes/Aethernet, all Aether Currents, Mapping/Remapping exploration, achievements, and all current class/Grand Company Hunting Log targets directly from the character. Its one-click actions can attune the next reachable travel node, collect the next reachable field current, run the next currently unlockable quest-earned current through the exact quest contract, or explore the next reachable map region. Nexus owns selection, resource leases, Stop, and exact completion verification; provider loss never turns an unverified objective into success.

Gear & Inventory owns both manual and automatic upgrade planning and execution. Nexus reads live equipped/owned items and each supported ordinary gil vendor's real game-data catalog, chooses the correct vendor band and job family, scores the current job's primary stat, selects the winning item per slot, protects active EXP items, and rejects off-hand purchases behind a two-handed weapon. One exact approval pins the character, equipment signature, vendor and territory, item IDs, quantities, maximum prices, and protected gil floor. Nexus then travels over Lifestream/vnavmesh, opens the matching combat shop, confirms each exact purchase, verifies each equip, updates the current gearset, and moves only equipment displaced by that transaction into normal inventory. Automatic Progression uses the same native transaction and will not schedule a duty after an unconfirmed gear failure. VieriAutoDuty is no longer used for Gear & Inventory.

Routes & Navigation is the first transactional migration slice. Its Migration card previews every VieriNavPlotter setting, creates a timestamped backup, writes a staged Nexus-owned route library atomically, records hashes and a rollback receipt, and can restore the prior Nexus state. Importing does not activate Nexus navigation or disable VieriNavPlotter.

The VieriAutoDuty operations importer preserves every profile, character assignment, retired-item transfer, compact-overlay preference, and custom gear/repair/extraction/coffer/desynthesis/turn-in/selling/registration/in-duty-maintenance policy as one verified staged snapshot. It also creates a separate atomic Nexus working copy, resolves the correct profile by content ID with saved-default fallback, and applies the imported overlay preferences once for that receipt. Nexus owns the maintenance sequence and directly runs self-repair, materia extraction, protected desynthesis, Triple Triad/minion/orchestrion registration, eligible coffer opening, and exact reviewed NPC selling. Selling approvals pin every bag slot/item/quantity and invalidate on any inventory change while permanently excluding EXP-bonus gear, collectables, protected gearsets, and unsafe equipment. For Grand Company turn-ins and eligible Armoire/Glamour storage, Nexus owns prerequisite selection, teleport/inn entry, local vnavmesh approach, furnishing interaction, leases, Stop, timeouts, and completion verification; stock AutoRetainer and Glamour Log receive only the final narrow mechanics request. An empty eligible set completes as a verified no-op. AutoDuty is not called. In-duty withdrawal remains inactive until stock AutoDuty exposes a verifiable leave/resume boundary.

The operations importer treats missing, explicit-null, and wrong-type optional scalar values as safe defaults and reports malformed profile entries as migration issues rather than throwing from the startup or Migration draw path.

The compact optional Goto/Gear/Inventory/Duty/Extras overlay includes native maintenance actions, the preserved striking-dummy catalog, configured level-goal Start/Resume/Last Run controls, live operation status, and one global Stop. Striking-dummy travel uses Lifestream only for an unlocked teleport and Nexus-owned vnavmesh travel for the final approach. It does not fall back to stock AutoDuty's UI or fork-only helper endpoints.

Nexus also owns one versioned command and operations-status gateway for its overlay, chat, and trusted companion integrations. Mutation requests are bounded, idempotent by request ID, and scoped to the current character. The gateway exposes the same route, Progression, maintenance, protected-selling review, page-opening, and global Stop behavior as the visible UI; unsupported fork-only pause/leave/loop commands fail closed rather than approximating unsafe provider behavior. `/nexus status`, `start`, `resume`, `last`, `stop`, and the maintenance shortcuts use this same boundary.

The bounded duty adapter supports either VieriAutoDuty during migration or stock AutoDuty after the fork is unloaded, using only the narrow stock-compatible path/start/status/Stop/leveling-reset contract. Because both packages intentionally share the `AutoDuty` internal identity, Nexus inventories every matching installation and blocks duty dispatch if more than one instance is loaded—even if the colliding IPC registry appears partially healthy. Disabled alternatives remain visible but do not own the contract.

For stock Questionable solo quest duties, Nexus owns the missing rotation handoff without copying Questionable or Wrath. Only a Nexus-started exact quest inside a duty can acquire the public Wrath-compatible action lease; Boss Mod keeps sole movement ownership. The selected-target policy never changes the player's hard target, uses only a short bounded nearest-hostile fallback, and returns safely to Boss Mod rotation if Wrath is absent or rejects control. VieriCodex and AutoDuty work are excluded from this handoff.

Staged migration status survives a Nexus reload: the saved receipt, target path, target hash, schema, and staged payload are verified before the import message and rollback action are restored.

The Routes page keeps that verified staging snapshot immutable and can explicitly promote it into a separate Nexus-owned working library. The working library is atomically saved with a previous-file recovery copy and can create a route at the character's current position, record movement at a configurable cadence and minimum spacing, add/replace/reorder/remove/reverse points, edit names/tags/notes and movement settings, duplicate routes, safely exchange route JSON, search, inspect, preview, travel to point one, play every point in order, clear points only after confirmation, and delete only after confirmation. Changing the working copy never changes VieriNavPlotter or invalidates the migration receipt.

Timed recording is observation-only: it neither requires nor acquires navigation authority and cannot move the character. It avoids duplicating an already-nearby final point, persists each accepted capture atomically, refreshes an active preview, and stops if the route disappears, the character becomes unavailable, the territory changes, the plugin unloads, or the character logs out. Route JSON imports accept both the versioned Nexus shape and the compatible legacy VieriNavPlotter shape, regenerate identity, bound untrusted data, and always disable automation assignment pending review.

The route library also exposes all 27 verified VieriAutoDuty gear-vendor standing-point templates as immutable references. Adding a template creates an independent editable personal copy with its automation override disabled. The user may bind the current target and explicitly enable one route for an exact territory/vendor pair; Nexus disables any competing route for that same pair atomically. While Nexus owns navigation, VieriAutoDuty 1.0.0.440 prefers the versioned Nexus resolver, falls back to VieriNavPlotter during transition, and finally retains its built-in safe route. Absent, inactive, invalid, mismatched, empty, oversized, or ambiguous Nexus assignments fail closed.

The compact Routes page leads with Search, Create, and the selected route's Play/Travel/Show actions. Recording and Add Position live together with collapsed recording options; route metadata/automation, display preferences, vendor templates, import/export/destructive tools, and diagnostics stay in clearly named collapsed sections until needed. The route list and editor grow naturally under the page's single scrollbar. Clear-points and delete-route actions retain confirmation dialogs.

The activation-safety assessment enforces the coexistence boundary internally. When VieriNavPlotter is loaded, Nexus routes pause. When it is not loaded and the working library/providers are ready, Nexus authority becomes available automatically without a separate approval or staging workflow.

Route actions now use a Nexus-owned complete-trip coordinator and no longer call VieriAutoDuty. Nexus asks stock Lifestream for cross-zone teleports, Aethernet transfers, and exact Grand Company inn shortcuts, waits through loading transitions, then gives only the authored local path to vnavmesh. Same-territory routes go directly to vnavmesh. Manual movement neither cancels an active route nor blocks a start; user cancellation occurs only through the explicit Stop button or `/nexus stop`, which aborts only the Lifestream/vnavmesh work belonging to that Nexus trip. Provider failures are reported as failures rather than false completions, and replacement vnavmesh destinations are yielded without stopping the replacement plugin.

Live generated vnavmesh waypoints can now be shown separately from the saved route preview. The ownership filter draws them only during local Nexus execution or suite travel explicitly dispatched by the current Nexus process; ordinary AutoDuty, duty, Questionable, and unrelated vnavmesh movement remains hidden.

Route input policy is explicit-Stop-only. Nexus does not observe keyboard, mouse, gamepad, jump, or autorun input to block or cancel route movement. Historical manual-yield types remain only as compatibility/regression fixtures and are not part of the production route path.

Reload reconciliation and active lease enforcement remain connected behind the same assessment. Before direct local provider movement, Nexus atomically persists a minimal execution ID, route ID, lease ID, and state—never an instruction pointer. Reload, shutdown, or a missed heartbeat can only Stop and confirm movement inactive; they never replay a route. A normal user Stop completes immediately. Corrupt or unwritable intent remains fail-closed. The watchdog and execution monitor run every draw, including while the Nexus window is closed.

Nexus still never disables or enables VieriNavPlotter. Each route start rechecks the source, current character automation setting, provider readiness, and every ownership/safety prerequisite before movement. Manual movement is not a cancellation signal; user cancellation is only the explicit Stop button or `/nexus stop`. If the source reappears or provider/ownership safety is lost, Nexus revokes authority and safely ends tracked work. Reload never replays an interrupted route.

The Routes page includes provider-health and transition-audit panels for explicit route Stop, reload/watchdog state, predecessor ownership, the atomic resource bundle, and Nexus authority. These observations also populate the shared world snapshot instead of leaving provider health empty.

An explicit Stop confirms inactivity, releases ownership, and completes its checkpoint immediately, so the route can be started again without another acknowledgement. Reload/source/provider failures remain fail-closed and never replay movement.

## Current safety guarantees

- Existing Vieri configurations are discovered read-only and left in place.
- Discord credentials and channel/message IDs are not read, decrypted, logged, or rewritten.
- The Communications discovery card checks only whether VieriLink configuration exists; it does not open that file.
- Route imports preserve disabled overrides as disabled and never activate navigation automatically.
- Built-in vendor templates remain immutable, personal copies begin disabled, and exact-target override resolution never starts movement.
- Migration staging remains immutable. Nexus edits only its separate working library, and read-only IPC reports the active working copy when present.
- A loaded VieriNavPlotter is reported as the current route owner and pauses Nexus route actions. Unloading it makes Nexus ready automatically; Nexus never disables or enables it.
- Stop completion requires a separate inactive-path confirmation; sending a Stop request alone never releases navigation ownership.
- Player movement input does not stop or block Nexus routes; the route Stop button and `/nexus stop` are the user cancellation controls.
- Reload recovery never replays a saved movement instruction; it stops first and waits for a new explicit route action.
- Progression stores desired state and task checkpoints, never a provider instruction pointer. Reload/unload stops and reconciles the active gear, exact-quest, or duty task without replaying it.
- An exact quest task counts only when the pinned quest ID is reported complete and the quest provider has returned control. A provider switch to another quest triggers Stop and retains ownership until inactivity is confirmed.
- A duty counts only after Nexus observes provider start, correct duty entry, the matching game duty-completion event, provider shutdown, and return to the world. Abandonment or an unconfirmed exit pauses the goal without scheduling another run.
- Progression acquires the full duty/teleport/UI/inventory/targeting/rotation resource bundle and retains it across Stop reconciliation until provider inactivity is confirmed.
- Gear readiness is its own durable task with exclusive travel/UI/inventory ownership. The approved gil floor is temporary, prior provider settings are restored, and a gil-floor or item-level regression blocks duty dispatch.
- Manual gear shopping starts only from the Nexus Gear & Inventory page after exact single-use approval. A changed character, equipment snapshot, item, quantity, or higher price invalidates the request before movement; the full resource bundle remains owned until provider inactivity is confirmed.
- Expired ownership leases are reported to the active watchdog and trigger the same fail-closed Stop path instead of disappearing silently.
- Navigation authority is automatic while VieriNavPlotter is off and is revoked if the predecessor or another owner conflicts.
- Same-zone and cross-zone route actions prefer the capability-checked suite travel provider for consistent whole-trip behavior; guarded direct vnavmesh is a same-zone fallback.
- A replacement shared-provider path causes Nexus to yield its own lease without sending Stop to the new owner.
- Provider health is observed read-only and its bounded session audit records transitions rather than every frame.
- The non-moving safety simulation uses isolated memory-only state and has no movement operation.
- Route cancellation is explicit-Stop-only. Historical manual-takeover primitives remain as compatibility fixtures but are not connected to production route input.
- No migration-source plugin is disabled automatically.
- Nexus starts route movement only from an explicit Routes-page or `/nexus play <name>` action after working-library and safety checks.
- The Nexus window waits until a targetable character is fully in the world.
- The supplied VieriNexus logo is the permanent Home experience, not a temporary popup window.
- VieriCodex migration now backs up and transactionally stages its complete activity preferences and saved Progression Queue definition, applies a matching current-job goal without replaying a predecessor instruction pointer, and supports guarded rollback. Nexus owns exact MSQ, Class/Job/Role, ordinary side-quest, current-quest, and Atlas objective selection; stock Questionable supplies the one exact supported quest's mechanics through the narrow versioned adapter. Never load VieriCodex and Questionable together.

The complete required/recommended provider inventory and its current-product evidence are recorded in [`docs/DEPENDENCY_AUDIT.md`](docs/DEPENDENCY_AUDIT.md).

## Build

```powershell
dotnet build .\VieriNexus.slnx -c Release
```
