# VieriNexus

VieriNexus is the in-progress unified home for the Vieri FFXIV plugin suite.

Its ninth migration source is VieriNavPlotter: a reusable named-route library for recording, manually plotting, previewing, and assigning navigation paths across Nexus modules.

The first public build is an early testing foundation. Install it alongside the existing Vieri plugins; it does not replace or disable them yet.

The foundation build intentionally does not replace live automation. It provides the application shell, Home experience, module registry, dependency gate, character-scoped settings, shared world snapshots, resource ownership, migration discovery, and versioned IPC contracts needed for safe incremental consolidation.

Routes & Navigation is the first transactional migration slice. Its Migration card previews every VieriNavPlotter setting, creates a timestamped backup, writes a staged Nexus-owned route library atomically, records hashes and a rollback receipt, and can restore the prior Nexus state. Importing does not activate Nexus navigation or disable VieriNavPlotter.

Staged migration status survives a Nexus reload: the saved receipt, target path, target hash, schema, and staged payload are verified before the import message and rollback action are restored.

The Routes page now presents that verified staged snapshot as a read-only library. It shows imported preferences and, when present, searchable route metadata, movement settings, assignments, tolerances, and ordered points. Versioned Nexus navigation IPC provides read-only status/list/detail access with compatibility-shaped JSON, while execution, override resolution, route drawing, travel, and playback remain disabled.

An activation-safety assessment now makes the coexistence boundary explicit. It detects the installed/loaded source owner, required-provider readiness, Navigation/Movement lease conflicts, and the mandatory Stop, manual-override, reload-reconciliation, and explicit-approval gates. Nexus cannot approve navigation authority while any gate is blocked, and this release intentionally provides no route executor or execution action.

The first execution-safety primitive is now connected behind that assessment. Nexus has an idempotent, provider-neutral Stop coordinator and a vnavmesh adapter: a tracked execution keeps its Navigation/Movement lease when Stop is unavailable, fails, cannot be confirmed, or movement remains active, and releases ownership only after vnavmesh explicitly reports that the path is no longer running. There is still no route executor, execution action, or public Stop command in this build.

Manual-movement yielding is now connected as the next fail-closed primitive. Nexus observes FFXIV's own configured movement actions, covering remapped keyboard controls, mouse steering, gamepad movement, jump, and autorun. Physical player input blocks a new movement start; if a future tracked execution is active, takeover latches verified Stop and cannot silently auto-resume after the input ends. The existing character setting must remain enabled for navigation activation readiness.

Reload reconciliation and active lease enforcement are now connected behind the same assessment. Before any future provider movement, Nexus must atomically persist a minimal execution ID, route ID, lease ID, and state—never an instruction pointer. A plugin reload, shutdown, or missed Navigation/Movement heartbeat can only latch verified Stop, confirm movement inactive, and wait for a separate explicit acknowledgement. Corrupt or unwritable intent remains fail-closed. The watchdog runs every draw, including while the Nexus window is closed; the current build still exposes no route executor or execution action.

The explicit navigation-authority handoff is now available as a reversible, session-only decision. Nexus will not disable or enable VieriNavPlotter: the user must unload it manually before approval becomes available. Approval atomically probes the complete Navigation/Movement resource bundle but starts nothing. Any future route start must re-check the source and every safety prerequisite, atomically acquire the bundle, persist no-replay intent, and register verified Stop before a provider may be called. If the source reappears or safety is lost, Nexus revokes authority and stops tracked work. Returning to staging changes no source plugin, and reload never remembers approval.

The Routes page now includes live provider-health and transition-audit panels for vnavmesh Stop, manual movement, reload/watchdog state, predecessor ownership, the atomic resource bundle, and Nexus authority. These observations also populate the shared world snapshot instead of leaving provider health empty. A user-triggered non-moving simulation runs the production safety coordinators against isolated memory-only leases, journals, and a Stop-only provider. It checks guarded start/Stop, manual takeover, reload recovery, source return, and lease expiry without touching live ownership, the live journal, or any movement provider.

## Current safety guarantees

- Existing Vieri configurations are discovered read-only and left in place.
- Discord credentials and channel/message IDs are not read, decrypted, logged, or rewritten.
- The Communications discovery card checks only whether VieriLink configuration exists; it does not open that file.
- Route imports preserve disabled overrides as disabled and never activate navigation automatically.
- Route-library browsing and IPC read only the verified staged snapshot; they do not query or change the live VieriNavPlotter configuration.
- A loaded VieriNavPlotter is reported as the current route owner and blocks Nexus approval. Nexus never disables or enables it automatically.
- Stop completion requires a separate inactive-path confirmation; sending a Stop request alone never releases navigation ownership.
- Player movement input takes priority, blocks a new Nexus movement start through its quiet period, and requires an explicit resume decision after interrupting tracked navigation.
- Reload recovery never replays a saved movement instruction; it stops first and requires explicit acknowledgement.
- Expired ownership leases are reported to the active watchdog and trigger the same fail-closed Stop path instead of disappearing silently.
- Navigation-authority approval is session-only, starts no route, can be returned to staging, and is revoked if the predecessor or another owner conflicts.
- Provider health is observed read-only and its bounded session audit records transitions rather than every frame.
- The non-moving safety simulation uses isolated memory-only state and has no movement operation.
- No migration-source plugin is disabled automatically.
- No gameplay automation is started by the foundation build.
- The Nexus window waits until a targetable character is fully in the world.
- The supplied VieriNexus logo is the permanent Home experience, not a temporary popup window.
- Questionable is not a runtime dependency. Its maintained quest engine and route data are incorporated through VieriCodex and will migrate into Nexus.

The complete required/recommended provider inventory and its current-product evidence are recorded in [`docs/DEPENDENCY_AUDIT.md`](docs/DEPENDENCY_AUDIT.md).

## Build

```powershell
dotnet build .\VieriNexus.slnx -c Release
```
