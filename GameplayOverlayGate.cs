namespace VieriRotationHelper;

internal sealed class GameplayOverlayGate(long settleMilliseconds = 750)
{
    private long eligibleSince = -1;

    internal bool Evaluate(
        bool isLoggedIn,
        bool hasAvailablePlayer,
        bool hasTerritory,
        bool isBetweenAreas,
        long now)
    {
        if (!isLoggedIn || !hasAvailablePlayer || !hasTerritory || isBetweenAreas)
        {
            eligibleSince = -1;
            return false;
        }

        if (eligibleSince < 0)
        {
            eligibleSince = now;
            return settleMilliseconds <= 0;
        }

        return now - eligibleSince >= settleMilliseconds;
    }
}
