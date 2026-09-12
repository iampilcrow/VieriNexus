# Questionable provider migration audit

## Pinned final trees

- Vieri migration source: VieriCodex 1.12.2.81, commit `1bc749b1e6f04519608b04caaca21c882e28412f`.
- Stock base: Questionable 15.756.2.4, tag commit `f1b6da5ee5a9509c2f955f1fa93e73d5bfc4ffb8`.
- The Vieri final tree is 169 commits and 266 files beyond that stock tag. This is an inventory boundary, not a claim that every changed line belongs in Nexus.
- The 15.756.2.4 integration changed 305 files relative to the previous incorporated 15.756.0.1 tag. All 8,919 Questionable tests, 4,337 semantic route validations, and the generator test pass after the merge.

## Permanent provider contract

Nexus uses only the stock-compatible single-quest boundary:

- `IsRunning`
- `GetCurrentQuestId`
- `StartSingleQuest`
- `IsQuestLocked`
- `IsReadyToAcceptQuest`
- `IsQuestAccepted`
- `IsQuestComplete`
- `Stop`

Nexus owns the exact quest choice, desired level, task history, resource leases, completion checkpoint, fallback limit, Stop-after decision, recovery, and decision to schedule another activity. Questionable owns execution of the one exact supported quest it is given.

VieriCodex and stock Questionable have distinct internal and IPC names, so Dalamud can load both without an IPC collision. Nexus 0.1.0.51 therefore inventories both identities and reports a hard conflict whenever both are loaded, before it invokes either contract. With only VieriCodex loaded, the compatibility contract remains available. With only stock Questionable loaded, stock is selected automatically. Nexus never disables either plugin itself.

## Vieri behavior inventory

The final-tree audit divides the Vieri delta into these ownership groups:

1. **Already Nexus-owned:** target-level planning; Class/Job/Role and ordinary side-quest selection; gear-readiness ordering; Hunting and Grand Company Logs; Aetherytes/Aethernet; field and quest Aether Currents; Mapping/Remapping exploration; achievement progress; Progress Atlas completion state and bounded actions; named personal routes; cross-zone travel; Stop and reload reconciliation; and the compact Nexus overlay.
2. **Remain protected until final retirement:** live gearset refresh and job eligibility filtering, empty-candidate protection, the corrected solo-duty rotation/targeting lease, Vieri-specific route corrections not yet present in stock data, and any saved configuration still needed for local migration.
3. **Stock Questionable responsibility:** quest path content and updates, ordinary quest step execution, supported interactions, navigation requests, dialogue/cutscene flow, and the stock integration adapters it publishes.
4. **Not copied into Nexus:** the broad VieriCodex queue/window implementation, Questionable's internal quest engine, and duplicate copies of stock route data. Nexus retains the resulting product behavior through its own planners and the narrow provider boundary.

## Current upstream integration

VieriCodex 1.12.2.81 incorporates Questionable 15.756.2.4's current quest and gathering routes, missing Kugane side quests, standardized IPC health base, interaction-wait combat handling, missing-gearset failure, class-switch safeguards, Aetheryte and Unlocks journal improvements, and current quest-selection/navigation fixes. The merge preserves Vieri branding, commands, saved settings, Progress Atlas, Progression Queue, named VieriNavPlotter routes, Hunting Log extensions, gear readiness, and solo-duty rotation coordination.

The merged acceptance path keeps `CodexActivityPolicy.SelectAvailableQuestJobs`, refreshes the live gearset list, rejects an empty eligible result before indexing it, and then uses the upstream candidate and missing-gearset failure flow. This is a required regression boundary for future updates.

## Retirement gates

VieriCodex can be disabled permanently only after all of the following are true:

1. Nexus and local predecessor settings have been migrated and verified on that computer.
2. Progress Atlas, Progression, routes, logs, travel nodes, currents, exploration, achievements, gear readiness, and Stop/reload behavior remain available from Nexus without calling VieriCodex-only planners or UI.
3. The current stock Questionable package exposes the complete permanent contract above and successfully runs representative MSQ, Class/Job/Role, ordinary side-quest, quest-current, and combat/solo-duty paths.
4. Every Vieri route correction still relevant to live content is either present in current stock, contributed upstream, or isolated as Nexus-owned data with provenance and regression coverage.
5. The solo-duty rotation handoff is owned by Nexus or proven equivalent through stock provider behavior without VieriCodex.
6. VieriCodex is disabled before stock Questionable is enabled; a both-loaded state is intentionally blocked.

Routine compatible Questionable updates should then require only a contract and representative-path audit. They must not require a Nexus source change or a manual fork merge.
