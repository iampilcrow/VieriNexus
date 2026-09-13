# Nexus-owned module architecture

Version 0.1.0.61 packages the remaining custom Vieri runtimes inside the single
`VieriNexus` Dalamud archive. They are not separately installed plugins and are
not loaded into Nexus's own assembly context.

## Included custom runtimes

| Nexus page | Internal runtime | Replaces after local preparation |
| --- | --- | --- |
| Combat | Rotation suggestions, embedded Wrath engine, switch overlay, and hotkeys | VieriRotationHelper |
| Combat | Positional forecasts, profiles, encounter feedback, and overlay | VieriAvarice |
| Custom UI | Complete customized HUD, profiles, highlighting, nameplates, markers, party roles, and ready checks | VieriDelvUI |
| Market | Owned-retainer matching, guarded repricing, pacing, confirmation, and verification | VieriAutoMarket |
| Communications | Encrypted Discord status, notifications, recovery, authorization, and Nexus remote commands | VieriLink |

VieriDeck and VieriCodex were already replaced by native Nexus pages and
services. VieriNavPlotter routes are already held in the Nexus working library.
VieriAutoDuty-specific routes, gear, maintenance, overlay, command, and telemetry
behavior is already Nexus-owned; ordinary duty execution is delegated to stock
AutoDuty through its narrow IPC contract.

## Isolation and lifecycle

Each packaged runtime is a nested ZIP with its own collectible assembly load
context. Dalamud, FFXIVClientStructs, Lumina, Newtonsoft.Json, Serilog, and the
interop runtime are shared with the host; module-private dependencies such as
ECommons and PunishLib remain isolated. This prevents one imported engine's
dependency versions from replacing another engine's dependencies or Nexus's.

Nexus forwards the real Dalamud services required by each module, but supplies a
module-scoped plugin interface, UI callback surface, assembly directory, and
configuration directory. Draw callbacks continue normally. Open-config and
open-main callbacks are captured so opening Nexus does not open every embedded
window; each module's settings are opened from its owning Nexus page.

A module never runs beside its standalone predecessor. While the predecessor is
loaded, Nexus prepares settings and reports that it is waiting. When the user
disables the predecessor, Nexus starts the internal runtime automatically. If a
module fails to initialize, that module alone is stopped and the page exposes
the exact failure plus Retry; the rest of Nexus stays active.

## Per-computer settings handoff

The handoff is deliberately local because Dalamud settings and Discord secrets
belong to the current Windows user and character:

1. Install and enable Nexus while the existing Vieri plugins and their settings
   still exist on that computer.
2. Nexus copies each located source into a timestamped backup and a separate
   Nexus-owned working configuration. Existing files are never overwritten.
3. Disable the matching standalone Vieri plugin.
4. Nexus starts the prepared internal runtime automatically. Future settings are
   saved only in the Nexus-owned module directory.

VieriLink's bot token is never printed or logged. Before Nexus copies a protected
configuration, it must decrypt, re-encrypt, decrypt, and fixed-time compare the
token under Windows CurrentUser protection. A token that cannot be proven usable
by the current Windows account blocks that module's handoff without affecting
the other modules.

This same install-first order is required on a friend's computer. No local
configuration, character state, identifiers, or secrets are distributed in the
Nexus release package.

## External providers

The consolidation does not copy general-purpose provider engines into Nexus.
Stock Questionable owns ordinary quest execution; Nexus layers only its five
validated route corrections when the exact matching route bundle is present.
Stock AutoDuty owns ordinary duty execution. Boss Mod, vnavmesh, Lifestream, and
Fast Job Switcher remain external providers behind their narrow contracts.
Provider updates therefore do not require rebuilding Nexus unless their public
contract changes or a guarded compatibility check detects a real incompatibility.

Before a fresh stock AutoDuty run, Nexus clears a stale vnavmesh path at the
outer provider boundary. Nexus never resumes an old provider instruction after
reload and does not claim access to provider-private execution state.

## Release verification

`scripts/verify-embedded-module-package.ps1` fails the release when any nested
archive is absent, empty, unsafe, or missing its entry assembly/required manifest
or default HUD profile. The normal Nexus build compiles all five modules first,
then places their archives under `EmbeddedModules/` in `latest.zip`.
