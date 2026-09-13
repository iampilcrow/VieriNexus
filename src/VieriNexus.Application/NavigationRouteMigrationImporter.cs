using System.Text.Json;

namespace VieriNexus.Application;

public sealed class NavigationRouteMigrationImporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public NavigationMigrationPreview Preview(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Failed("The VieriNavPlotter configuration is empty.");

        SourceConfiguration? source;
        try
        {
            source = JsonSerializer.Deserialize<SourceConfiguration>(json, Options);
        }
        catch (JsonException)
        {
            return Failed("The VieriNavPlotter configuration is not valid JSON.");
        }

        if (source is null)
            return Failed("The VieriNavPlotter configuration could not be read.");

        List<MigrationIssue> issues = [];
        List<NavigationRouteSnapshot> routes = [];
        HashSet<Guid> ids = [];
        foreach (SourceRoute route in source.Routes ?? [])
        {
            if (route.Id == Guid.Empty)
                issues.Add(new(MigrationIssueSeverity.Error, "A route has an empty stable ID."));
            else if (!ids.Add(route.Id))
                issues.Add(new(MigrationIssueSeverity.Error, $"The route ID {route.Id} is duplicated."));

            if (string.IsNullOrWhiteSpace(route.Name))
                issues.Add(new(MigrationIssueSeverity.Warning, $"Route {route.Id} has no name; the empty name will be preserved."));
            if (route.TerritoryId == 0)
                issues.Add(new(MigrationIssueSeverity.Warning, $"Route '{route.Name}' has no territory yet; it will remain an editable draft."));
            if (route.Points is null || route.Points.Count == 0)
                issues.Add(new(MigrationIssueSeverity.Warning, $"Route '{route.Name}' has no points yet; it will remain an editable draft."));
            if (route.Points is { Count: > 10_000 })
                issues.Add(new(MigrationIssueSeverity.Error, $"Route '{route.Name}' exceeds the 10,000-point safety limit."));
            if (!float.IsFinite(route.Tolerance) || !float.IsFinite(route.LastPointTolerance))
                issues.Add(new(MigrationIssueSeverity.Error, $"Route '{route.Name}' contains a non-finite arrival tolerance."));

            List<NavigationRoutePoint> points = [];
            foreach (SourcePoint point in route.Points ?? [])
            {
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
                    issues.Add(new(MigrationIssueSeverity.Error, $"Route '{route.Name}' contains a non-finite coordinate."));
                points.Add(new(point.X, point.Y, point.Z));
            }

            routes.Add(new(
                route.Id,
                route.Name ?? string.Empty,
                route.TerritoryId,
                points,
                route.Notes ?? string.Empty,
                route.Tags ?? string.Empty,
                route.UseMesh,
                route.UseFlight,
                route.Tolerance,
                route.LastPointTolerance,
                route.BindingKind,
                route.TargetDataId,
                route.TargetLabel ?? string.Empty,
                route.OverrideEnabled,
                route.UpdatedAtUtc));
        }

        if (source.SelectedRouteId is { } selected && routes.All(route => route.Id != selected))
            issues.Add(new(MigrationIssueSeverity.Warning, "The selected route no longer exists; the selection value will still be preserved for rollback fidelity."));

        if (routes.Count == 0)
            issues.Add(new(MigrationIssueSeverity.Information, "No personal routes exist yet; display and recording settings can still be imported."));

        NavigationLibrarySnapshot snapshot = new(
            1,
            source.Version,
            source.RecordingIntervalSeconds,
            source.MinimumPointDistance,
            source.ShowWorldPreview,
            source.ShowPointNumbers,
            source.ShowLiveNavigationPath,
            source.LibraryPaneWidth,
            source.SelectedRouteId,
            routes);
        return new(snapshot, issues);
    }

    private static NavigationMigrationPreview Failed(string message) =>
        new(null, [new(MigrationIssueSeverity.Error, message)]);

    private sealed class SourceConfiguration
    {
        public int Version { get; set; } = 1;
        public float RecordingIntervalSeconds { get; set; } = 1f;
        public float MinimumPointDistance { get; set; } = .75f;
        public bool ShowWorldPreview { get; set; } = true;
        public bool ShowPointNumbers { get; set; } = true;
        public bool ShowLiveNavigationPath { get; set; } = true;
        public float LibraryPaneWidth { get; set; } = 355f;
        public List<SourceRoute>? Routes { get; set; } = [];
        public Guid? SelectedRouteId { get; set; }
    }

    private sealed class SourceRoute
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public uint TerritoryId { get; set; }
        public List<SourcePoint>? Points { get; set; } = [];
        public string? Notes { get; set; }
        public string? Tags { get; set; }
        public bool UseMesh { get; set; }
        public bool UseFlight { get; set; }
        public float Tolerance { get; set; } = .75f;
        public float LastPointTolerance { get; set; } = 3f;
        public int BindingKind { get; set; }
        public uint TargetDataId { get; set; }
        public string? TargetLabel { get; set; }
        public bool OverrideEnabled { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    private sealed class SourcePoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}
