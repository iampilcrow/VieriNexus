using Avarice.Structs;

namespace Avarice.Positional;

internal static class PositionalVerdict
{
    internal static PositionalState Merge(PositionalState current, bool? verifiedHit) =>
        current == PositionalState.Success || verifiedHit == true ? PositionalState.Success :
        verifiedHit == false ? PositionalState.Failure : current;
}
