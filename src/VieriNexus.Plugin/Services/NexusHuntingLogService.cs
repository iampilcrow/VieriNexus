using System.Numerics;
using System.Reflection;
using System.Text.Json.Nodes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

/// <summary>
/// Nexus-owned bounded Hunting Log executor. Nexus chooses one exact target and verifies its live
/// kill count; Lifestream, vnavmesh, and BossMod provide only teleport, pathing, and rotation.
/// </summary>
internal sealed class NexusHuntingLogService : IProgressionHuntingProvider
{
    private const string PresetName = "VieriNexus - Hunting Log";
    private const string PresetResource = "VieriNexus.Data.BossModPreset_Overworld.json";
    private const string MovementModule = "BossMod.Autorotation.MiscAI.NormalMovement";
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan CombatTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CreditTimeout = TimeSpan.FromSeconds(10);

    private readonly ProgressAtlasService atlas;
    private readonly NexusRouteTravelProvider travel;
    private readonly VnavmeshNavigationStopProvider navigation;
    private readonly DependencyService dependencies;
    private readonly IClientState clientState;
    private readonly IPlayerState playerState;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private readonly ICondition condition;
    private readonly ICommandManager commandManager;
    private readonly ICallGateSubscriber<string, string?> getPreset;
    private readonly ICallGateSubscriber<string?> getActivePreset;
    private readonly ICallGateSubscriber<string, bool, bool> createPreset;
    private readonly ICallGateSubscriber<string, bool> setPreset;
    private readonly ICallGateSubscriber<bool> clearPreset;
    private readonly ICallGateSubscriber<IReadOnlyList<string>, bool, IReadOnlyList<string>> bossConfiguration;

    private ProgressionHuntingTargetCandidate? activeTarget;
    private IReadOnlyList<HuntingLogLocation> locations = [];
    private int locationIndex;
    private RunPhase phase;
    private DateTimeOffset phaseStartedAt;
    private DateTimeOffset nextDismountAt;
    private ulong engagedObjectId;
    private string detail = "No Nexus Hunting Log target is active.";
    private bool failed;
    private bool complete;
    private string? previousPreset;
    private bool? previousClearOnCombatEnd;
    private bool? previousQuestBattles;

    internal NexusHuntingLogService(
        IDalamudPluginInterface pluginInterface,
        ProgressAtlasService atlas,
        NexusRouteTravelProvider travel,
        VnavmeshNavigationStopProvider navigation,
        DependencyService dependencies,
        IClientState clientState,
        IPlayerState playerState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        ICondition condition,
        ICommandManager commandManager)
    {
        this.atlas = atlas;
        this.travel = travel;
        this.navigation = navigation;
        this.dependencies = dependencies;
        this.clientState = clientState;
        this.playerState = playerState;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.condition = condition;
        this.commandManager = commandManager;
        getPreset = pluginInterface.GetIpcSubscriber<string, string?>("BossMod.Presets.Get");
        getActivePreset = pluginInterface.GetIpcSubscriber<string?>("BossMod.Presets.GetActive");
        createPreset = pluginInterface.GetIpcSubscriber<string, bool, bool>("BossMod.Presets.Create");
        setPreset = pluginInterface.GetIpcSubscriber<string, bool>("BossMod.Presets.SetActive");
        clearPreset = pluginInterface.GetIpcSubscriber<bool>("BossMod.Presets.ClearActive");
        bossConfiguration = pluginInterface.GetIpcSubscriber<IReadOnlyList<string>, bool, IReadOnlyList<string>>(
            "BossMod.Configuration");
    }

    public ProviderId Id { get; } = new("vieri.nexus.hunting-log/v1");

    internal bool IsReady => IsProviderReady(out _);

    internal string ReadinessDetail => IsProviderReady(out string reason)
        ? "Nexus owns Hunting Log selection and verification; stock Lifestream, vnavmesh, and Boss Mod supply narrow mechanics."
        : reason;

