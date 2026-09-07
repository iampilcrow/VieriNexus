namespace VieriNexus.Services;

internal sealed class GameplayReadyGate(long settleMilliseconds = 900)
{
    private long eligibleSince = -1;

    internal bool Evaluate(bool loggedIn, bool playerReady, bool hasTerritory, bool betweenAreas, long now)
    {
        if (!loggedIn || !playerReady || !hasTerritory || betweenAreas)
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
