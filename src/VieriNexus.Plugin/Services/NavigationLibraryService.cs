using System.Numerics;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed record NavigationLibraryStatus(
    bool HasWorkingLibrary,
    NavigationLibrarySnapshot? Snapshot,
    string Code,
    string Message);

internal sealed class NavigationLibraryService
{
    private readonly NavigationMigrationService migration;
    private readonly INavigationLibraryStore store;
    private NavigationLibrarySnapshot? working;
    private string code = "working-library-not-created";
    private string message = "Create a Nexus working copy from verified staging before editing routes.";

    internal NavigationLibraryService(
        NavigationMigrationService migration,
        string pluginConfigDirectory)
    {
        this.migration = migration;
        store = new FileNavigationLibraryStore(Path.Combine(
            pluginConfigDirectory, "NexusData", "navigation-library.v1.json"));
        try
        {
            working = store.Load();
            if (working is not null)
            {
                code = "working-library-loaded";
                message = $"Loaded the Nexus working library with {working.Routes.Count} personal route(s).";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            code = "working-library-invalid";
            message = $"The Nexus working library could not be verified: {ex.Message}";
        }
    }

    internal NavigationLibrarySnapshot? Current => Volatile.Read(ref working) ?? migration.StagedSnapshot;

    internal bool HasWorkingLibrary => Volatile.Read(ref working) is not null;

    internal NavigationLibraryStatus Status => new(HasWorkingLibrary, Current, code, message);

    internal NavigationLibraryWriteResult CreateWorkingCopy()
    {
        NavigationLibrarySnapshot? staged = migration.StagedSnapshot;
        if (staged is null)
            return Result(false, "working-library-staging-required",
                "Import and verify VieriNavPlotter staging before creating the Nexus working library.");
        return Save(staged with { Routes = staged.Routes.ToArray() },
            "working-library-created",
            $"Created a separate Nexus working library with {staged.Routes.Count} personal route(s). Migration staging and its rollback receipt remain unchanged.");
    }

    internal NavigationLibraryWriteResult CreateRoute(uint territoryId, Vector3 position)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        if (current is null)
            return Result(false, "working-library-required", "Create the Nexus working library first.");
        if (territoryId == 0 || !Finite(position))
            return Result(false, "route-current-position-unavailable", "A valid current territory and position are required.");

        var route = new NavigationRouteSnapshot(
            Guid.NewGuid(),
            UniqueName(current, "New route"),
            territoryId,
            [new(position.X, position.Y, position.Z)],
            string.Empty,
            string.Empty,
            UseMesh: true,
            UseFlight: false,
            Tolerance: 0.75f,
            LastPointTolerance: 3f,
            BindingKind: 0,
            TargetDataId: 0,
            TargetLabel: string.Empty,
            OverrideEnabled: false,
            UpdatedAtUtc: DateTime.UtcNow);
        NavigationLibrarySnapshot updated = current with
        {
            Routes = current.Routes.Append(route).ToArray(),
            SelectedRouteId = route.Id,
        };
        return Save(updated, "route-created",
            $"Created {route.Name} with the current position as point 1.");
    }

    internal NavigationLibraryWriteResult AddCurrentPoint(Guid routeId, uint territoryId, Vector3 position)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteSnapshot? route = current?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (current is null || route is null)
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        if (territoryId != route.TerritoryId)
            return Result(false, "route-territory-mismatch",
                $"This route belongs to territory {route.TerritoryId}; the current territory is {territoryId}.");
        if (!Finite(position))
            return Result(false, "route-current-position-unavailable", "The current position is not valid.");

