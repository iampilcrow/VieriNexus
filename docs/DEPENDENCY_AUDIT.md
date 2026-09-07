# VieriNexus dependency audit

This catalog was checked against the live integration and dependency surfaces in VieriAutoDuty, VieriAutoMarket, VieriAvarice, VieriCodex, VieriDeck, VieriDelvUI, VieriLink, and VieriRotationHelper.

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

## Intentionally not external dependencies

- Questionable is incorporated through VieriCodex's maintained quest engine and route library. It must not be installed beside VieriCodex or listed as a Nexus dependency.
- The eight Vieri products are temporary migration sources, not final third-party dependencies.
- Wrath Combo and VieriWrathSwitch are already incorporated into VieriRotationHelper and migrate as Nexus's embedded combat engine.
- Rotation Solver Reborn and BossMod AutoRotation are alternative rotation engines, not requirements for the Nexus combat engine.
