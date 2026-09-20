using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Numerics;
using VieriNexus.Application;
using NativePlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace VieriNexus.Services;

/// <summary>
/// Nexus-owned whole-trip route coordinator. Lifestream owns teleport and inn-entry operations,
/// vnavmesh owns only the authored local path, and VieriAutoDuty is not part of this flow.
/// </summary>
internal sealed unsafe class NexusRouteTravelProvider : INavigationSuiteTravelProvider
{
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PhaseTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MovementStartGrace = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FlightPreparationTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan FlightActionThrottle = TimeSpan.FromSeconds(2);

    private readonly DependencyService dependencies;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IDataManager dataManager;
    private readonly IAetheryteList aetheryteList;
    private readonly VnavmeshNavigationStopProvider navigation;
    private readonly ICallGateSubscriber<uint, byte, bool> teleport;
    private readonly ICallGateSubscriber<string, bool> aethernetTeleport;
    private readonly ICallGateSubscriber<int?, object> enqueueInnShortcut;
    private readonly ICallGateSubscriber<bool> lifestreamBusy;
    private readonly ICallGateSubscriber<object> abortLifestream;

    private NavigationSuiteRouteRequest.PlaybackRequest? request;
    private TravelPhase phase;
    private DateTimeOffset startedAt;
    private DateTimeOffset phaseStartedAt;
    private DateTimeOffset nextFlightActionAt;
    private bool observedMovement;
    private int authoredPointIndex;
    private uint rootTerritoryId;
    private string? aethernetDestination;
    private uint territoryOnlyTarget;
    private Dictionary<uint, Vector2>? aetherytePositions;
    private SuiteRouteProviderObservation observation = Idle();

    internal NexusRouteTravelProvider(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies,
        IClientState clientState,
        ICondition condition,
        IDataManager dataManager,
        IAetheryteList aetheryteList,
        VnavmeshNavigationStopProvider navigation)
    {
        this.dependencies = dependencies;
        this.clientState = clientState;
        this.condition = condition;
        this.dataManager = dataManager;
        this.aetheryteList = aetheryteList;
        this.navigation = navigation;
        teleport = pluginInterface.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport");
        aethernetTeleport = pluginInterface.GetIpcSubscriber<string, bool>("Lifestream.AethernetTeleport");
        enqueueInnShortcut = pluginInterface.GetIpcSubscriber<int?, object>("Lifestream.EnqueueInnShortcut");
        lifestreamBusy = pluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        abortLifestream = pluginInterface.GetIpcSubscriber<object>("Lifestream.Abort");
    }

    public bool IsAvailable => navigation.IsAvailable;

    public SuiteRouteDispatchResult Dispatch(string requestJson)
    {
        if (phase != TravelPhase.Idle)
            return new(false, "Stop the current Nexus route before starting another one.");
        if (!navigation.IsAvailable)
            return new(false, "vnavmesh is not loaded.");
        if (Plugin.ObjectTable.LocalPlayer is null)
            return new(false, "FFXIV is not logged into a character.");
        if (condition[ConditionFlag.InCombat])
            return new(false, "Route travel cannot begin while in combat.");

        request = NavigationSuiteRouteRequest.Parse(requestJson);
        if (request is null)
            return new(false, "The route request is invalid. It must contain a territory and 1 to 500 valid points.");

        startedAt = DateTimeOffset.UtcNow;
        phaseStartedAt = startedAt;
        observedMovement = false;
        authoredPointIndex = 0;
        rootTerritoryId = 0;
        aethernetDestination = null;

        try
        {
            if (clientState.TerritoryType == request.TerritoryId)
            {
                if (navigation.IsReady)
                    StartAuthoredPath(startedAt);
                else
                    SetRunning(TravelPhase.WaitingForMesh,
                        "nexus-route-waiting-for-mesh",
                        "Nexus reached the route territory and is waiting for vnavmesh to finish loading.");
                return new(true, StartMessage());
            }

            if (!LifestreamAvailable())
                return FailDispatch("Cross-zone route travel requires Lifestream to be loaded and compatible.");
            if (IsLifestreamBusy())
                return FailDispatch("Lifestream is already busy. Let its current trip finish before starting a Nexus route.");

            if (TryGetInnIndex(request.TerritoryId, out int innIndex))
            {
                enqueueInnShortcut.InvokeAction(innIndex);
                SetRunning(TravelPhase.WaitingForTargetTerritory,
                    "nexus-route-entering-inn",
                    "Nexus asked Lifestream to travel to the selected Grand Company inn.");
                return new(true, StartMessage());
            }

            NavigationRoutePoint target = request.Points[0];
            if (!TryResolveTeleport(request.TerritoryId, target.X, target.Z, out TeleportDestination destination))
                return FailDispatch($"Nexus could not find an unlocked Aetheryte or Aethernet route to territory {request.TerritoryId}.");

            if (!teleport.InvokeFunc(destination.RootAetheryteId, 0))
                return FailDispatch("Lifestream did not accept the teleport request.");

            rootTerritoryId = destination.RootTerritoryId;
            aethernetDestination = destination.AethernetName;
            SetRunning(destination.AethernetName is null
                    ? TravelPhase.WaitingForTargetTerritory
                    : TravelPhase.WaitingForAethernetRoot,
                "nexus-route-teleporting",
                "Nexus asked Lifestream to teleport toward the route.");
            return new(true, StartMessage());
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Nexus route travel could not start.");
            return FailDispatch("Nexus could not start route travel through Lifestream and vnavmesh.");
        }
    }