    public IReadOnlyList<ProgressionHuntingTargetCandidate> EligibleTargets(uint classJobId, int currentLevel)
    {
        if (!IsProviderReady(out _) || !clientState.IsLoggedIn || objectTable.LocalPlayer is null)
            return [];

        int grandCompanyRank = CurrentGrandCompanyRank();
        HuntingLogTargetProgress[] eligible = atlas.HuntingTargets
            .Where(target => target.IsCurrentRank && !target.IsComplete && target.HasOpenWorldLocation)
            .Where(target => currentLevel >= RequiredLevel(target))
            .Where(target => target.LogKey < 10_000 || grandCompanyRank >= RequiredGrandCompanyRank(target.Rank))
            .ToArray();
        HuntingLogTargetProgress? selected = HuntingLogCandidatePolicy.SelectNext(
            eligible,
            clientState.TerritoryType,
            new HashSet<(uint, int, int, int)>());
        return selected is null
            ? []
            : [ToCandidate(selected)];
    }

    public ProgressionHuntingProviderObservation ObserveHunt(ProgressionHuntingTargetCandidate target)
    {
        HuntingLogTargetProgress? progress = FindProgress(target);
        int killed = progress?.Killed ?? target.Killed;
        bool verifiedComplete = progress?.IsComplete == true;
        bool providerReady = IsProviderReady(out string unavailable);
        // Once Nexus has synchronously stopped and cleared its own state, the coordinator can
        // safely confirm inactivity even if a provider disappeared. Readiness for a new run is
        // still reported separately through IsReady.
        bool available = phase == RunPhase.Idle || providerReady;
        string currentDetail = providerReady || phase == RunPhase.Idle ? detail : unavailable;
        bool? busy = available ? phase != RunPhase.Idle : null;
        return new ProgressionHuntingProviderObservation(
            available,
            busy,
            complete && verifiedComplete,
            failed,
            killed,
            target.Required,
            currentDetail);
    }

