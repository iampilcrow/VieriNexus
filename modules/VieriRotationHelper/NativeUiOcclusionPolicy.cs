using System.Numerics;

namespace VieriRotationHelper;

internal static class NativeUiOcclusionPolicy
{
    internal static bool Overlaps(Vector2 position, Vector2 size, Vector2 nativeMinimum, Vector2 nativeMaximum)
    {
        if (size.X <= 0 || size.Y <= 0 || nativeMaximum.X <= nativeMinimum.X || nativeMaximum.Y <= nativeMinimum.Y)
            return false;

        var maximum = position + size;
        return position.X < nativeMaximum.X && maximum.X > nativeMinimum.X &&
               position.Y < nativeMaximum.Y && maximum.Y > nativeMinimum.Y;
    }
}
