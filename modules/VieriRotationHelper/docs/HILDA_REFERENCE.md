# Hilda display reference

The Hilda owner authorized the project to inspect and reuse Hilda behavior.
VieriRotationHelper 1.0.0.2 ports the keyboard overlay presentation from the
pinned Hilda 7.5.1.0 assemblies. The earlier 1.0.0.1 labels/badges were not a
visual match and have been replaced. Reference locations:

- `PrioritySetGroup` / `DrawUtils`: 65px lead, 50px secondary, centered row,
  3px spacing setting (6px between icons), 20px inset;
- `PriorityIcon`: 95% inset action image, `IconA_Frame_hr1.tex` UV crop,
  `IconA_Recast_hr1.tex` 81-cell sweep, actual Font Awesome positional/weave
  glyphs, and Hilda's overlay positions/colors;
- `PriorityIconKeybind` / `FontManager`: Miedinger 26px font, 0.7 key scale,
  white text with an eight-direction one-pixel black outline;
- `HotbarManager`: game hotbar labels and modifier normalization, with added
  node bounds/type checks and Wrath live replacement-button resolution;
- `ComboMaps.ActionSets`: all 167 natural upgrade/transform keybind families
  (481 unique actions); no unrelated XIVCombo preset assumptions;
- transparent/titleless bar windows with zero padding and no extra status text.

The existing behaviors remain:

- hostile, targetable, living-enemy counting around the player or target;
- action-radius-aware target counts;
- out-of-range icon dimming;
- flank and rear positional markers on action icons;
- main-icon enemy-count presentation; and
- icon cooldown and GCD timing feedback (no separate progress strip).

No Hilda assembly is redistributed or required at runtime. The authorized
Miedinger display asset is embedded in the plugin assembly. The source pin and
SHA-256 hashes are recorded in `upstream/hilda.lock.json` so a future Hilda
update can be audited deliberately without silently changing behavior.

Verification: compiled geometry/UV/hotkey contracts, resource hash checks,
Release build and package checks. No live side-by-side pixel comparison has
been performed; the tests establish source-level presentation parity, not a
claim that every customized Hilda configuration is identical.
