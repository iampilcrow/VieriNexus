using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Numerics;
using VieriNexus.Application;

namespace VieriNexus.Services;

internal sealed record StrikingDummyTravelStatus(
    bool IsActive,
    string Message,
    StrikingDummyDestination? Destination = null);

/// <summary>
/// Nexus-owned striking-dummy travel. Lifestream performs only the unlocked teleport; after the
/// target zone loads, Nexus projects the map coordinate onto vnavmesh and starts a normal owned route.
/// </summary>
internal sealed class StrikingDummyTravelService
{
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IDataManager dataManager;
    private readonly IAetheryteList aetheryteList;
    private readonly NavigationRouteRuntimeService navigation;
    private readonly ICallGateSubscriber<uint, byte, bool> teleport;
    private readonly ICallGateSubscriber<bool> lifestreamBusy;
    private readonly ICallGateSubscriber<object> abortLifestream;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> pointOnFloor;
    private StrikingDummyDestination? destination;
    private uint territoryId;
    private DateTimeOffset startedAt;
    private State state;
    private string message = "No striking-dummy trip is running.";

    internal StrikingDummyTravelService(
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        ICondition condition,
        IDataManager dataManager,
        IAetheryteList aetheryteList,
        NavigationRouteRuntimeService navigation)
    {
        this.clientState = clientState;
        this.condition = condition;
        this.dataManager = dataManager;
        this.aetheryteList = aetheryteList;
        this.navigation = navigation;
        teleport = pluginInterface.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport");
        lifestreamBusy = pluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        abortLifestream = pluginInterface.GetIpcSubscriber<object>("Lifestream.Abort");
        pointOnFloor = pluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Mesh.PointOnFloor");
    }

    internal StrikingDummyTravelStatus Status => new(state != State.Idle, message, destination);

