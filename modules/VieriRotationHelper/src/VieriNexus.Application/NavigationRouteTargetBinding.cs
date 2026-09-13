namespace VieriNexus.Application;

public sealed record NavigationRouteBindingUpdate(
    bool Success,
    string Code,
    string Message,
    NavigationRouteSnapshot? Route = null);

/// <summary>
/// Applies route binding changes without activating an automation override. Target capture is
/// intentionally separate from approval so observing a target can never make a route executable
/// by an automatic consumer.
/// </summary>
public static class NavigationRouteTargetBinding
{
    public static NavigationRouteBindingUpdate SetKind(
        NavigationRouteSnapshot route,
        int bindingKind,
        DateTime updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (bindingKind is < 0 or > 1)
            return Failed("route-binding-kind-invalid", "The selected route binding is not supported.");

        NavigationRouteSnapshot updated = bindingKind == 0
            ? route with
            {
                BindingKind = 0,
                TargetDataId = 0,
                TargetLabel = string.Empty,
                OverrideEnabled = false,
                UpdatedAtUtc = updatedAtUtc,
            }
            : route with
            {
                BindingKind = 1,
                OverrideEnabled = false,
                UpdatedAtUtc = updatedAtUtc,
            };
        return new(true, "route-binding-kind-updated",
            bindingKind == 0
                ? $"Removed the automation binding from {route.Name}."
                : $"Set {route.Name} to use a gear-vendor target. Bind the current target, then approve the override explicitly.",
            updated);
    }

    public static NavigationRouteBindingUpdate BindCurrentTarget(
        NavigationRouteSnapshot route,
        uint currentTerritoryId,
        uint targetDataId,
        string? targetLabel,
        DateTime updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (route.BindingKind != 1)
            return Failed("route-binding-kind-required", "Choose Gear vendor before binding a target.");
        if (currentTerritoryId == 0 || targetDataId == 0)
            return Failed("route-target-invalid", "Target a valid vendor or object first.");
        if (route.Points.Count > 0 && route.TerritoryId != currentTerritoryId)
            return Failed("route-target-territory-mismatch",
                $"This route belongs to territory {route.TerritoryId}; bind its target while in that territory.");

        string label = string.IsNullOrWhiteSpace(targetLabel)
            ? $"Target {targetDataId}"
            : targetLabel.Trim();
        NavigationRouteSnapshot updated = route with
        {
            TerritoryId = currentTerritoryId,
            TargetDataId = targetDataId,
            TargetLabel = label,
            OverrideEnabled = false,
            UpdatedAtUtc = updatedAtUtc,
        };
        return new(true, "route-target-bound",
            $"Bound {route.Name} to {label}. The gear-vendor override remains disabled until explicitly approved.",
            updated);
    }

    private static NavigationRouteBindingUpdate Failed(string code, string message) =>
        new(false, code, message);
}
