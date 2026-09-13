namespace VieriRotationHelper;

internal static class PositionalCatalog
{
    // Positional metadata mirrored from the authorized Hilda 7.5.1 job definitions.
    private static readonly Dictionary<uint, PositionalKind> Positionals = new()
    {
        [36947] = PositionalKind.Flank, // Pouncing Coeurl
        [56] = PositionalKind.Flank,    // Snap Punch
        [66] = PositionalKind.Rear,     // Demolish
        [3554] = PositionalKind.Flank,  // Fang and Claw
        [3556] = PositionalKind.Rear,   // Wheeling Thrust
        [25772] = PositionalKind.Rear,  // Chaotic Spring
        [88] = PositionalKind.Rear,     // Chaos Thrust
        [2258] = PositionalKind.Rear,   // Trick Attack
        [2255] = PositionalKind.Rear,   // Aeolian Edge
        [3563] = PositionalKind.Flank,  // Armor Crush
        [7481] = PositionalKind.Rear,   // Gekko
        [7482] = PositionalKind.Flank,  // Kasha
        [24382] = PositionalKind.Flank, // Gibbet
        [24383] = PositionalKind.Rear,  // Gallows
        [36970] = PositionalKind.Flank, // Executioner's Gibbet
        [36971] = PositionalKind.Rear,  // Executioner's Gallows
        [34610] = PositionalKind.Flank, // Flanksting Strike
        [34611] = PositionalKind.Flank, // Flanksbane Fang
        [34612] = PositionalKind.Rear,  // Hindsting Strike
        [34613] = PositionalKind.Rear,  // Hindsbane Fang
        [34621] = PositionalKind.Flank, // Hunter's Coil
        [34622] = PositionalKind.Rear,  // Swiftskin's Coil
    };

    internal static PositionalKind Get(uint actionId) =>
        Positionals.GetValueOrDefault(actionId, PositionalKind.None);
}
