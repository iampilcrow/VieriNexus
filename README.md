# VieriRotationHelper

VieriRotationHelper is a unified combat suite for Final Fantasy XIV. It hosts
the full pinned Wrath rotation engine and Auto-Rotation system, the complete
VieriWrathSwitch overlay and safety controls, and separate single-target, AoE,
and live dynamic suggestion bars for every standard combat job.

The dynamic bar changes between single-target and AoE suggestions from the
number of valid hostile targets within the active AoE's real radius. Action
icons use Hilda's game-frame and recast textures, side-by-side 65/50-pixel
layout, outlined keyboard hotkeys in its Miedinger font, green positional
symbols, weave symbols, charge counts, enemy counts, and range dimming.
No spell names, mode banners, colored lead borders, or separate GCD strips
appear on the bars. Open settings and hover an icon for names/diagnostics.

## Suite model

- The execution engine and suggestion overlay use the same bundled, pinned
  Wrath PvE rules for all combat jobs and base classes, including actual gauges,
  statuses, cooldowns, level restrictions, targets, openers, and action history.
- Action replacement, Auto-Rotation, targeting, retargeting, IPC leases, and
  advanced job settings are provided directly by VieriRotationHelper. The
  separate Wrath Combo plugin is not required.
- Forecast icons use a thread-local shadow combat timeline. It advances shared
  combo/cooldown/action history plus every standard job's deterministic gauges,
  player buffs/procs/stacks, target debuffs, dance/transformation steps, and
  resource spends/gains. All three bars therefore re-run Wrath against the
  projected result of each prior icon instead of stale live state. Game actions,
  movement, targeting, retargeting, configuration writes, and item use are
  suppressed while that forecast is calculated, without disabling those
  features for the live rotation engine.
- The embedded switch preserves Manual Movement / Targeting Only, In Combat
  Only, authoritative ON/OFF control, keybinds, the movable overlay, and its
  automation handoff behavior. The separate VieriWrathSwitch plugin is not
  required.
- Exact `WrathCombo.*` and `WrathSwitch.BeginAutomation` IPC names are retained
  for VieriCodex, VieriAutoDuty, AutoDuty, BossMod, Avarice, and other clients.
- Debug mode exposes live source and parity state in settings-open tooltips.

## Commands

VieriAvarice 2.2.0.18+ automatically consumes the suite's upcoming positional
guidance. Rear/flank hints use the integrated Wrath evaluator even with the
suggestion bars hidden, and prioritize the nearest positional rather than a
later opposite-side action. No separate Wrath plugin is required.

- `/vrh` opens settings.
- `/vrh toggle` enables or disables the suggestion bars.
- `/wrath` and `/wrathcombo` open and control the integrated rotation engine.
- `/wrathswitch` and `/ws` control the integrated switch and safety modes.

In **Keybinds**, record a keyboard shortcut to toggle the suite settings window,
with Ctrl/Shift/Alt, exact-modifier matching, Enable, Clear, and Escape to cancel.
No shortcut is assigned by default. This window shortcut does not change
rotation or bar visibility; existing F1/switch shortcuts remain in Switch settings.
Use an unused combination: other game/plugin bindings are not intercepted.

All three suggestion bars open automatically in separate default positions and are visible
out of combat by default so they can be positioned immediately. Each fixed bar
and the dynamic bar can be toggled separately.
Keyboard hotkeys are read from the current character's hotbar labels and
refresh within 100 ms after layout changes. Each displayed key must currently
resolve to that action: consolidated buttons retain their key; separate actions
use their separate keys; native upgrades and transformations retain the actual
working button. Unbound/unknown actions have no invented hotkey. Unlock bars to
drag them into place.

## Upstream maintenance

Wrath provenance is pinned in `upstream/wrath.lock.json`. Run
`scripts/Audit-WrathUpdate.ps1` with a newer Wrath checkout to produce the
all-job compatibility report before integrating an update. See
`docs/WRATH_INTEGRATION.md`.

When upgrading from the separate plugins, VieriRotationHelper imports the last
Wrath options and VieriWrathSwitch settings into its own configuration. The
original configuration files are left untouched. Disable the separate Wrath
Combo and VieriWrathSwitch plugins, then reload once so only the suite owns the
action hook and compatibility IPC providers.

Version 2.0.0.3 refreshes existing hotbar action display classifications when
the integrated engine loads or action replacement changes. This addresses
buttons retaining their original icon (such as Spinning Edge) even while the
engine resolves a replacement. Actions and hotbar layouts are preserved.
The 2.0.0.2 switch lease ownership and consolidated forecast hotkey fixes remain.

The authorized Hilda display reference is pinned separately in
`upstream/hilda.lock.json`; see `docs/HILDA_REFERENCE.md`.
