using System.Collections.ObjectModel;

namespace VieriRotationHelper;

internal static class JobCatalog
{
    // The first two action IDs are Wrath's ST/AoE replacement entry points at
    // the pinned upstream revision. Combo arrays are the conservative embedded
    // baseline used if Wrath is unavailable; job-specific evaluators may
    // replace them as the embedded provider advances its simulated state.
    private static readonly Dictionary<byte, RotationAnchor> Jobs = new()
    {
        [19] = new(19, "PLD", 9, 7381, [9, 15, 3539], [7381, 16457]),
        [20] = new(20, "MNK", 53, 62, [53, 54, 56], [62, 16473, 70]),
        [21] = new(21, "WAR", 31, 41, [31, 37, 42], [41, 16462]),
        [22] = new(22, "DRG", 75, 86, [75, 78, 84], [86, 7397, 16477]),
        [23] = new(23, "BRD", 97, 106, [97], [106]),
        [24] = new(24, "WHM", 119, 139, [119], [139]),
        [25] = new(25, "BLM", 142, 25793, [142, 141, 147], [25793, 25794]),
        [27] = new(27, "SMN", 163, 16511, [163], [16511]),
        [28] = new(28, "SCH", 17869, 16539, [17869], [16539]),
        [30] = new(30, "NIN", 2240, 2254, [2240, 2242, 2255], [2254, 16488]),
        [31] = new(31, "MCH", 2866, 2870, [2866, 2868, 2873], [2870]),
        [32] = new(32, "DRK", 3617, 3621, [3617, 3623, 3632], [3621, 16468]),
        [33] = new(33, "AST", 3596, 3615, [3596], [3615]),
        [34] = new(34, "SAM", 7477, 7483, [7477, 7478, 7481], [7483, 7484]),
        [35] = new(35, "RDM", 7503, 7509, [7503], [7509]),
        [36] = new(36, "BLU", 18308, 18298, [18308], [18298]),
        [37] = new(37, "GNB", 16137, 16141, [16137, 16139, 16145], [16141, 16149]),
        [38] = new(38, "DNC", 15989, 15993, [15989, 15991], [15993, 15995]),
        [39] = new(39, "RPR", 24373, 24376, [24373, 24374, 24375], [24376, 24377]),
        [40] = new(40, "SGE", 24283, 24297, [24283], [24297]),
        [41] = new(41, "VPR", 34606, 34614, [34606, 34607, 34608], [34614, 34615, 34616]),
        [42] = new(42, "PCT", 34650, 34656, [34650, 34651, 34652], [34656, 34657, 34658]),
    };

    private static readonly Dictionary<byte, byte> Classes = new()
    {
        [1] = 19, [2] = 20, [3] = 21, [4] = 22, [5] = 23,
        [6] = 24, [7] = 25, [26] = 27, [29] = 30,
    };

    internal static ReadOnlyDictionary<byte, RotationAnchor> All { get; } = new(Jobs);

    internal static bool TryGet(byte classJobId, out RotationAnchor anchor)
    {
        if (Classes.TryGetValue(classJobId, out var upgraded))
            classJobId = upgraded;
        return Jobs.TryGetValue(classJobId, out anchor);
    }
}