    internal SuiteRouteDispatchResult DispatchToInn(uint territoryId)
    {
        if (phase != TravelPhase.Idle)
            return new(false, "Stop the current Nexus trip before starting another one.");
        if (Plugin.ObjectTable.LocalPlayer is null)
            return new(false, "FFXIV is not logged into a character.");
        if (condition[ConditionFlag.InCombat])
            return new(false, "Inn travel cannot begin while in combat.");
        if (!TryGetInnIndex(territoryId, out int innIndex))
            return new(false, $"Territory {territoryId} is not a supported Grand Company inn.");
        if (clientState.TerritoryType == territoryId)
        {
            observation = new SuiteRouteProviderObservation(
                SuiteRouteProviderState.Completed, "nexus-inn-arrived", "Nexus is already in the selected inn.", false);
            return new(true, observation.Message);
        }
        if (!LifestreamAvailable())
            return new(false, "Inn travel requires Lifestream to be loaded and compatible.");
        if (IsLifestreamBusy())
            return new(false, "Lifestream is already busy. Let its current trip finish first.");
        try
        {
            startedAt = DateTimeOffset.UtcNow;
            phaseStartedAt = startedAt;
            territoryOnlyTarget = territoryId;
            enqueueInnShortcut.InvokeAction(innIndex);
            SetRunning(TravelPhase.WaitingForInnOnly,
                "nexus-maintenance-entering-inn", "Nexus asked Lifestream to enter the selected Grand Company inn.");
            return new(true, observation.Message);
        }
        catch (Exception exception)
        {
            Plugin.Log.Warning(exception, "Nexus maintenance could not start inn travel.");
            return FailDispatch("Lifestream did not accept the Grand Company inn request.");
        }
    }

    public bool Stop()
    {
        try
        {
            if (IsLifestreamBusy() && abortLifestream.HasAction)
                abortLifestream.InvokeAction();
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Lifestream abort was unavailable while stopping a Nexus route.");
        }

        try
        {
            if (phase == TravelPhase.MovingAuthoredPath)
                navigation.RequestStop();
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "vnavmesh Stop was unavailable while stopping a Nexus route.");
        }

