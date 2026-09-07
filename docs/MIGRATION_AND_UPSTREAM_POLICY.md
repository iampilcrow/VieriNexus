# Migration and upstream policy

## Non-negotiable migration rules

1. Existing Vieri configuration files and directories remain untouched during discovery and import.
2. An importer writes a timestamped backup before creating Nexus-owned state.
3. Import happens into a staging model, validates, then commits atomically.
4. A failed import leaves the old product authoritative and Nexus inactive for that module.
5. Every setting receives an explicit mapping, intentional retirement reason, or compatibility default.
6. Character-owned data is keyed by content ID and world, not character name.
7. Discord secrets are never logged or included in support exports. Encrypted tokens move only on the same Windows account and remain encrypted.
8. Standalone plugins are not disabled or uninstalled automatically.
9. A migrated module must pass behavior, configuration, IPC, and rollback tests before becoming authoritative.
10. Progression-to-combat migration must retain the corrected solo-duty handoff from VieriCodex 1.12.2.76: begin a fresh rotation automation session on duty entry, keep selected-target behavior primary, enable nearest-hostile action targeting only after a sustained targetless gap, yield immediately when normal targeting recovers, expire every fallback assist automatically, never rewrite the player's hard target, and leave movement exclusively to the encounter provider.
11. Quest-route migration must retain VieriCodex route corrections over downloaded upstream data. In particular, every resumable Dravanian Hinterlands approach for `Sage's Focus` (accepted, duty-ready, and post-duty sequences 5, 6, and 8) must request flight when it is unlocked.
12. Manual Gear shopping must remain a complete transaction: honor the approved item preview and gil reserve, purchase the selected upgrades, equip and verify every approved replacement with one idempotent retry, update the current gearset, then transfer every newly displaced weapon, armor piece, and accessory from the Armoury Chest into normal inventory before releasing the Gear lease. Existing EXP-item and sell/desynth protections remain mandatory.
13. Until Nexus is authoritative for a module, a published fix to any predecessor Vieri product is not complete until Nexus pins that exact source revision and records the applicable behavior or regression requirement. Nexus may not claim runtime ownership before the corresponding embedded module and tests exist.
14. VieriNavPlotter migration must retain every custom route exactly: stable ID, name, notes, tags, ordered coordinates, territory, playback flags, tolerances, and explicit consumer assignments. A disabled override must remain disabled after migration.

## Upstream update workflow

Questionable, AutoDuty, Avarice, DelvUI, Wrath Combo, and other incorporated upstream work retain pinned provenance in `upstreams/source-lock.json`.

For every upstream update:

1. Fetch without changing the active Nexus source.
2. Record the old pin, candidate pin, release notes, file delta, and dependency/API changes.
3. Classify changes as upstream behavior, Vieri customization overlap, data-only update, IPC change, or packaging change.
4. Apply the update in the owning module only.
5. Reapply Vieri patches as explicit reviewable commits; never hide them in a bulk source replacement.
6. Run module contracts, saved incident replays, configuration migrations, legacy IPC tests, and clean-package validation.
7. Perform targeted in-game validation for the affected workflows.
8. Update the source lock and third-party notices together.
9. Publish only after the Nexus release gate passes.

Questionable quest/path data should remain isolated from the Nexus orchestration core so routine upstream data updates do not destabilize combat, communications, the HUD, or market modules.

## Rollback

Each migrated module keeps the pre-import backup and migration receipt. Rollback restores Nexus-owned settings from that receipt without deleting or rewriting the predecessor configuration. Discord keys, channel IDs, message IDs, and per-character settings are verified before the standalone source is retired.
