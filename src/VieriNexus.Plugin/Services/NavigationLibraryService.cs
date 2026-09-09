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

        NavigationRouteSnapshot updatedRoute = route with
        {
            Points = route.Points.Append(new NavigationRoutePoint(position.X, position.Y, position.Z)).ToArray(),
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
