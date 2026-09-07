# VieriNexus

VieriNexus is the in-progress unified home for the Vieri FFXIV plugin suite.

The first public build is an early testing foundation. Install it alongside the existing Vieri plugins; it does not replace or disable them yet.

The foundation build intentionally does not replace live automation. It provides the application shell, module registry, dependency gate, character-scoped settings, shared world snapshots, resource ownership, migration discovery, and versioned IPC contracts needed for safe incremental consolidation.

## Current safety guarantees

- Existing Vieri configurations are discovered read-only and left in place.
- Discord credentials and channel/message IDs are not read, decrypted, logged, or rewritten.
- No migration-source plugin is disabled automatically.
- No gameplay automation is started by the foundation build.
- UI and splash rendering wait until a targetable character is fully in the world.

## Build

```powershell
dotnet build .\VieriNexus.slnx -c Release
```