        return AddPoint(routeId, new NavigationRoutePoint(position.X, position.Y, position.Z));
    }

    internal NavigationLibraryWriteResult AddPoint(Guid routeId, NavigationRoutePoint point)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteSnapshot? route = current?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (current is null || route is null)
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        if (!Finite(point))
            return Result(false, "route-point-invalid", "The route point is not valid.");
        NavigationRouteSnapshot updatedRoute = route with
        {
            Points = route.Points.Append(point).ToArray(),
            UpdatedAtUtc = DateTime.UtcNow,
        };
        return Replace(current, updatedRoute, "route-point-added",
            $"Added point {updatedRoute.Points.Count} to {route.Name}.");
    }

    internal NavigationLibraryWriteResult RemoveLastPoint(Guid routeId)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteSnapshot? route = current?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (current is null || route is null)
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        if (route.Points.Count == 0)
            return Result(false, "route-empty", "This route has no points to remove.");
        NavigationRouteSnapshot updated = route with
        {
            Points = route.Points.Take(route.Points.Count - 1).ToArray(),
            OverrideEnabled = false,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        return Replace(current, updated, "route-point-removed", $"Removed the last point from {route.Name}.");
    }

    internal NavigationLibraryWriteResult Reverse(Guid routeId)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteSnapshot? route = current?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (current is null || route is null)
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        if (route.Points.Count < 2)
            return Result(false, "route-reverse-insufficient-points", "At least two points are required to reverse a route.");
        NavigationRouteSnapshot updated = route with
        {
            Points = route.Points.Reverse().ToArray(),
            UpdatedAtUtc = DateTime.UtcNow,
        };
        return Replace(current, updated, "route-reversed", $"Reversed {route.Name}.");
    }

    internal NavigationLibraryWriteResult ReplacePoint(
        Guid routeId,
        int pointIndex,
        uint territoryId,
        Vector3 position)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteSnapshot? route = current?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (current is null || route is null)
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        if (pointIndex < 0 || pointIndex >= route.Points.Count)
            return Result(false, "route-point-not-found", "The selected route point no longer exists.");
        if (territoryId != route.TerritoryId)
            return Result(false, "route-territory-mismatch",
                $"This route belongs to territory {route.TerritoryId}; the current territory is {territoryId}.");
        if (!Finite(position))
            return Result(false, "route-current-position-unavailable", "The current position is not valid.");

        NavigationRoutePoint[] points = route.Points.ToArray();
        points[pointIndex] = new(position.X, position.Y, position.Z);
        return Replace(current, route with { Points = points, UpdatedAtUtc = DateTime.UtcNow },
            "route-point-replaced", $"Replaced point {pointIndex + 1} in {route.Name}.");
    }

    internal NavigationLibraryWriteResult MovePoint(Guid routeId, int pointIndex, int offset)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteSnapshot? route = current?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (current is null || route is null)
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        int destination = pointIndex + offset;
        if (pointIndex < 0 || pointIndex >= route.Points.Count || destination < 0 || destination >= route.Points.Count)
            return Result(false, "route-point-move-invalid", "The selected point cannot move in that direction.");

        NavigationRoutePoint[] points = route.Points.ToArray();
        (points[pointIndex], points[destination]) = (points[destination], points[pointIndex]);
        return Replace(current, route with { Points = points, UpdatedAtUtc = DateTime.UtcNow },
            "route-point-moved", $"Moved point {pointIndex + 1} to position {destination + 1}.");
    }

    internal NavigationLibraryWriteResult RemovePoint(Guid routeId, int pointIndex)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteSnapshot? route = current?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (current is null || route is null)
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        if (pointIndex < 0 || pointIndex >= route.Points.Count)
            return Result(false, "route-point-not-found", "The selected route point no longer exists.");

        NavigationRoutePoint[] points = route.Points.Where((_, index) => index != pointIndex).ToArray();
        NavigationRouteSnapshot updated = route with
        {
            Points = points,
            OverrideEnabled = points.Length >= 2 && route.OverrideEnabled,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        return Replace(current, updated, "route-point-removed",
            $"Removed point {pointIndex + 1} from {route.Name}.");
    }

    internal NavigationLibraryWriteResult ClearPoints(Guid routeId)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteSnapshot? route = current?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (current is null || route is null)
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        if (route.Points.Count == 0)
            return Result(false, "route-empty", "This route already has no points.");
        return Replace(current, route with
        {
            Points = [],
            OverrideEnabled = false,
            UpdatedAtUtc = DateTime.UtcNow,
        }, "route-points-cleared", $"Cleared all points from {route.Name}.");
    }

    internal NavigationLibraryWriteResult DuplicateRoute(Guid routeId)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteSnapshot? route = current?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (current is null || route is null)
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        NavigationRouteSnapshot copy = route with
        {
            Id = Guid.NewGuid(),
            Name = UniqueName(current, $"{route.Name} copy"),
            Points = route.Points.ToArray(),
            BindingKind = 0,
            TargetDataId = 0,
            TargetLabel = string.Empty,
            OverrideEnabled = false,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        return Save(current with
        {
            Routes = current.Routes.Append(copy).ToArray(),
            SelectedRouteId = copy.Id,
        }, "route-duplicated", $"Created {copy.Name} with automation assignment disabled.");
    }

    internal NavigationLibraryWriteResult AddBuiltInTemplate(NavigationRouteSnapshot template)
    {
        ArgumentNullException.ThrowIfNull(template);
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        if (current is null)
            return Result(false, "working-library-required", "Create the Nexus working library first.");
        if (template.BindingKind != 1 || template.TargetDataId == 0 || template.Points.Count == 0)
            return Result(false, "built-in-template-invalid", "The selected built-in vendor template is not valid.");

        NavigationRouteSnapshot copy = template with
        {
            Id = Guid.NewGuid(),
            Name = UniqueName(current, template.Name),
            Points = template.Points.ToArray(),
            OverrideEnabled = false,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        return Save(current with
        {
            Routes = current.Routes.Append(copy).ToArray(),
            SelectedRouteId = copy.Id,
        }, "built-in-template-added",
            $"Added {copy.Name} to the personal library. Its gear-vendor override remains disabled until explicitly approved.");
    }

    internal NavigationLibraryWriteResult SetOverride(Guid routeId, bool enabled)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteOverrideUpdate update = NavigationRouteOverrideResolver.SetExclusive(
            current, routeId, enabled, DateTime.UtcNow);
        return !update.Success || update.Library is null
            ? Result(false, update.Code, update.Message)
            : Save(update.Library, update.Code, update.Message);
    }

    internal string ExportRoute(Guid routeId)
    {
        NavigationRouteSnapshot? route = Volatile.Read(ref working)?.Routes.FirstOrDefault(item => item.Id == routeId);
        return route is null ? string.Empty : NavigationRouteClipboardCodec.Write(route);
    }

    internal NavigationLibraryWriteResult ImportRoute(string? json)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        if (current is null)
            return Result(false, "working-library-required", "Create the Nexus working library first.");
        NavigationRouteClipboardReadResult read = NavigationRouteClipboardCodec.Read(json, DateTime.UtcNow);
        if (!read.Success || read.Route is null)
            return Result(false, "route-import-invalid", read.Message);
        NavigationRouteSnapshot route = read.Route with { Name = UniqueName(current, read.Route.Name) };
        return Save(current with
        {
            Routes = current.Routes.Append(route).ToArray(),
            SelectedRouteId = route.Id,
        }, "route-imported", $"Imported {route.Name} with {route.Points.Count} point(s). Automation assignment remains disabled until reviewed.");
    }

    internal NavigationLibraryWriteResult UpdatePreferences(
        float recordingIntervalSeconds,
        float minimumPointDistance,
        bool showWorldPreview,
        bool showPointNumbers,
        bool showLiveNavigationPath)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        if (current is null)
            return Result(false, "working-library-required", "Create the Nexus working library first.");
        if (!float.IsFinite(recordingIntervalSeconds) || !float.IsFinite(minimumPointDistance))
            return Result(false, "route-preferences-invalid", "Recording preferences must be finite numbers.");
        return Save(current with
        {
            RecordingIntervalSeconds = Math.Clamp(recordingIntervalSeconds, 0.2f, 5f),
            MinimumPointDistance = Math.Clamp(minimumPointDistance, 0.1f, 10f),
            ShowWorldPreview = showWorldPreview,
            ShowPointNumbers = showPointNumbers,
            ShowLiveNavigationPath = showLiveNavigationPath,
        }, "route-preferences-updated", "Saved Nexus route recording and display preferences.");
    }

    internal NavigationLibraryWriteResult SelectRoute(Guid routeId)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        if (current is null || current.Routes.All(route => route.Id != routeId))
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        if (current.SelectedRouteId == routeId)
            return Result(true, "route-selected", "Route selected.", current);
        return Save(current with { SelectedRouteId = routeId }, "route-selected", "Route selection saved.");
    }

    internal NavigationLibraryWriteResult UpdateRoute(NavigationRouteSnapshot route)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        if (current is null || current.Routes.All(item => item.Id != route.Id))
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        NavigationRouteSnapshot safe = route with
        {
            Name = string.IsNullOrWhiteSpace(route.Name) ? "Unnamed route" : route.Name.Trim(),
            Tolerance = Math.Clamp(route.Tolerance, 0.1f, 20f),
            LastPointTolerance = Math.Clamp(route.LastPointTolerance, 0.1f, 30f),
            UpdatedAtUtc = DateTime.UtcNow,
        };
        return Replace(current, safe, "route-updated", $"Saved {safe.Name}.");
    }

    internal NavigationLibraryWriteResult DeleteRoute(Guid routeId)
    {
        NavigationLibrarySnapshot? current = Volatile.Read(ref working);
        NavigationRouteSnapshot? route = current?.Routes.FirstOrDefault(item => item.Id == routeId);
        if (current is null || route is null)
            return Result(false, "route-not-found", "The selected Nexus route no longer exists.");
        NavigationRouteSnapshot[] routes = current.Routes.Where(item => item.Id != routeId).ToArray();
        return Save(current with
        {
            Routes = routes,
            SelectedRouteId = routes.FirstOrDefault()?.Id,
        }, "route-deleted", $"Deleted {route.Name} from the Nexus working library.");
    }

    private NavigationLibraryWriteResult Replace(
        NavigationLibrarySnapshot current,
        NavigationRouteSnapshot route,
        string resultCode,
        string resultMessage) => Save(current with
        {
            Routes = current.Routes.Select(item => item.Id == route.Id ? route : item).ToArray(),
            SelectedRouteId = route.Id,
        }, resultCode, resultMessage);

    private NavigationLibraryWriteResult Save(
        NavigationLibrarySnapshot snapshot,
        string successCode,
        string successMessage)
    {
        try
        {
            store.Save(snapshot);
            Volatile.Write(ref working, snapshot);
            return Result(true, successCode, successMessage, snapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            return Result(false, "working-library-write-failed",
                $"The Nexus working library was not changed: {ex.Message}");
        }
    }

    private NavigationLibraryWriteResult Result(
        bool success,
        string resultCode,
        string resultMessage,
        NavigationLibrarySnapshot? snapshot = null)
    {
        code = resultCode;
        message = resultMessage;
        return new(success, resultMessage, snapshot);
    }

    private static bool Finite(Vector3 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);

    private static bool Finite(NavigationRoutePoint point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);

    private static string UniqueName(NavigationLibrarySnapshot snapshot, string basis)
    {
        if (snapshot.Routes.All(route => !string.Equals(route.Name, basis, StringComparison.OrdinalIgnoreCase)))
            return basis;
        int suffix = 2;
        while (snapshot.Routes.Any(route => string.Equals(route.Name, $"{basis} {suffix}", StringComparison.OrdinalIgnoreCase)))
            suffix++;
        return $"{basis} {suffix}";
    }
}
