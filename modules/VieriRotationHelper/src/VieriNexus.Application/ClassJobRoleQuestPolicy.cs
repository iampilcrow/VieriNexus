namespace VieriNexus.Application;

/// <summary>
/// Nexus-owned selection boundary for class, job, and role quest families. Chapter IDs are stable
/// game-data keys used to discover the actual quest rows at runtime; Questionable remains solely
/// the executor for the one exact quest Nexus selects.
/// </summary>
public static class ClassJobRoleQuestPolicy
{
    public static IReadOnlyList<uint> Chapters(uint classJobId)
    {
        uint[] classJob = classJobId switch
        {
            1 => [63],
            2 => [67],
            3 => [64],
            4 => [68],
            5 => [70],
            6 => [65],
            7 => [71],
            8 => [30, 31, 32],
            9 => [33, 34, 35],
            10 => [36, 37, 38],
            11 => [39, 40, 41],
            12 => [42, 43, 44],
            13 => [45, 46, 47],
            14 => [48, 49, 50],
            15 => [51, 52, 53],
            16 => [54, 55, 56],
            17 => [57, 58, 59],
            18 => [60, 61, 62],
            19 => [72, 73, 74],
            20 => [98, 99, 100],
            21 => [76, 77, 78],
            22 => [102, 103, 104],
            23 => [113, 114, 115],
            24 => [86, 87, 88],
            25 => [123, 124, 125],
            26 => [66],
            27 => [127, 128, 129],
            28 => [90, 91, 92],
            29 => [69],
            30 => [106, 107, 108],
            31 => [117, 118, 119],
            32 => [80, 81, 82],
            33 => [94, 95, 96],
            34 => [110, 111],
            35 => [131, 132],
            36 => [134, 135, 146, 170],
            37 => [84],
            38 => [121],
            39 => [153],
            40 => [152],
            41 => [176],
            42 => [177],
            43 => [206],
            _ => [],
        };
        uint[] role = classJobId switch
        {
            19 or 21 or 32 or 37 => [136, 154, 178],
            24 or 28 or 33 or 40 => [137, 155, 179],
            20 or 22 or 30 or 34 or 39 or 41 => [138, 156, 180],
            23 or 31 or 38 => [138, 157, 181],
            25 or 27 or 35 or 42 => [139, 158, 182],
            _ => [],
        };
        return classJob.Concat(role).Distinct().ToArray();
    }
}
