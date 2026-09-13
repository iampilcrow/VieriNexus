# Integrated Wrath positional guidance

VieriAvarice 2.2.0.18 uses VieriRotationHelper 2.0.0.5's versioned positional IPC
before the optional Rotation Solver or standalone heuristics. No new setting is
required; existing anticipation visibility, colors and True North options stay
in effect. The Positional Anticipation settings show the source/action status.

The provider receives the drawn target's GameObjectId and returns primitive
uint fields: schema, available, action, side, actions ahead, current job.
An available response with no positional suppresses fallback guesses. A missing
or inactive provider retains standalone functionality. The current-target
prediction is never applied to a different focus target. The suite computes
short lookahead independently of whether suggestion bars are drawn.

Damage feedback is separate: only verified table rows produce HIT or MISS.
Unrecognized values and absent damage are Unknown, visible in the debug window,
and do not change hit/miss statistics or trigger failure feedback. Missing result
values need observed evidence before adding rows, not guesses from damage alone.

Verification: both Release builds; suite metadata/lookahead checks for MNK, DRG,
NIN, SAM, RPR, VPR and their applicable base classes; executable verdict checks.
Live combat timing/rotation parity must also be tested in-game, including hidden
bars, one-icon bars, True North, job/target changes, ST/AoE transitions and plugin
unload/reload. Never claim full all-job live parity from source checks alone.

From VieriAvarice 2.2.0.19 and VieriRotationHelper 2.0.0.6, positional guidance
and visible suggestions consume the identical Wrath sequence cached for the
current game frame. The provider no longer reevaluates independently during an
IPC call. The suite keeps eight actions internally for anticipation while the
user's visible action-count setting only controls how many icons are drawn.
