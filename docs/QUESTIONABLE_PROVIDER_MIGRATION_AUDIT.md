# Questionable provider migration audit

## Pinned final trees

- Vieri migration source: VieriCodex 1.12.2.82, commit `173d6ad599d2c057e0f88cea76ed302a7746bf32`.
- Stock base: Questionable 15.756.2.5, tag commit `e21fec6934db687829b9530394a709a5c1eb1d52`.
- The Vieri final tree is 170 commits and 266 files beyond that stock tag. This is an inventory boundary, not a claim that every changed line belongs in Nexus.
- The 15.756.2.5 integration changed 149 files relative to 15.756.2.4. All 8,919 Questionable tests, 4,337 semantic route validations, and the generator test pass after the merge.

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

VieriCodex and stock Questionable have distinct internal and IPC names, so Dalamud can load both without an IPC collision. Nexus inventories both identities and reports a hard conflict whenever both are loaded, before it invokes either contract. With only VieriCodex loaded, the compatibility contract remains available. With only stock Questionable loaded, stock is selected automatically. Nexus never disables either plugin itself.

## Vieri behavior inventory

The final-tree audit divides the Vieri delta into these ownership groups:

1. **Already Nexus-owned:** target-level planning; Class/Job/Role and ordinary side-quest selection; gear-readiness ordering; Hunting and Grand Company Logs; Aetherytes/Aethernet; field and quest Aether Currents; Mapping/Remapping exploration; achievement progress; Progress Atlas completion state and bounded actions; named personal routes; cross-zone travel; Stop and reload reconciliation; and the compact Nexus overlay.
2. **Remain protected until final retirement:** live gearset refresh and job eligibility filtering, empty-candidate protection, five verified route corrections not yet present in stock data, and any saved configuration still needed for local migration. Nexus 0.1.0.52 now owns the corrected solo-duty rotation/targeting lease for stock Questionable.
3. **Stock Questionable responsibility:** quest path content and updates, ordinary quest step execution, supported interactions, navigation requests, dialogue/cutscene flow, and the stock integration adapters it publishes.
4. **Not copied into Nexus:** the broad VieriCodex queue/window implementation, Questionable's internal quest engine, and duplicate copies of stock route data. Nexus retains the resulting product behavior through its own planners and the narrow provider boundary.

## Current upstream integration

VieriCodex 1.12.2.82 incorporates Questionable 15.756.2.5's current Beastmaster and Chocobo paths, broad route metadata corrections, editable inactive Stop settings, and scrollable long-comment field. The merge preserves Vieri branding, commands, saved settings, Progress Atlas, Progression Queue, named VieriNavPlotter routes, Hunting Log extensions, gear readiness, and solo-duty rotation coordination.

The merged acceptance path keeps `CodexActivityPolicy.SelectAvailableQuestJobs`, refreshes the live gearset list, rejects an empty eligible result before indexing it, and then uses the upstream candidate and missing-gearset failure flow. This is a required regression boundary for future updates.

Nexus 0.1.0.57 removes MSQ selection from that protected fork boundary. Main Scenario quests are classified from current game data, prioritized after already-accepted work and before other new quest kinds, pinned as exact durable Nexus tasks, and delegated through the same stock-compatible one-quest contract. VieriCodex configuration migration now preserves every activity/Atlas preference and the complete saved queue definition transactionally; a matching current-job queue step becomes fresh Nexus desired state without replaying the predecessor's runtime pointer.

Nexus 0.1.0.58 closes the remaining local route boundary without an upstream submission. It layers only the five exact corrections below onto Questionable's complete downloaded route bundle, retains an exact hash-addressed official backup and receipt, verifies the result before atomic replacement, and asks stock Questionable to reload only after its public activity contract confirms it is idle. Nexus blocks only an affected quest until that corrected bundle is actually loaded. Every other quest and all quest execution remain stock Questionable-owned.

