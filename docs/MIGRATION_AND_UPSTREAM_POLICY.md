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
12. Manual Gear shopping must remain a complete transaction: honor the approved item preview and gil reserve, purchase the selected upgrades, equip and verify every approved replacement with one idempotent retry, update the current gearset, then transfer every newly displaced weapon, armor piece, and accessory from the Armoury Chest into normal inventory before releasing the Gear lease. Every automatic/manual gear-shopping, selling, repair, card-exchange, and GC turn-in route must stop its active movement inside the shared travel loop as soon as either the game's Event NPC distance or hitbox-aware geometry reports that the current targetable vendor is within normal interaction range. If that live Event NPC state is unavailable or delayed, a trusted vendor-coordinate fallback must stop the final approach at 4.25 yalms; it may apply only on the final route segment so authored waypoints remain ordered. Vendor travel must not wait for an outer helper poll, drive toward the NPC's center coordinate, touch nearby collision, or enter stall/repath recovery. Existing EXP-item and sell/desynth protections remain mandatory.
13. Until Nexus is authoritative for a module, a published fix to any predecessor Vieri product is not complete until Nexus pins that exact source revision and records the applicable behavior or regression requirement. Nexus may not claim runtime ownership before the corresponding embedded module and tests exist.
14. VieriNavPlotter migration must retain every custom route exactly: stable ID, name, notes, tags, ordered coordinates, territory, playback flags, tolerances, and explicit consumer assignments. A disabled override must remain disabled after migration.
15. Built-in route migration must preserve provenance and certainty: destination-only AutoDuty entries may not be presented as complete authored paths, while the corrected Domitien hall and Old Sharlayan stair coordinate sets remain visible, copyable reference templates. Domitien's wall-side NPC coordinate is forbidden as a movement point; navigation must approach directly through `Territory 133 | X 164.4264 | Y 15.5000 | Z -75.7035` and finish at the measured standing point `X 157.5930 | Y 15.7000 | Z -69.3316`. Route-point arrival uses a stable 0.75-yalm tolerance to avoid final-step repath/jump oscillation, while the wider NPC interaction completion radius is evaluated separately only after all explicit points are reached, including when a VieriNavPlotter override supplies the route.
16. Route review and playback parity is mandatory: every built-in and personal route must expose Show Route, Travel to Start/Destination, Play Route, and Stop Playback. Complete routes follow all saved points in order; destination-only entries remain labeled as generated navmesh approaches. Cross-zone execution must use the suite travel owner so teleport, Aethernet, flight, navmesh, and stall recovery do not compete. Nexus may render the complete live waypoint chain only while NavPlotter playback/travel owns navigation or the suite explicitly authorizes gear-shopping travel; ordinary Goto commands, inn travel, duties, and unrelated vnavmesh users must remain hidden. Existing visualization owned by outside plugins remains untouched.
17. Route authoring parity includes a horizontally resizable route-library pane and an always-available current-position inspector that displays and copies territory plus full-precision X/Y/Z coordinates.
18. Market migration must retain VieriAutoMarket's ownership-aware pricing and verified one-pass execution. Exact matching to another owned retainer must wait until the comparison window is closed and settled, write through the game's numeric price control, send the complete confirmation event expected by `AddonRetainerSell`, and verify the saved price before continuing. Automatic reference selection must detect a suspicious low-price cluster activated by a 1-gil listing (for example, `1, 5, 10,000` selects `10,000`), use the next logical external or owned-retainer reference, and leave the listing unchanged when no safe reference exists.

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
