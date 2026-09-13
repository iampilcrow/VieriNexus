using System.Text.Json;

namespace VieriNexus.Application;

public sealed record NavigationRouteClipboardData(
    int? SchemaVersion,
    Guid? Id,
    string? Name,
    uint TerritoryId,
    IReadOnlyList<NavigationRoutePoint>? Points,
    string? Notes,
    string? Tags,
    bool UseMesh,
    bool UseFlight,
    float Tolerance,
    float LastPointTolerance,
    int BindingKind,
    uint TargetDataId,
    string? TargetLabel,
    bool OverrideEnabled,
    DateTime? UpdatedAtUtc);

public sealed record NavigationRouteClipboardReadResult(
    bool Success,
    string Message,
    NavigationRouteSnapshot? Route = null);

/// <summary>
/// Versioned Nexus route exchange that also accepts the compatible legacy
/// VieriNavPlotter route JSON shape. Imported identities are always regenerated and
/// automation overrides are always disabled pending review.
/// </summary>
public static class NavigationRouteClipboardCodec
{
    private const int MaximumPointCount = 10_000;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string Write(NavigationRouteSnapshot route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var data = new NavigationRouteClipboardData(
            1, route.Id, route.Name, route.TerritoryId, route.Points.ToArray(),
            route.Notes, route.Tags, route.UseMesh, route.UseFlight, route.Tolerance,
            route.LastPointTolerance, route.BindingKind, route.TargetDataId,
            route.TargetLabel, route.OverrideEnabled, route.UpdatedAtUtc);
        return JsonSerializer.Serialize(data, Options);
    }

    public static NavigationRouteClipboardReadResult Read(string? json, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Failure("The clipboard is empty.");

        NavigationRouteClipboardData? data;
        try
        {
            data = JsonSerializer.Deserialize<NavigationRouteClipboardData>(json, Options);
        }
        catch (JsonException)
        {
            return Failure("The clipboard does not contain readable route JSON.");
        }

        if (data is null)
            return Failure("The clipboard route is empty.");
        if (data.SchemaVersion is < 0 or > 1)
            return Failure($"Clipboard route schema {data.SchemaVersion} is not supported.");
        if (data.TerritoryId == 0)
            return Failure("The clipboard route has no territory.");
        if (data.Points is null)
            return Failure("The clipboard route has no point collection.");
        if (data.Points.Count > MaximumPointCount)
            return Failure($"The clipboard route exceeds the {MaximumPointCount:N0}-point safety limit.");
        if (data.Points.Any(point =>
                !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z)))
            return Failure("The clipboard route contains an invalid coordinate.");
        if (!float.IsFinite(data.Tolerance) || !float.IsFinite(data.LastPointTolerance))
            return Failure("The clipboard route contains an invalid tolerance.");
        if (data.BindingKind is < 0 or > 1)
            return Failure("The clipboard route contains an unsupported assignment type.");

        string name = SafeText(data.Name, "Imported route", 120);
        var route = new NavigationRouteSnapshot(
            Guid.NewGuid(),
            name,
            data.TerritoryId,
            data.Points.ToArray(),
            SafeText(data.Notes, string.Empty, 4_000),
            SafeText(data.Tags, string.Empty, 500),
            data.UseMesh,
            data.UseFlight,
            Math.Clamp(data.Tolerance, 0.1f, 20f),
            Math.Clamp(data.LastPointTolerance, 0.1f, 30f),
            data.BindingKind,
            data.TargetDataId,
            SafeText(data.TargetLabel, string.Empty, 200),
            OverrideEnabled: false,
            UpdatedAtUtc: DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc));
        return new(true,
            $"Imported {route.Points.Count} point(s). Automation assignment remains disabled until reviewed.",
            route);
    }

    private static NavigationRouteClipboardReadResult Failure(string message) => new(false, message);

    private static string SafeText(string? value, string fallback, int maximumLength)
    {
        string result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return result.Length <= maximumLength ? result : result[..maximumLength];
    }
}
