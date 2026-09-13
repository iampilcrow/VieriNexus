namespace VieriLink;

internal static class DurabilityNotificationPolicy
{
    internal static bool IsGenuineDrop(AutoDutyStatus previous, AutoDutyStatus current, float alertPercent) =>
        previous.Available &&
        current.Available &&
        previous.ContentId != 0 &&
        previous.ContentId == current.ContentId &&
        previous.DurabilityPercent > alertPercent &&
        current.DurabilityPercent <= alertPercent;
}
