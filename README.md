# VieriNexus

VieriNexus is the in-progress unified home for the Vieri FFXIV plugin suite.

The first public build is an early testing foundation. Install it alongside the existing Vieri plugins; it does not replace or disable them yet.

The foundation build intentionally does not replace live automation. It provides the application shell, Home experience, module registry, dependency gate, character-scoped settings, shared world snapshots, resource ownership, migration discovery, and versioned IPC contracts needed for safe incremental consolidation.

## Current safety guarantees

- Existing Vieri configurations are discovered read-only and left in place.
- Discord credentials and channel/message IDs are not read, decrypted, logged, or rewritten.
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