    public bool TryStartHunt(ProgressionHuntingTargetCandidate target, out string message)
    {
        if (phase != RunPhase.Idle)
        {
            message = "Stop the current Hunting Log target before starting another one.";
            return false;
        }
        if (!IsProviderReady(out message))
            return false;
        if (condition[ConditionFlag.InCombat])
        {
            message = "Hunting Log travel cannot begin while already in combat.";
            return false;
        }
        if (target.Locations.All(location => !location.IsOpenWorld))
        {
            message = "This Hunting Log target is duty-only; the open-world executor cannot run it.";
            return false;
        }

        try
        {
            activeTarget = target;
            locations = target.Locations
                .Where(location => location.IsOpenWorld)
                .OrderByDescending(location => location.TerritoryId == clientState.TerritoryType)
                .ThenBy(location => location.TerritoryId)
                .ThenBy(location => location.MapId)
                .ThenBy(location => location.MapX)
                .ThenBy(location => location.MapY)
                .ToArray();
            locationIndex = 0;
            failed = false;
            complete = false;
            engagedObjectId = 0;
            CaptureBossModState();
            if (!StartTravelToCurrentLocation(out message))
            {
                CleanupProviders(restorePreset: true);
                ResetToIdle(message, didFail: true);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Nexus could not start Hunting Log target {Target}.", target.TargetName);
            message = "Nexus could not initialize the stock Hunting Log providers.";
            CleanupProviders(restorePreset: true);
            ResetToIdle(message, didFail: true);
            return false;
        }
    }

    public bool TryStopHunt(out string message)
    {
        CleanupProviders(restorePreset: true);
        ResetToIdle("Hunting Log work stopped. No target will be replayed.");
        message = detail;
        return true;
    }

    internal void Update(DateTimeOffset now)
    {
        if (phase == RunPhase.Idle || activeTarget is null)
            return;

        HuntingLogTargetProgress? progress = FindProgress(activeTarget);
        if (progress?.IsComplete == true)
        {
            CleanupProviders(restorePreset: true);
            complete = true;
            phase = RunPhase.Idle;
            detail = $"Verified {activeTarget.TargetName} complete ({progress.Killed}/{progress.Required}).";
            return;
        }

        if (!IsProviderReady(out string unavailable))
        {
            Fail(unavailable);
            return;
        }

        try
        {
            switch (phase)
            {
                case RunPhase.Travelling:
                    UpdateTravel(now);
                    break;
                case RunPhase.Searching:
                    UpdateSearch(now);
                    break;
                case RunPhase.Approaching:
                    UpdateApproach(now);
                    break;
                case RunPhase.Engaging:
                    UpdateCombat(now, progress);
                    break;
                case RunPhase.WaitingForCredit:
                    UpdateCredit(now, progress);
                    break;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Nexus Hunting Log execution failed during {Phase}.", phase);
            Fail("A stock Hunting Log provider operation failed. Nexus stopped travel and combat.");
        }
    }

    internal void Shutdown()
    {
        if (phase != RunPhase.Idle)
            TryStopHunt(out _);
    }

    private void UpdateTravel(DateTimeOffset now)
    {
        SuiteRouteProviderObservation observation = travel.Observe(now);
        if (observation.State == SuiteRouteProviderState.Failed)
        {
            TryNextLocationOrFail($"Travel failed near the selected {activeTarget!.TargetName} camp: {observation.Message}");
            return;
        }
        if (observation.State != SuiteRouteProviderState.Completed)
        {
            detail = $"Travelling to {activeTarget!.TargetName}: {observation.Message}";
            return;
        }

        phase = RunPhase.Searching;
        phaseStartedAt = now;
        detail = $"Searching the known camp for {activeTarget!.TargetName}.";
    }

    private void UpdateSearch(DateTimeOffset now)
    {
        IBattleNpc? target = FindNearestTarget();
        if (target is not null)
        {
            engagedObjectId = target.GameObjectId;
            BeginEngagementOrApproach(now, target);
            return;
        }

        if (now - phaseStartedAt >= SearchTimeout)
            TryNextLocationOrFail($"No {activeTarget!.TargetName} appeared at the known camp within {SearchTimeout.TotalSeconds:0} seconds.");
    }

    private void BeginEngagementOrApproach(DateTimeOffset now, IBattleNpc target)
    {
        if (objectTable.LocalPlayer is not { } player)
            return;
        float range = RequiresBossModMovement(playerState.ClassJob.RowId) ? 3f : 20f;
        float distance = Vector3.Distance(player.Position, target.Position);
        if (distance > range + 2f)
        {
            navigation.StartPathfinding(
                new NavigationRoutePoint(target.Position.X, target.Position.Y, target.Position.Z),
                useFlight: false,
                tolerance: range);
            phase = RunPhase.Approaching;
            phaseStartedAt = now;
            detail = $"Approaching {activeTarget!.TargetName} through vnavmesh.";
            return;
        }

        if (!TryDismount(now))
            return;
        targetManager.Target = target;
        EnableBossModPreset();
        phase = RunPhase.Engaging;
        phaseStartedAt = now;
        detail = $"Engaging {activeTarget!.TargetName}; Nexus is waiting for exact Hunting Log credit.";
    }

    private void UpdateApproach(DateTimeOffset now)
    {
        IBattleNpc? target = objectTable.OfType<IBattleNpc>()
            .FirstOrDefault(candidate => candidate.GameObjectId == engagedObjectId && !candidate.IsDead && candidate.IsTargetable);
        if (target is null)
        {
            navigation.RequestStop();
            phase = RunPhase.Searching;
            phaseStartedAt = now;
            detail = $"The selected {activeTarget!.TargetName} moved or despawned; searching the camp again.";
            return;
        }

        if (objectTable.LocalPlayer is not { } player)
            return;
        float range = RequiresBossModMovement(playerState.ClassJob.RowId) ? 3f : 20f;
        if (Vector3.Distance(player.Position, target.Position) <= range + 2f)
        {
            navigation.RequestStop();
            BeginEngagementOrApproach(now, target);
            return;
        }

        if (navigation.IsMovementActive() is false)
        {
            navigation.StartPathfinding(
                new NavigationRoutePoint(target.Position.X, target.Position.Y, target.Position.Z),
                useFlight: false,
                tolerance: range);
        }
        if (now - phaseStartedAt >= TimeSpan.FromMinutes(1))
            TryNextLocationOrFail($"Nexus could not reach {activeTarget!.TargetName} within one minute.");
    }

    private bool TryDismount(DateTimeOffset now)
    {
        if (!condition[ConditionFlag.Mounted])
            return true;
        if (now >= nextDismountAt)
        {
            unsafe
            {
                ActionManager* actions = ActionManager.Instance();
                if (actions is not null)
                    actions->UseAction(ActionType.GeneralAction, 23);
            }
            nextDismountAt = now.AddSeconds(1);
        }
        detail = $"Found {activeTarget!.TargetName}; dismounting before combat.";
        return false;
    }

    private void UpdateCombat(DateTimeOffset now, HuntingLogTargetProgress? progress)
    {
        if (progress is not null && progress.Killed > activeTarget!.Killed)
        {
            DisableBossModPreset(restorePrevious: false);
            phase = RunPhase.WaitingForCredit;
            phaseStartedAt = now;
            detail = $"Hunting Log credit advanced to {progress.Killed}/{progress.Required}; verifying whether another kill is needed.";
            return;
        }

        IBattleNpc? engaged = objectTable.OfType<IBattleNpc>()
            .FirstOrDefault(candidate => candidate.GameObjectId == engagedObjectId);
        if (engaged is null || engaged.IsDead)
        {
            DisableBossModPreset(restorePrevious: false);
            phase = RunPhase.WaitingForCredit;
            phaseStartedAt = now;
            detail = $"{activeTarget!.TargetName} was defeated; waiting for Hunting Log credit.";
            return;
        }

        if (targetManager.Target?.GameObjectId != engaged.GameObjectId)
            targetManager.Target = engaged;
        if (now - phaseStartedAt >= CombatTimeout)
            TryNextLocationOrFail($"Combat with {activeTarget!.TargetName} exceeded the bounded two-minute limit.");
    }

    private void UpdateCredit(DateTimeOffset now, HuntingLogTargetProgress? progress)
    {
        if (progress?.IsComplete == true)
            return;
        if (progress is not null && progress.Killed > activeTarget!.Killed)
        {
            activeTarget = activeTarget with { Killed = progress.Killed };
            phase = RunPhase.Searching;
            phaseStartedAt = now;
            detail = $"Searching for the next {activeTarget.TargetName} ({progress.Killed}/{progress.Required}).";
            return;
        }
        if (now - phaseStartedAt >= CreditTimeout)
        {
            phase = RunPhase.Searching;
            phaseStartedAt = now;
            detail = $"No kill credit was recorded yet; searching again for {activeTarget!.TargetName}.";
        }
    }

    private bool StartTravelToCurrentLocation(out string message)
    {
        HuntingLogLocation location = locations[locationIndex];
        Vector3 flat = MapToWorld(location);
        Vector3 destination = navigation.PointOnFloor(flat with { Y = 1024f }, halfExtent: 5f)
                              ?? navigation.NearestPoint(flat)
                              ?? flat;
        NavigationRouteSnapshot route = new(
            Guid.NewGuid(),
            $"Hunting Log: {activeTarget!.TargetName}",
            location.TerritoryId,
            [new NavigationRoutePoint(destination.X, destination.Y, destination.Z)],
            string.Empty,
            "hunting-log",
            true,
            false,
            6f,
            8f,
            0,
            0,
            string.Empty,
            false,
            DateTime.UtcNow);
        SuiteRouteDispatchResult result = travel.Dispatch(
            NavigationSuiteRouteRequest.Create(route, NavigationRoutePlanKind.TravelToStart));
        if (!result.Started)
        {
            message = result.Message;
            return false;
        }

        phase = RunPhase.Travelling;
        phaseStartedAt = DateTimeOffset.UtcNow;
        detail = $"Travelling to {activeTarget.TargetName} camp {locationIndex + 1}/{locations.Count} through Nexus.";
        message = $"Started one bounded Hunting Log target: {activeTarget.TargetName} ({activeTarget.Killed}/{activeTarget.Required}).";
        return true;
    }

    private void TryNextLocationOrFail(string reason)
    {
        CleanupActiveMovementAndCombat();
        if (++locationIndex < locations.Count && StartTravelToCurrentLocation(out _))
            return;
        Fail(reason);
    }

    private void Fail(string reason)
    {
        CleanupProviders(restorePreset: true);
        ResetToIdle(reason, didFail: true);
    }

    private void ResetToIdle(string message, bool didFail = false)
    {
        phase = RunPhase.Idle;
        failed = didFail;
        complete = false;
        detail = message;
        engagedObjectId = 0;
        activeTarget = null;
        locations = [];
        locationIndex = 0;
    }

    private IBattleNpc? FindNearestTarget()
    {
        if (objectTable.LocalPlayer is not { } player || activeTarget is null)
            return null;
        return objectTable.OfType<IBattleNpc>()
            .Where(candidate => candidate.NameId == activeTarget.NameId && candidate.IsTargetable && !candidate.IsDead)
            .Where(candidate => Vector3.Distance(player.Position, candidate.Position) <= 60f)
            .OrderBy(candidate => Vector3.DistanceSquared(player.Position, candidate.Position))
            .FirstOrDefault();
    }

    private HuntingLogTargetProgress? FindProgress(ProgressionHuntingTargetCandidate target) =>
        atlas.HuntingTargets.FirstOrDefault(candidate =>
            candidate.LogKey == target.LogKey &&
            candidate.Rank == target.Rank &&
            candidate.TaskIndex == target.TaskIndex &&
            candidate.MonsterIndex == target.MonsterIndex);

    private bool IsProviderReady(out string reason)
    {
        try
        {
            if (!dependencies.FindPlugin("vnavmesh").IsLoaded || !navigation.IsAvailable)
            {
                reason = "Hunting Log requires stock vnavmesh.";
                return false;
            }
            if (!dependencies.FindPlugin("Lifestream").IsLoaded)
            {
                reason = "Hunting Log requires stock Lifestream for cross-zone targets.";
                return false;
            }
            if (!dependencies.FindPlugin("BossMod").IsLoaded || !getPreset.HasFunction || !getActivePreset.HasFunction ||
                !createPreset.HasFunction || !setPreset.HasFunction || !clearPreset.HasFunction)
            {
                reason = "Hunting Log requires the stock Boss Mod preset contract.";
                return false;
            }
            reason = string.Empty;
            return true;
        }
        catch (Exception)
        {
            reason = "A required stock Hunting Log provider IPC contract is unavailable.";
            return false;
        }
    }

    private void CaptureBossModState()
    {
        previousPreset = getActivePreset.InvokeFunc();
        if (string.Equals(previousPreset, PresetName, StringComparison.Ordinal))
            previousPreset = null;
        previousClearOnCombatEnd = ReadBossBoolean("Autorotation", "ClearPresetOnCombatEnd");
        previousQuestBattles = ReadBossBoolean("ZoneModuleConfig", "EnableQuestBattles");
        commandManager.ProcessCommand("/vbmai off");
        commandManager.ProcessCommand("/vbm cfg Autorotation ClearPresetOnCombatEnd false");
        commandManager.ProcessCommand("/vbm cfg ZoneModuleConfig EnableQuestBattles true");
    }

    private void EnableBossModPreset()
    {
        bool withMovement = RequiresBossModMovement(playerState.ClassJob.RowId);
        createPreset.InvokeFunc(BuildPreset(withMovement), true);
        if (!setPreset.InvokeFunc(PresetName))
            throw new InvalidOperationException("Boss Mod rejected the Nexus Hunting Log preset.");
    }

    private void DisableBossModPreset(bool restorePrevious)
    {
        try
        {
            string? active = getActivePreset.HasFunction ? getActivePreset.InvokeFunc() : null;
            if (string.Equals(active, PresetName, StringComparison.Ordinal))
                clearPreset.InvokeFunc();
            if (restorePrevious && !string.IsNullOrWhiteSpace(previousPreset) &&
                getPreset.InvokeFunc(previousPreset) is not null)
                setPreset.InvokeFunc(previousPreset);
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "Boss Mod preset cleanup was unavailable while stopping Hunting Log work.");
        }
    }

    private void CleanupActiveMovementAndCombat()
    {
        travel.Stop();
        try
        {
            navigation.RequestStop();
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "vnavmesh was unavailable while stopping Hunting Log movement.");
        }
        DisableBossModPreset(restorePrevious: false);
        if (targetManager.Target?.GameObjectId == engagedObjectId)
            targetManager.Target = null;
        engagedObjectId = 0;
    }