## Exact remaining route delta

After ignoring line endings, BOMs, and formatting-only changes, only five Vieri route corrections remain semantically different from stock 15.756.2.5:

1. Monk `A Slave to the Aether`: correct the guarded next quest from `1604` to `1064`.
2. Bard `Sleeping Truths Lie`: retain flight for the long combat approach.
3. Samurai `The Face of True Evil`: explicitly enable its supported solo duty.
4. Sage `Sage's Focus`: retain flight on both resumable approaches to the solo duty.
5. Island Sanctuary `The Land, Wind, and Sea`: do not mount for the indoor accept/turn-in points.

They were originally isolated from all Vieri UI and automation code as commit `b9bbe51d2` on branch `nexus-route-fixes-15.756.2.5`, where the complete 4,337-route semantic validator passes. That branch is retained only as provenance and will not be pushed or submitted upstream at the user's direction.

The set was rechecked against upstream `new-main` at `5a751c819` and the independently updated official route bundle data version `1789230462` (SHA-256 `FCBA23F671852C996650CE83417E9F52154564976AB1BFB47811416D3258596A`) on 2026-09-12. All five corrections are still absent. The Nexus compatibility pack locates each route by exact quest ID, validates the expected sequence, object, territory, interaction, and old value, then edits only that ZIP entry. A missing route, duplicate route, unexpected shape, or changed value is a conflict: the whole bundle is left untouched and the affected quests remain blocked. When stock data already contains all corrections, Nexus recognizes it as native and performs no write.

Questionable updates remain automatic. A changed bundle immediately revokes readiness, is rechecked while Questionable is disabled or confirmed idle, and receives the same guarded layer only when still required. Rollback restores the byte-identical official bundle only if both current and backup hashes still match the receipt. Nexus never changes Questionable's DLL, settings, provider contract, or unrelated route entries.

## Nexus-owned solo-duty handoff

Nexus 0.1.0.52 detects only the intersection of a Nexus-owned exact quest, stock Questionable selection, an entered duty, and a ready Wrath-compatible public IPC. It then starts a fresh rotation lease, readies the current job, applies the proven healer/DPS support settings, and selects Questionable's movement-only Boss Mod preset. Boss Mod remains the sole encounter-movement owner. Nexus never rewrites the player's hard target; after two seconds without a usable hostile target it pulses Wrath's nearest-hostile action targeting for at most 1.5 seconds, then returns to selected-target mode.

The lease is released when the exact Nexus quest/duty boundary ends or Nexus shuts down. If the rotation provider is absent, rejects the lease, loses it, or rejects a setting, Nexus restores Questionable's normal Quest Battle preset and suppresses another Wrath attempt until the next solo duty. The handoff is inactive for VieriCodex and for ordinary AutoDuty runs, preventing duplicate controllers during migration.

## Retirement gates

VieriCodex can be disabled permanently only after all of the following are true:

1. Nexus and local predecessor settings have been migrated and verified on that computer.
2. Progress Atlas, Progression, routes, logs, travel nodes, currents, exploration, achievements, gear readiness, and Stop/reload behavior remain available from Nexus without calling VieriCodex-only planners or UI.
3. The current stock Questionable package exposes the complete permanent contract above and successfully runs representative MSQ, Class/Job/Role, ordinary side-quest, quest-current, and combat/solo-duty paths.
4. Every Vieri route correction still relevant to live content is either present in current stock, contributed upstream, or isolated as Nexus-owned data with provenance and regression coverage.
5. The Nexus-owned stock-Questionable solo-duty rotation handoff passes representative in-game entry, combat, bounded target fallback, Stop, and duty-exit validation without VieriCodex.
6. VieriCodex is disabled before stock Questionable is enabled; a both-loaded state is intentionally blocked.

Routine compatible Questionable updates should then require only a contract and representative-path audit. They must not require a Nexus source change or a manual fork merge.
