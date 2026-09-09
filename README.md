# VieriNexus

VieriNexus is the in-progress unified home for the Vieri FFXIV plugin suite.

Its ninth migration source is VieriNavPlotter: a reusable named-route library for recording, manually plotting, previewing, and assigning navigation paths across Nexus modules.

The first public build is an early testing foundation. Install it alongside the existing Vieri plugins; it does not replace or disable them yet.

The foundation build intentionally does not replace live automation. It provides the application shell, Home experience, module registry, dependency gate, character-scoped settings, shared world snapshots, resource ownership, migration discovery, and versioned IPC contracts needed for safe incremental consolidation.

Routes & Navigation is the first transactional migration slice. Its Migration card previews every VieriNavPlotter setting, creates a timestamped backup, writes a staged Nexus-owned route library atomically, records hashes and a rollback receipt, and can restore the prior Nexus state. Importing does not activate Nexus navigation or disable VieriNavPlotter.

Staged migration status survives a Nexus reload: the saved receipt, target path, target hash, schema, and staged payload are verified before the import message and rollback action are restored.

## Current safety guarantees

- Existing Vieri configurations are discovered read-only and left in place.
- Discord credentials and channel/message IDs are not read, decrypted, logged, or rewritten.
- The Communications discovery card checks only whether VieriLink configuration exists; it does not open that file.
- Route imports preserve disabled overrides as disabled and never activate navigation automatically.
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
