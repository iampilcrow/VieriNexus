namespace VieriNexus.Application;

/// <summary>
/// Resolves a Dalamud package for a Nexus dependency. AutoDuty and VieriAutoDuty
/// intentionally share an internal name, so that family must also match the
/// player-facing package name before Nexus may install, enable, or report it ready.
/// </summary>
public static class DependencyPackageIdentityPolicy
{
    public static bool MatchesInstalled(
        DependencyDescriptor definition,
        string? internalName,
        string? displayName)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (IsStockAutoDuty(definition))
            return string.Equals(displayName, "AutoDuty", StringComparison.OrdinalIgnoreCase);

        return definition.InternalNames.Any(name =>
            string.Equals(internalName, name, StringComparison.OrdinalIgnoreCase));
    }

    public static bool MatchesAvailable(
        DependencyDescriptor definition,
        string? internalName,
        string? displayName)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (IsStockAutoDuty(definition))
            return string.Equals(displayName, "AutoDuty", StringComparison.OrdinalIgnoreCase);

        return MatchesInstalled(definition, internalName, displayName) ||
               string.Equals(displayName, definition.DisplayName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(displayName, definition.InstallerSearch, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStockAutoDuty(DependencyDescriptor definition) =>
        string.Equals(definition.Id, "autoduty", StringComparison.OrdinalIgnoreCase);
}