    private void CleanupProviders(bool restorePreset)
    {
        CleanupActiveMovementAndCombat();
        if (restorePreset)
        {
            DisableBossModPreset(restorePrevious: true);
            RestoreBossBoolean("Autorotation", "ClearPresetOnCombatEnd", previousClearOnCombatEnd);
            RestoreBossBoolean("ZoneModuleConfig", "EnableQuestBattles", previousQuestBattles);
        }
        previousPreset = null;
        previousClearOnCombatEnd = null;
        previousQuestBattles = null;
    }

    private bool? ReadBossBoolean(string section, string setting)
    {
        if (!bossConfiguration.HasFunction)
            return null;
        try
        {
            IReadOnlyList<string> values = bossConfiguration.InvokeFunc(["cfg", section, setting], false);
            return values.Count > 0 && bool.TryParse(values[0], out bool value) ? value : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void RestoreBossBoolean(string section, string setting, bool? value)
    {
        if (value is not { } restore)
            return;
        commandManager.ProcessCommand($"/vbm cfg {section} {setting} {restore.ToString().ToLowerInvariant()}");
    }

    private static string BuildPreset(bool withMovement)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PresetResource)
                              ?? throw new InvalidDataException($"Missing Nexus Boss Mod preset {PresetResource}.");
        JsonObject preset = JsonNode.Parse(stream)?.AsObject()
                            ?? throw new InvalidDataException("The Nexus Boss Mod preset is invalid.");
        preset["Name"] = PresetName;
        if (!withMovement)
            preset["Modules"]?.AsObject().Remove(MovementModule);
        return preset.ToJsonString();
    }

