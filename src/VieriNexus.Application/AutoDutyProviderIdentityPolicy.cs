namespace VieriNexus.Application;

public enum AutoDutyProviderIdentity
{
    None,
    VieriCompatibility,
    Stock,
    Conflict,
}

public sealed record AutoDutyProviderInstance(
    bool IsLoaded,
    string? DisplayName,
    string? Version);

public sealed record AutoDutyProviderIdentityAssessment(
    AutoDutyProviderIdentity Identity,
    int InstalledCount,
    int LoadedCount,
    string Detail);

/// <summary>
/// Resolves the two packages that intentionally share Dalamud's AutoDuty internal name.
/// More than one loaded instance is always a conflict, even when the colliding IPC registry
/// happens to expose enough functions to look healthy.
/// </summary>
public static class AutoDutyProviderIdentityPolicy
{
    public static AutoDutyProviderIdentityAssessment Assess(
        IEnumerable<AutoDutyProviderInstance> instances,
        bool hasVieriCompatibilityMarker)
    {
        ArgumentNullException.ThrowIfNull(instances);
        AutoDutyProviderInstance[] installed = instances.ToArray();
        AutoDutyProviderInstance[] loaded = installed.Where(instance => instance.IsLoaded).ToArray();
        if (loaded.Length > 1)
            return new(
                AutoDutyProviderIdentity.Conflict,
                installed.Length,
                loaded.Length,
                "More than one loaded plugin claims the AutoDuty identity. Disable one and reload before Nexus can delegate a duty.");
        if (loaded.Length == 0)
            return new(
                AutoDutyProviderIdentity.None,
                installed.Length,
                0,
                installed.Length == 0
                    ? "No AutoDuty provider is installed."
                    : "An AutoDuty provider is installed but disabled.");

        bool isVieri = hasVieriCompatibilityMarker || string.Equals(
            loaded[0].DisplayName,
            "VieriAutoDuty",
            StringComparison.OrdinalIgnoreCase);
        return new(
            isVieri ? AutoDutyProviderIdentity.VieriCompatibility : AutoDutyProviderIdentity.Stock,
            installed.Length,
            1,
            isVieri
                ? "The loaded provider is the VieriAutoDuty migration source."
                : "The loaded provider is stock AutoDuty.");
    }
}
