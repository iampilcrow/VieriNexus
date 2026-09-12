# VieriNexus dependency audit

This catalog was checked against the live integration and dependency surfaces in VieriAutoDuty, VieriAutoMarket, VieriAvarice, VieriCodex, VieriDeck, VieriDelvUI, VieriLink, and VieriRotationHelper.

The 2026-09-09 provider audit pins Boss Mod 7.5.6.0 (`a96b0a4614a8ae28c6fc7949ec221207c56bd5f9`) and Lifestream 2.5.4.21 (`62a6f68fc0966530f6baa380c1082e41f6cd6d7c`) in `upstreams/source-lock.json`. Boss Mod retains every preset/configuration/action-queue IPC endpoint and rotation-module identifier used by AutoDuty, Codex, Avarice, and RotationHelper. Its transient-strategy implementation now resolves the existing string identifiers through the rotation-module registry, with no public signature change. Lifestream retains every travel, busy-state, character-switch, command, and abort endpoint used by AutoDuty and Codex; 2.5.4.21 changes internal duty-transfer scheduling only. VieriDeck contributes fixed chat-command shortcuts and has no binary Lifestream IPC contract. No consumer compatibility patch or unrelated rebuild is required for either provider update.

The 2026-09-12 final AutoDuty recovery audit confirms that released stock 0.0.0.335 and current upstream development still lack the fork's generic stale-path/death/re-entry protections. The minimal stock patch is isolated on `nexus-recovery-fixes-0.0.0.335` at `2c583870c216573a054af109d11fafdd85d556be`, passes its five dedicated policy tests, and builds against the stock tree. Nexus retains only its outer bounded-duty ownership and completion checks; it does not absorb AutoDuty's internal path engine.

## Required external providers

| Provider | Current Vieri consumers | Purpose |
| --- | --- | --- |
| Boss Mod | AutoDuty, Codex, Avarice, RotationHelper | Duty mechanics, supported solo duties, encounter movement and positional coordination |
| vnavmesh | AutoDuty, Codex, RotationHelper | Navigation meshes and in-zone movement |
| Lifestream | AutoDuty, Codex, Deck | Aetheryte, Aethernet, world, and character travel |
| TextAdvance | Codex | Quest acceptance, turn-in, dialogue, and cutscene progression |
| Marketbuddy | AutoMarket | Configured retainer price application |
| Allagan Market | AutoMarket | Market ownership, price state, and undercut intelligence |

## Recommended integrations

| Provider | Current Vieri consumers | Added capability |
| --- | --- | --- |
| AutoRetainer | AutoDuty | Retainer cycles, GC turn-ins, protected selling/discarding, Multi Mode |
| Glamour Log | AutoDuty | Glamour Dresser and Armoire ownership and entrusting |
| Anti-AFK | AutoDuty | Long-session AFK protection |
| Pandora's Box | AutoDuty, Codex | Active Time Maneuvers, chests, tank stance, and instance interactions |
| Gearsetter | AutoDuty | Recommended gear across Armoury Chest and inventory |
| Stylist | AutoDuty, Codex | Gearset organization and recommended equipment |
| Fast Job Switcher | Codex | Unattended Progression Queue job changes |
| CBT | Codex | Automated quest sniper sequences |
| Artisan | Codex | Quest crafting |
| AutoHook | Codex | Quest fishing |
| Mogmail | Codex | Mailed quest-item claims |
| NotificationMaster | Codex | Manual-attention alerts |
| SelectString | Codex | Numbered dialogue and menu selection |
| QuestMap | Codex | Quest and reward discovery |
| YesAlready | AutoDuty | Compatible confirmation automation with exclusive-control pausing |
| Skippy | AutoDuty | Optional supported MSQ duty playback skipping |

## Planned module-scoped providers

| Provider | Nexus module | Transition rule |
| --- | --- | --- |
| Stock Questionable | Progression / Questing | The 0.1.0.26 read-only adapter verifies `IsRunning`, `StartSingleQuest`, and `Stop` without invoking them. VieriCodex remains authoritative until Nexus owns its custom planners, policies, route overlay, safety, and UI and the stock capability contract passes parity. |
| Stock AutoDuty | Duties | The 0.1.0.26 read-only adapter verifies `ContentHasPath`, `Run`, `IsStopped`, and `Stop` without invoking them. VieriAutoDuty remains authoritative until Nexus owns its custom route/travel, gear/inventory, maintenance, progression-loop, Last Run, telemetry, command, and UI behavior and the stock duty contract passes parity. See `AUTODUTY_PROVIDER_MIGRATION_AUDIT.md`. |

These providers are required only when their owning module is enabled. They must not become global first-run blockers.

## Intentionally not permanent external dependencies

- During migration, Questionable is incorporated through VieriCodex's maintained quest engine and route library and must not be installed beside VieriCodex. The approved end-state moves Vieri-specific behavior into Nexus, retires the full fork only after parity, and then uses stock Questionable as a capability-versioned provider for ordinary supported quests.
- Questionable 15.756.2.5 is incorporated through VieriCodex 1.12.2.82 at source commit `173d6ad599d2c057e0f88cea76ed302a7746bf32` (upstream tag commit `e21fec6934db687829b9530394a709a5c1eb1d52`). It adds current Beastmaster/Chocobo paths, route metadata, inactive Stop editing, and long-comment scrolling while retaining the Vieri live-gearset guard, Progress Atlas, Progression Queue, named routes, and solo-duty safeguards. Nexus 0.1.0.52 owns the stock-Questionable solo-duty Wrath/Boss Mod handoff. Nexus 0.1.0.58 manages the five remaining semantic route corrections as an atomic, reversible compatibility layer over official data version `1789230462`; all other route data and execution remain stock-owned. See `QUESTIONABLE_PROVIDER_MIGRATION_AUDIT.md`.
- The eight Vieri forks/products are temporary migration sources, not final third-party dependencies. This does not exclude their stock upstreams from becoming module-scoped providers after parity.
- Wrath Combo and VieriWrathSwitch are already incorporated into VieriRotationHelper and migrate as Nexus's embedded combat engine.
- Rotation Solver Reborn and BossMod AutoRotation are alternative rotation engines, not requirements for the Nexus combat engine.
