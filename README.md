# VieriNexus

VieriNexus is the in-progress unified home for the Vieri FFXIV plugin suite.

Its ninth migration source is VieriNavPlotter: a reusable named-route library for recording, manually plotting, previewing, and assigning navigation paths across Nexus modules.

Install it alongside the existing Vieri plugins during migration. Nexus never disables a predecessor automatically.

The shared foundation provides the application shell, Home experience, module registry, dependency gate, character-scoped settings, shared world snapshots, resource ownership, migration discovery, and versioned IPC contracts needed for safe consolidation.

Progression now has its first Nexus-owned planning surface. For the current job, it persists a target level, allowed job-quest/Hunting Log/side-quest/duty methods, and a hard minimum-gil reserve. It capability-checks VieriCodex versus stock Questionable and VieriAutoDuty versus stock AutoDuty, rejects simultaneous ready quest providers, and previews a bounded gear/quest/one-duty/verification plan. These probes are read-only and the planner cannot start provider work yet, so the existing Vieri progression products remain authoritative while the execution lifecycle is built.

Routes & Navigation is the first transactional migration slice. Its Migration card previews every VieriNavPlotter setting, creates a timestamped backup, writes a staged Nexus-owned route library atomically, records hashes and a rollback receipt, and can restore the prior Nexus state. Importing does not activate Nexus navigation or disable VieriNavPlotter.

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
- During migration VieriCodex remains authoritative and stock Questionable must not run beside it. The approved target architecture moves Vieri-specific planning, policy, safety, custom-route, and UI behavior into Nexus, then uses stock Questionable through a narrow versioned provider adapter for ordinary supported quest execution.

The complete required/recommended provider inventory and its current-product evidence are recorded in [`docs/DEPENDENCY_AUDIT.md`](docs/DEPENDENCY_AUDIT.md).

## Build

```powershell
dotnet build .\VieriNexus.slnx -c Release
```