    private static Vector3 MapToWorld(HuntingLogLocation location)
    {
        MapLinkPayload link = new(location.TerritoryId, location.MapId, location.MapX, location.MapY);
        return new Vector3(link.RawX / 1000f, 0f, link.RawY / 1000f);
    }

    private static int RequiredLevel(HuntingLogTargetProgress target) => target.LogKey >= 10_000
        ? 20 + target.Rank * 10
        : target.Rank == 0 ? 1 : target.Rank * 10;

    private static int RequiredGrandCompanyRank(int rank) => rank switch
    {
        0 => 1,
        1 => 5,
        2 => 9,
        _ => int.MaxValue,
    };

    private static unsafe int CurrentGrandCompanyRank()
    {
        PlayerState* state = PlayerState.Instance();
        return state is null ? 0 : state->GetGrandCompanyRank();
    }

    private static bool RequiresBossModMovement(uint classJobId) => classJobId is
        1 or 2 or 3 or 4 or 19 or 20 or 21 or 22 or 29 or 30 or 32 or 34 or 37 or 39 or 41 or 43;

    private static ProgressionHuntingTargetCandidate ToCandidate(HuntingLogTargetProgress target) => new(
        target.LogKey,
        target.LogName,
        target.Rank,
        target.TaskIndex,
        target.MonsterIndex,
        target.NameId,
        target.TargetName,
        target.Killed,
        target.Required,
        target.Locations);

    private enum RunPhase
    {
        Idle,
        Travelling,
        Searching,
        Approaching,
        Engaging,
        WaitingForCredit,
    }
}
