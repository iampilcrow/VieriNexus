using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using VieriNexus.Application;

namespace VieriNexus.Services;

/// <summary>
/// Nexus-owned whole-trip route coordinator. Lifestream owns teleport and inn-entry operations,
/// vnavmesh owns only the authored local path, and VieriAutoDuty is not part of this flow.
/// </summary>
internal sealed class NexusRouteTravelProvider : INavigationSuiteTravelProvider
{
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PhaseTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MovementStartGrace = TimeSpan.FromSeconds(2);

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
    private bool observedMovement;
    private int authoredPointIndex;
    private uint rootTerritoryId;
    private string? aethernetDestination;
    private uint territoryOnlyTarget;
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
                if (!navigation.IsReady)
                    return FailDispatch("vnavmesh is not ready in the current territory.");
                StartAuthoredPath(startedAt);
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

            if (!TryResolveTeleport(request.TerritoryId, out TeleportDestination destination))
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
                    if (clientState.TerritoryType == request!.TerritoryId && navigation.IsReady)
                        StartAuthoredPath(now);
                    else if (!IsLifestreamBusy() && clientState.TerritoryType == rootTerritoryId)
                    {
                        if (string.IsNullOrWhiteSpace(aethernetDestination) ||
                            !aethernetTeleport.InvokeFunc(aethernetDestination))
                            return Fail("nexus-route-aethernet-rejected",
                                "Lifestream did not accept the Aethernet transfer toward the route.");
                        SetRunning(TravelPhase.WaitingForTargetTerritory,
                            "nexus-route-using-aethernet",
                            $"Nexus is using Lifestream to reach {aethernetDestination}.");
                    }
                    else if (!IsLifestreamBusy() && now - phaseStartedAt > PhaseTimeout)
                        return Fail("nexus-route-root-timeout", "The teleport completed without reaching the expected city Aetheryte.");
                    break;

                case TravelPhase.WaitingForTargetTerritory:
                    if (clientState.TerritoryType == request!.TerritoryId && !IsLifestreamBusy() && navigation.IsReady)
                        StartAuthoredPath(now);
                    else if (!IsLifestreamBusy() && now - phaseStartedAt > PhaseTimeout)
                        return Fail("nexus-route-territory-timeout", "Lifestream stopped without reaching the route territory.");
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
                            StartAuthoredLeg(now);
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

        authoredPointIndex = 0;
        territoryOnlyTarget = 0;
        StartAuthoredLeg(now);
        SetRunning(TravelPhase.MovingAuthoredPath,
            "nexus-route-playing",
            request.TravelOnly
                ? "Nexus is pathfinding to the route start through vnavmesh."
                : $"Nexus is pathfinding through all {request.Points.Count} authored route points with vnavmesh.",
            visualizationActive: true,
            now);
    }

    private void StartAuthoredLeg(DateTimeOffset now)
    {
        if (request is null || authoredPointIndex < 0 || authoredPointIndex >= request.Points.Count)
            throw new InvalidOperationException("No authored Nexus route point is available.");

        NavigationAuthoredLeg leg = NavigationAuthoredLegPolicy.Create(
            request,
            authoredPointIndex,
            FlightPathSupported(request.TerritoryId));
        if (leg.RequiresPathfinding)
            navigation.StartPathfinding(leg.Destination, leg.UseFlight, leg.Tolerance);
        else
            navigation.Start([leg.Destination], leg.UseFlight, leg.Tolerance);

        observedMovement = false;
        phaseStartedAt = now;
    }

    private bool FlightPathSupported(uint territoryId)
    {
        TerritoryType? territory = dataManager.GetExcelSheet<TerritoryType>()
            .GetRowOrDefault(territoryId);
        return territory?.TerritoryIntendedUse.RowId is 1 or 47 or 49;
    }

    private bool TryResolveTeleport(uint territoryId, out TeleportDestination destination)
    {
        var sheet = dataManager.GetExcelSheet<Aetheryte>();
        Aetheryte? direct = sheet
            .Where(row => row.IsAetheryte && row.Territory.RowId == territoryId && IsUnlocked(row.RowId))
            .OrderBy(row => row.RowId)
            .Cast<Aetheryte?>()
            .FirstOrDefault();
        if (direct is { } directValue)
        {
            destination = new TeleportDestination(directValue.RowId, territoryId, null);
            return true;
        }

        Aetheryte? node = sheet
            .Where(row => !row.IsAetheryte && row.Territory.RowId == territoryId && row.AethernetGroup != 0 &&
                          row.AethernetName.ValueNullable is not null)
            .OrderBy(row => row.RowId)
            .Cast<Aetheryte?>()
            .FirstOrDefault();
        if (node is not { } nodeValue)
        {
            destination = default;
            return false;
        }

        Aetheryte? root = sheet
            .Where(row => row.IsAetheryte && row.AethernetGroup == nodeValue.AethernetGroup && IsUnlocked(row.RowId))
            .OrderBy(row => row.RowId)
            .Cast<Aetheryte?>()
            .FirstOrDefault();
        if (root is not { } rootValue)
        {
            destination = default;
            return false;
        }

        destination = new TeleportDestination(
            rootValue.RowId,
            rootValue.Territory.RowId,
            nodeValue.AethernetName.Value.Name.ToString());
        return true;
    }

    private bool IsUnlocked(uint aetheryteId) =>
        aetheryteList.Any(entry => entry.AetheryteId == aetheryteId);

    private bool LifestreamAvailable() =>
        dependencies.FindPlugin("Lifestream").IsLoaded &&
        teleport.HasFunction && aethernetTeleport.HasFunction && enqueueInnShortcut.HasAction &&
        lifestreamBusy.HasFunction && abortLifestream.HasAction;

    private bool IsLifestreamBusy() =>
        lifestreamBusy.HasFunction && lifestreamBusy.InvokeFunc();

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
        WaitingForInnOnly,
        MovingAuthoredPath,
    }

    private readonly record struct TeleportDestination(
        uint RootAetheryteId,
        uint RootTerritoryId,
        string? AethernetName);
}
