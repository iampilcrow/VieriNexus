namespace VieriLink;

internal static class LevelNotificationPolicy
{
    internal static bool IsGenuineIncrease(AutoDutyStatus previous, AutoDutyStatus current) =>
        previous.Available &&
        current.Available &&
        previous.ContentId != 0 &&
        previous.ContentId == current.ContentId &&
        string.Equals(previous.Job, current.Job, StringComparison.Ordinal) &&
        current.Level > previous.Level;
}
