using System.Numerics;

namespace VieriRotationHelper;

// Constants and geometry ported from Hilda 7.5.1 PriorityIcon/DrawUtils.
// Keep independent of the provider: changing presentation must not change actions.
internal static class HildaVisualStyle
{
    internal const string FrameTexture = "ui/uld/IconA_Frame_hr1.tex";
    internal const string RecastTexture = "ui/uld/IconA_Recast_hr1.tex";
    internal const char FlankSymbol = (char)62263;
    internal const char RearSymbol = (char)62217;
    internal const char WeaveSymbol = (char)61549;
    internal const char EnemySymbol = (char)61713;

    internal static Vector2 Position(int index, float main, float secondary, float spacing, bool horizontal)
    {
        var scale = main / 65f;
        var along = 20f * scale + (index == 0 ? 0 : main + spacing * 2 + (index - 1) * (secondary + spacing * 2));
        var cross = 20f * scale + (main - (index == 0 ? main : secondary)) / 2f;
        return horizontal ? new Vector2(along, cross) : new Vector2(cross, along);
    }

    internal static (Vector2 Min, Vector2 Max) RecastUv(float elapsed, float total)
    {
        var cell = Math.Round(Math.Clamp(elapsed / total, 0f, 1f) * 80.0) + 1;
        var row = Math.Ceiling(cell / 9.0) - 1;
        return (new Vector2((float)(.11110000312328339 * (cell - 1 + .01899999938905239 - row * 9)),
                (float)(.11110000312328339 * row) + .002f),
            new Vector2((float)(.11110000312328339 * (cell - row * 9)),
                (float)(.11110000312328339 * (row + 1)) - .016f));
    }
}