    internal bool Start(StrikingDummyDestination selected, out string result)
    {
        if (state != State.Idle || navigation.Status.IsActive)
        {
            result = "Stop the current Nexus trip before selecting striking dummies.";
            return false;
        }
        if (Plugin.ObjectTable.LocalPlayer is null)
        {
            result = "Log into a character before traveling.";
            return false;
        }
        if (condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ||
            condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty] ||
            condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty56] ||
            condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty95])
        {
            result = "Striking-dummy travel cannot start during combat or a duty.";
            return false;
        }
        if (!pointOnFloor.HasFunction)
        {
            result = "vnavmesh does not expose the floor-projection capability required for this trip.";
            return false;
        }

        uint resolved = ResolveTerritory(selected.Zone);
        if (resolved == 0)
        {
            result = "The selected training area is not present in the installed game data.";
            return false;
        }
        if (selected.RequiresInstance && clientState.TerritoryType != resolved)
        {
            result = "Enter this field-operation instance first, then select its striking dummies.";
            return false;
        }

        destination = selected;
        territoryId = resolved;
        startedAt = DateTimeOffset.UtcNow;
        if (clientState.TerritoryType == territoryId)
        {
            state = State.Projecting;
            message = $"Nexus is locating the walkable training area in {selected.DisplayLocation}.";
            result = message;
            return true;
        }
        if (!teleport.HasFunction || !lifestreamBusy.HasFunction || !abortLifestream.HasAction)
        {
            Reset();
            result = "Cross-zone striking-dummy travel requires a compatible Lifestream installation.";
            return false;
        }
        if (lifestreamBusy.InvokeFunc())
        {
            Reset();
            result = "Lifestream is already busy.";
            return false;
        }
        uint aetheryteId = aetheryteList
            .Select(entry => dataManager.GetExcelSheet<Aetheryte>().GetRowOrDefault(entry.AetheryteId))
            .Where(row => row is { IsAetheryte: true } && row.Value.Territory.RowId == territoryId)
            .Select(row => row!.Value.RowId)
            .FirstOrDefault();
        if (aetheryteId == 0 || !teleport.InvokeFunc(aetheryteId, 0))
        {
            Reset();
            result = "No unlocked direct teleport could be started for this training area.";
            return false;
        }
        state = State.Teleporting;
        message = $"Nexus asked Lifestream to teleport to {selected.DisplayLocation}.";
        result = message;
        return true;
    }

    internal void Update(DateTimeOffset now)
    {
        if (state == State.Idle || destination is null)
            return;
        if (now - startedAt > TimeSpan.FromMinutes(5))
        {
            Stop("Striking-dummy travel timed out.");
            return;
        }
        if (condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] ||
            condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
            return;
        try
        {
            if (state == State.Teleporting)
            {
                if (clientState.TerritoryType == territoryId && !lifestreamBusy.InvokeFunc())
                {
                    state = State.Projecting;
                    message = $"Nexus reached {destination.DisplayLocation} and is locating its walkable training area.";
                }
                return;
            }
            if (state == State.Projecting)
            {
                Vector3? floor;
                if (destination.MapX == 0 && destination.MapY == 0)
                {
                    HashSet<uint> names = dataManager.GetExcelSheet<BNpcName>(ClientLanguage.English)
                        .Where(row => row.Singular.ToString().Contains("striking dummy", StringComparison.OrdinalIgnoreCase))
                        .Select(row => row.RowId)
                        .ToHashSet();
                    IBattleNpc? dummy = Plugin.ObjectTable.OfType<IBattleNpc>()
                        .Where(item => names.Contains(item.NameId))
                        .OrderBy(item => Vector3.Distance(item.Position, Plugin.ObjectTable.LocalPlayer!.Position))
                        .FirstOrDefault();
                    floor = dummy is null
                        ? null
                        : pointOnFloor.InvokeFunc(dummy.Position + new Vector3(0, 5, 0), false, 10);
                }
                else
                {
                    TerritoryType? territory = dataManager.GetExcelSheet<TerritoryType>(ClientLanguage.English)
                        .GetRowOrDefault(territoryId);
                    if (territory is not { } value || value.Map.ValueNullable is null)
                    {
                        Stop("The selected training area's map data is unavailable.");
                        return;
                    }
                    Map map = value.Map.Value;
                    (float x, float z) = StrikingDummyCatalog.MapToWorld(
                        destination.MapX, destination.MapY, map.SizeFactor, map.OffsetX, map.OffsetY);
                    floor = pointOnFloor.InvokeFunc(new Vector3(x, 1024, z), false, 12);
                }
                if (floor is null)
                {
                    Stop(destination.MapX == 0 && destination.MapY == 0
                        ? "No striking dummy is loaded nearby. Move toward the training area and try again."
                        : "vnavmesh could not locate a walkable point near these striking dummies.");
                    return;
                }
                var route = new NavigationRouteSnapshot(
                    Guid.NewGuid(), $"Striking dummies — {destination.DisplayLocation}", territoryId,
                    [new(floor.Value.X, floor.Value.Y, floor.Value.Z)],
                    "Temporary Nexus striking-dummy destination.", "built-in, striking-dummy",
                    true, false, 3f, 3f, 0, 0, string.Empty, false, DateTime.UtcNow);
                var started = navigation.Start(route, NavigationRoutePlanKind.Playback);
                if (!started.IsActive)
                {
                    Stop(started.Message);
                    return;
                }
                state = State.Navigating;
                message = $"Nexus is walking to the {destination.DisplayLocation} striking dummies.";
                return;
            }
            if (state == State.Navigating && !navigation.Status.IsActive)
            {
                message = navigation.Status.State == NavigationRouteExecutionState.Completed
                    ? $"Arrived at {destination.DisplayLocation} striking dummies."
                    : navigation.Status.Message;
                state = State.Idle;
                destination = null;
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.Warning(exception, "Nexus striking-dummy travel failed.");
            Stop("Nexus stopped striking-dummy travel after a provider operation failed.");
        }
    }

    internal bool Stop(out string result)
    {
        if (state == State.Idle)
        {
            result = "No striking-dummy trip is active.";
            return false;
        }
        Stop("Nexus stopped striking-dummy travel.");
        result = message;
        return true;
    }

    private void Stop(string result)
    {
        try
        {
            if (state == State.Teleporting && lifestreamBusy.HasFunction && lifestreamBusy.InvokeFunc() && abortLifestream.HasAction)
                abortLifestream.InvokeAction();
            if (state == State.Navigating)
                navigation.Stop();
        }
        catch (Exception exception)
        {
            Plugin.Log.Debug(exception, "A striking-dummy provider could not be stopped cleanly.");
        }
        Reset(result);
    }

    private uint ResolveTerritory(string zone) => dataManager.GetExcelSheet<TerritoryType>(ClientLanguage.English)
        .Where(row => row.PlaceName.ValueNullable is not null &&
                      row.PlaceName.Value.Name.ToString().Equals(zone, StringComparison.OrdinalIgnoreCase))
        .Select(row => row.RowId)
        .FirstOrDefault();

    private void Reset(string? result = null)
    {
        state = State.Idle;
        destination = null;
        territoryId = 0;
        if (result is not null)
            message = result;
    }

    private enum State
    {
        Idle,
        Teleporting,
        Projecting,
        Navigating,
    }
}
