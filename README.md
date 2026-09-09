# VieriNexus

VieriNexus is the in-progress unified home for the Vieri FFXIV plugin suite.

Its ninth migration source is VieriNavPlotter: a reusable named-route library for recording, manually plotting, previewing, and assigning navigation paths across Nexus modules.

Install it alongside the existing Vieri plugins during migration. Nexus never disables a predecessor automatically.

The shared foundation provides the application shell, Home experience, module registry, dependency gate, character-scoped settings, shared world snapshots, resource ownership, migration discovery, and versioned IPC contracts needed for safe consolidation.

Routes & Navigation is the first transactional migration slice. Its Migration card previews every VieriNavPlotter setting, creates a timestamped backup, writes a staged Nexus-owned route library atomically, records hashes and a rollback receipt, and can restore the prior Nexus state. Importing does not activate Nexus navigation or disable VieriNavPlotter.

Staged migration status survives a Nexus reload: the saved receipt, target path, target hash, schema, and staged payload are verified before the import message and rollback action are restored.

The Routes page keeps that verified staging snapshot immutable and can explicitly promote it into a separate Nexus-owned working library. The working library is atomically saved with a previous-file recovery copy and can create a route at the character's current position, add/undo/reverse points, edit names/tags/notes and movement settings, search, inspect, preview, travel to point one, play every point in order, and delete only after confirmation. Changing the working copy never changes VieriNavPlotter or invalidates the migration receipt.

The activation-safety assessment makes the coexistence boundary explicit. It detects the installed/loaded source owner, working-library readiness, required-provider readiness, Navigation/Movement lease conflicts, and the mandatory Stop, manual-override, reload-reconciliation, and explicit-approval gates. VieriNavPlotter must be unloaded manually before Nexus authority can be approved; approval starts no route automatically.

The provider-neutral route executor acquires Navigation and Movement atomically, journals no-replay intent, and registers verified Stop before invoking vnavmesh. It heartbeats ownership throughout same-zone travel/playback. Natural completion releases ownership only after an independent inactive observation and sends no global Stop. If another plugin replaces the vnavmesh destination, Nexus marks its intent superseded and releases only its own lease—never stopping the replacement plugin's movement.

Manual-movement yielding observes FFXIV's configured movement actions, covering remapped keyboard controls, mouse steering, gamepad movement, jump, and autorun. Physical player input blocks a new movement start; during tracked execution, takeover latches verified Stop and cannot silently auto-resume after input ends. The character setting must remain enabled for navigation readiness.

Reload reconciliation and active lease enforcement remain connected behind the same assessment. Before provider movement, Nexus atomically persists a minimal execution ID, route ID, lease ID, and state—never an instruction pointer. A plugin reload, shutdown, manual takeover, explicit Stop, or missed heartbeat can only latch verified Stop, confirm movement inactive, and wait for a separate acknowledgement. Corrupt or unwritable intent remains fail-closed. The watchdog and execution monitor run every draw, including while the Nexus window is closed.

The navigation-authority handoff remains a reversible, session-only decision. Nexus will not disable or enable VieriNavPlotter. Each route start rechecks the source, current character automation setting, manual-input quiet period, territory, provider readiness, and every ownership/safety prerequisite before movement. If the source reappears or safety is lost, Nexus revokes authority and safely ends tracked work. Returning to staging changes no source plugin, and reload never remembers approval.

The Routes page now includes live provider-health and transition-audit panels for vnavmesh Stop, manual movement, reload/watchdog state, predecessor ownership, the atomic resource bundle, and Nexus authority. These observations also populate the shared world snapshot instead of leaving provider health empty. A user-triggered non-moving simulation runs the production safety coordinators against isolated memory-only leases, journals, and a Stop-only provider. It checks guarded start/Stop, manual takeover, reload recovery, provider loss/retry, source return, and lease expiry without touching live ownership, the live journal, or any movement provider.

If execution is stopped by manual takeover, explicit Stop, reload recovery, source return, or lease expiry, a dedicated checkpoint panel becomes actionable only after Stop is confirmed, Navigation/Movement ownership is released, and the player-input quiet period has elapsed. Acknowledgement marks stopped intent complete and clears the manual-yield latch; it never resumes/replays movement or approves Nexus authority.

## Current safety guarantees

- Existing Vieri configurations are discovered read-only and left in place.
- Discord credentials and channel/message IDs are not read, decrypted, logged, or rewritten.
- The Communications discovery card checks only whether VieriLink configuration exists; it does not open that file.
- Route imports preserve disabled overrides as disabled and never activate navigation automatically.
- Migration staging remains immutable. Nexus edits only its separate working library, and read-only IPC reports the active working copy when present.
- A loaded VieriNavPlotter is reported as the current route owner and blocks Nexus approval. Nexus never disables or enables it automatically.
- Stop completion requires a separate inactive-path confirmation; sending a Stop request alone never releases navigation ownership.
- Player movement input takes priority, blocks a new Nexus movement start through its quiet period, and requires an explicit resume decision after interrupting tracked navigation.
- Reload recovery never replays a saved movement instruction; it stops first and requires explicit acknowledgement.
- Expired ownership leases are reported to the active watchdog and trigger the same fail-closed Stop path instead of disappearing silently.
- Navigation-authority approval is session-only, starts no route automatically, can be returned to staging, and is revoked if the predecessor or another owner conflicts.
- Same-zone route travel/playback must enter through the guarded executor; cross-zone dispatch remains disabled.
- A replacement shared-provider path causes Nexus to yield its own lease without sending Stop to the new owner.
- Provider health is observed read-only and its bounded session audit records transitions rather than every frame.
- The non-moving safety simulation uses isolated memory-only state and has no movement operation.
- A stopped-intent checkpoint cannot be acknowledged until Stop is confirmed, ownership is released, and manual input is quiet; acknowledgement has no resume operation.
- No migration-source plugin is disabled automatically.
- Nexus starts route movement only from an explicit Routes-page or `/nexus play <name>` action after working-library and authority approval.
- The Nexus window waits until a targetable character is fully in the world.
- The supplied VieriNexus logo is the permanent Home experience, not a temporary popup window.
- Questionable is not a runtime dependency. Its maintained quest engine and route data are incorporated through VieriCodex and will migrate into Nexus.

The complete required/recommended provider inventory and its current-product evidence are recorded in [`docs/DEPENDENCY_AUDIT.md`](docs/DEPENDENCY_AUDIT.md).

## Build

```powershell
dotnet build .\VieriNexus.slnx -c Release
```