        Reset();
        return true;
    }

    public SuiteRouteProviderObservation Observe(DateTimeOffset now)
    {
        if (phase == TravelPhase.Idle)
            return observation;
        if (now - startedAt > OverallTimeout)
            return Fail("nexus-route-timeout", "Nexus stopped the route because the complete trip exceeded 15 minutes.");
        if (!navigation.IsAvailable)
            return Fail("nexus-route-vnavmesh-lost", "vnavmesh became unavailable; Nexus stopped the route and will not replay it.");
        if (Plugin.ObjectTable.LocalPlayer is null ||
            condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])
            return observation;

        try
        {
            if (request is not null &&
                phase is TravelPhase.WaitingForAethernetRoot or TravelPhase.WaitingForTargetTerritory)
            {
                NavigationArrivalAction arrival = NavigationArrivalPolicy.Decide(
                    clientState.TerritoryType == request.TerritoryId,
                    navigation.IsReady,
                    now - phaseStartedAt > PhaseTimeout);
                if (arrival is NavigationArrivalAction.StartLocalPath or NavigationArrivalAction.WaitForMesh)
                {
                    ReleaseCompletedLifestreamLeg();
                    if (arrival == NavigationArrivalAction.StartLocalPath)
                        StartAuthoredPath(now);
                    else
                        SetRunning(TravelPhase.WaitingForMesh,
                            "nexus-route-waiting-for-mesh",
                            "Nexus reached the route territory and is waiting for vnavmesh to finish loading.",
                            now: now);
                    return observation;
                }
                if (arrival == NavigationArrivalAction.Fail)
                    return Fail("nexus-route-territory-timeout",
                        "Lifestream did not reach the route territory within two minutes.");
            }

            switch (phase)
            {
                case TravelPhase.WaitingForInnOnly:
                    if (clientState.TerritoryType == territoryOnlyTarget && !IsLifestreamBusy())
                    {
                        observation = new SuiteRouteProviderObservation(
                            SuiteRouteProviderState.Completed, "nexus-inn-arrived",
                            "Nexus reached the selected Grand Company inn.", false);
                        phase = TravelPhase.Idle;
                        territoryOnlyTarget = 0;
                    }
                    else if (!IsLifestreamBusy() && now - phaseStartedAt > PhaseTimeout)
                        return Fail("nexus-inn-timeout", "Lifestream stopped without reaching the selected Grand Company inn.");
                    break;

                case TravelPhase.WaitingForAethernetRoot:
                    if (!IsLifestreamBusy() && clientState.TerritoryType == rootTerritoryId)
                    {
                        if (string.IsNullOrWhiteSpace(aethernetDestination) ||
                            !aethernetTeleport.InvokeFunc(aethernetDestination))
                            return Fail("nexus-route-aethernet-rejected",
                                "Lifestream did not accept the Aethernet transfer toward the route.");
                        SetRunning(TravelPhase.WaitingForTargetTerritory,
                            "nexus-route-using-aethernet",
                            $"Nexus is using Lifestream to reach {aethernetDestination}.");
                    }
                    else if (now - phaseStartedAt > PhaseTimeout)
                        return Fail("nexus-route-root-timeout", "The teleport completed without reaching the expected city Aetheryte.");
                    break;

                case TravelPhase.WaitingForTargetTerritory:
                    if (now - phaseStartedAt > PhaseTimeout)
                        return Fail("nexus-route-territory-timeout", "Lifestream stopped without reaching the route territory.");
                    break;

                case TravelPhase.WaitingForMesh:
                    if (clientState.TerritoryType != request!.TerritoryId)
                        return Fail("nexus-route-territory-changed",
                            "The active territory changed while Nexus was waiting for vnavmesh.");
                    if (navigation.IsReady)
                        StartAuthoredPath(now);
                    else if (now - phaseStartedAt > PhaseTimeout)
                        return Fail("nexus-route-mesh-timeout",
                            "vnavmesh did not become ready within two minutes after the zone finished loading.");
                    break;

                case TravelPhase.PreparingAuthoredLeg:
                    ContinueAuthoredLegPreparation(now);
                    break;

                case TravelPhase.MovingAuthoredPath:
                    bool? active = navigation.IsMovementActive();
                    if (active is true)
                    {
                        observedMovement = true;
                        bool? owned = navigation.IsDestinationOwned(request!.Points[authoredPointIndex]);
                        if (owned is false)
                            return Fail("nexus-route-path-replaced",
                                "Another plugin replaced the Nexus path. Nexus yielded without stopping that plugin.", stopOwnedMovement: false);
                    }
                    else if (active is false && (observedMovement || now - phaseStartedAt >= MovementStartGrace))
                    {
                        authoredPointIndex++;
                        if (authoredPointIndex < request!.Points.Count)
                            BeginAuthoredLeg(now);
                        else
                        {
                            string routeName = request.TravelOnly ? "Travel to start completed." : "Route playback completed.";
                            observation = new SuiteRouteProviderObservation(
                                SuiteRouteProviderState.Completed,
                                "nexus-route-completed",
                                routeName,
                                false);
                            phase = TravelPhase.Idle;
                            request = null;
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Nexus route travel failed during {Phase}.", phase);
            return Fail("nexus-route-provider-failed", "Nexus stopped the route after a provider operation failed.");
        }

        return observation;
    }

    private void StartAuthoredPath(DateTimeOffset now)
    {
        if (request is null)
            throw new InvalidOperationException("No Nexus route request is active.");
        if (clientState.TerritoryType != request.TerritoryId)
            throw new InvalidOperationException("The route territory is not active.");
        if (!navigation.IsReady)
            throw new InvalidOperationException("vnavmesh is not ready in the route territory.");

        if (request.ResolveDestinationFloor)
        {
            var resolvedPoints = new List<NavigationRoutePoint>(request.Points.Count);
            foreach (NavigationRoutePoint point in request.Points)
            {
                Vector3 approximate = new(point.X, 1024f, point.Z);
                Vector3? floor = navigation.PointOnFloor(approximate, false, 5f) ??
                                 navigation.PointOnFloor(approximate, true, 12f);
                if (floor is null)
                    throw new InvalidOperationException(
                        "The selected map point is not on a reachable navigation surface.");
                resolvedPoints.Add(new NavigationRoutePoint(floor.Value.X, floor.Value.Y, floor.Value.Z));
            }

            request = request with
            {
                Points = resolvedPoints,
                ResolveDestinationFloor = false,
            };
        }

        authoredPointIndex = 0;
        territoryOnlyTarget = 0;
        BeginAuthoredLeg(now);
    }

    private void BeginAuthoredLeg(DateTimeOffset now)
    {
        nextFlightActionAt = now;
        SetRunning(
            TravelPhase.PreparingAuthoredLeg,
            "nexus-route-preparing-leg",
            "Nexus is preparing the local route from the arrival Aetheryte.",
            now: now);
        ContinueAuthoredLegPreparation(now);
    }

    private void ContinueAuthoredLegPreparation(DateTimeOffset now)
    {
        if (request is null || authoredPointIndex < 0 || authoredPointIndex >= request.Points.Count)
            throw new InvalidOperationException("No authored Nexus route point is available.");

        NavigationAuthoredLeg leg = NavigationAuthoredLegPolicy.Create(
            request,
            authoredPointIndex,
            FlightPathSupported(request.TerritoryId));
        // Preserve VieriAutoDuty's proven vendor behavior: short approaches must stay on the
        // ground. Asking vnavmesh for a flight path across a counter or a few nearby yalms can
        // leave its asynchronous path calculation pending forever and block both manual shopping
        // and the duty preflight that is waiting for gear readiness.
        float directDistance = Plugin.ObjectTable.LocalPlayer is { } player
            ? Vector3.Distance(
                player.Position,
                new Vector3(leg.Destination.X, leg.Destination.Y, leg.Destination.Z))
            : float.MaxValue;
        bool useFlight = NavigationAuthoredLegPolicy.ShouldUseFlightForLeg(
            leg.UseFlight,
            request.VendorTargetDataId != 0,
            directDistance);

        NavigationFlightPreparationAction preparation = NavigationFlightPreparationPolicy.Decide(
            useFlight,
            FlightPathSupported(request.TerritoryId),
            condition[ConditionFlag.Mounted],
            condition[ConditionFlag.InFlight],
            now - phaseStartedAt >= FlightPreparationTimeout);
        switch (preparation)
        {
            case NavigationFlightPreparationAction.Mount:
                observation = observation with
                {
                    Code = "nexus-route-mounting",
                    Message = "Nexus is mounting for the vendor approach.",
                };
                if (now >= nextFlightActionAt)
                {
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
                    nextFlightActionAt = now + FlightActionThrottle;
                }
                return;

            case NavigationFlightPreparationAction.TakeOff:
                observation = observation with
                {
                    Code = "nexus-route-taking-off",
                    Message = "Nexus is taking off for the vendor approach.",
                };
                if (now >= nextFlightActionAt)
                {
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                    nextFlightActionAt = now + FlightActionThrottle;
                }
                return;

            case NavigationFlightPreparationAction.UseGroundPath:
                useFlight = false;
                break;

            case NavigationFlightPreparationAction.UseFlightPath:
                useFlight = true;
                break;
        }

        if (leg.RequiresPathfinding)
            navigation.StartPathfinding(leg.Destination, useFlight, leg.Tolerance);
        else
            navigation.Start([leg.Destination], useFlight, leg.Tolerance);

        observedMovement = false;
        SetRunning(TravelPhase.MovingAuthoredPath,
            "nexus-route-playing",
            request.TravelOnly
                ? "Nexus is pathfinding to the route start through vnavmesh."
                : $"Nexus is pathfinding through all {request.Points.Count} authored route points with vnavmesh.",
            visualizationActive: true,
            now);
    }

    private bool FlightPathSupported(uint territoryId)
    {
        TerritoryType? territory = dataManager.GetExcelSheet<TerritoryType>()
            .GetRowOrDefault(territoryId);
        uint currentSet = territory?.AetherCurrentCompFlgSet.RowId ?? 0;
        NativePlayerState* playerState = NativePlayerState.Instance();
        return territory?.TerritoryIntendedUse.RowId is 1 or 47 or 49 &&
               currentSet != 0 &&
               playerState is not null &&
               playerState->IsAetherCurrentZoneComplete(currentSet);
    }

    private bool TryResolveTeleport(uint territoryId, float targetX, float targetZ, out TeleportDestination destination)
    {
        var sheet = dataManager.GetExcelSheet<Aetheryte>();
        IReadOnlyDictionary<uint, Vector2> positions = AetherytePositions(sheet);
        NavigationArrivalCandidate[] candidates = sheet
            .Where(row => row.Territory.RowId != 0)
            .Select(row =>
            {
                Vector2? position = positions.TryGetValue(row.RowId, out Vector2 value) ? value : null;
                return new NavigationArrivalCandidate(
                    row.RowId,
                    row.Territory.RowId,
                    row.AethernetGroup,
                    row.IsAetheryte,
                    row.AethernetName.ValueNullable?.Name.ToString(),
                    IsUnlocked(row.RowId),
                    position?.X,
                    position?.Y);
            })
            .ToArray();
        NavigationArrivalSelection? selected = NavigationEndpointPolicy.Select(
            territoryId, targetX, targetZ, candidates);
        if (selected is null)
        {
            destination = default;
            return false;
        }

        destination = new TeleportDestination(
            selected.RootAetheryteId,
            selected.RootTerritoryId,
            selected.AethernetName);
        return true;
    }

    private IReadOnlyDictionary<uint, Vector2> AetherytePositions(ExcelSheet<Aetheryte> aetherytes) =>
        aetherytePositions ??= BuildAetherytePositions(aetherytes);

    private Dictionary<uint, Vector2> BuildAetherytePositions(ExcelSheet<Aetheryte> aetherytes)
    {
        ExcelSheet<Map> maps = dataManager.GetExcelSheet<Map>();
        SubrowExcelSheet<MapMarker> markers = dataManager.GetSubrowExcelSheet<MapMarker>();
        Dictionary<uint, uint> aetheryteByAethernetName = [];
        foreach (Aetheryte aetheryte in aetherytes)
        {
            if (aetheryte.AethernetName.RowId != 0)
                aetheryteByAethernetName[aetheryte.AethernetName.RowId] = aetheryte.RowId;
        }

        Dictionary<uint, Vector2> positions = [];
        foreach (Map map in maps)
        {
            if (map.MapMarkerRange == 0 || map.SizeFactor == 0 ||
                !markers.TryGetRow(map.MapMarkerRange, out SubrowCollection<MapMarker> group))
                continue;

            foreach (MapMarker marker in group)
            {
                uint? aetheryteId = marker.DataType switch
                {
                    3 => marker.DataKey.RowId,
                    4 when aetheryteByAethernetName.TryGetValue(marker.DataKey.RowId, out uint id) => id,
                    _ => null,
                };
                if (aetheryteId is not { } id2 || id2 == 0)
                    continue;

                float scale = map.SizeFactor / 100f;
                Vector2 world = new(
                    (marker.X - 1024f) / scale - map.OffsetX,
                    (marker.Y - 1024f) / scale - map.OffsetY);
                bool preferred = map.TerritoryType.RowId != 0 &&
                                 map.TerritoryType.RowId == aetherytes.GetRowOrDefault(id2)?.Territory.RowId;
                if (preferred || !positions.ContainsKey(id2))
                    positions[id2] = world;
            }
        }
        return positions;
    }

    private bool IsUnlocked(uint aetheryteId) =>
        aetheryteList.Any(entry => entry.AetheryteId == aetheryteId);

    private bool LifestreamAvailable() =>
        dependencies.FindPlugin("Lifestream").IsLoaded &&
        teleport.HasFunction && aethernetTeleport.HasFunction && enqueueInnShortcut.HasAction &&
        lifestreamBusy.HasFunction && abortLifestream.HasAction;

    private bool IsLifestreamBusy() =>
        lifestreamBusy.HasFunction && lifestreamBusy.InvokeFunc();

    private void ReleaseCompletedLifestreamLeg()
    {
        try
        {
            if (IsLifestreamBusy() && abortLifestream.HasAction)
                abortLifestream.InvokeAction();
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Lifestream retained its busy state after Nexus reached the route territory.");
        }
    }

    private string StartMessage() => request!.TravelOnly
        ? "Started travel to the route start through Nexus."
        : $"Started route playback through Nexus ({request.Points.Count} point{(request.Points.Count == 1 ? string.Empty : "s")}).";

    private SuiteRouteDispatchResult FailDispatch(string message)
    {
        Reset();
        return new SuiteRouteDispatchResult(false, message);
    }

    private SuiteRouteProviderObservation Fail(string code, string message, bool stopOwnedMovement = true)
    {
        if (stopOwnedMovement)
        {
            try
            {
                if (phase == TravelPhase.MovingAuthoredPath)
                    navigation.RequestStop();
                if (IsLifestreamBusy() && abortLifestream.HasAction)
                    abortLifestream.InvokeAction();
            }
            catch (Exception ex)
            {
                Plugin.Log.Debug(ex, "A provider could not be stopped after Nexus route failure.");
            }
        }
        observation = new SuiteRouteProviderObservation(SuiteRouteProviderState.Failed, code, message, false);
        phase = TravelPhase.Idle;
        request = null;
        return observation;
    }

    private void SetRunning(
        TravelPhase nextPhase,
        string code,
        string message,
        bool visualizationActive = false,
        DateTimeOffset? now = null)
    {
        phase = nextPhase;
        phaseStartedAt = now ?? DateTimeOffset.UtcNow;
        observation = new SuiteRouteProviderObservation(
            SuiteRouteProviderState.Running,
            code,
            message,
            visualizationActive);
    }

    private void Reset()
    {
        request = null;
        phase = TravelPhase.Idle;
        rootTerritoryId = 0;
        aethernetDestination = null;
        observedMovement = false;
        authoredPointIndex = 0;
        nextFlightActionAt = default;
        observation = Idle();
        territoryOnlyTarget = 0;
    }

    private static bool TryGetInnIndex(uint territoryId, out int innIndex)
    {
        innIndex = territoryId switch
        {
            177 => 0, // Limsa Lominsa / Maelstrom
            178 => 1, // Ul'dah / Immortal Flames
            179 => 2, // Gridania / Twin Adder
            _ => -1,
        };
        return innIndex >= 0;
    }

    private static SuiteRouteProviderObservation Idle() => new(
        SuiteRouteProviderState.Idle,
        "nexus-route-idle",
        "No Nexus route trip is running.",
        false);

    private enum TravelPhase
    {
        Idle,
        WaitingForAethernetRoot,
        WaitingForTargetTerritory,
        WaitingForMesh,
        PreparingAuthoredLeg,
        WaitingForInnOnly,
        MovingAuthoredPath,
    }

    private readonly record struct TeleportDestination(
        uint RootAetheryteId,
        uint RootTerritoryId,
        string? AethernetName);
}
