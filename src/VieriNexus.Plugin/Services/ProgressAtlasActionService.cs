using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal enum ProgressAtlasActionOutcome
{
    None,
    Active,
    Completed,
    Stopped,
    Failed,
}

internal enum ProgressAtlasActionKind
{
    Aetheryte,
    FieldAetherCurrent,
    AetherCurrentQuest,
    Exploration,
    Achievement,
}

internal sealed record ProgressAtlasActionStatus(
    Guid OperationId,
    bool IsActive,
    ProgressAtlasActionKind? Kind,
    ProgressAtlasActionOutcome Outcome,
    string Title,
    string Message);

/// <summary>
/// Nexus-owned one-objective Progress Atlas executor. It selects and verifies the exact objective;
/// the shared Nexus route provider uses stock Lifestream and vnavmesh only for travel/pathing.
/// </summary>
internal sealed class ProgressAtlasActionService
{
    private static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan QuestOverallTimeout = TimeSpan.FromHours(1);
    private static readonly TimeSpan InteractionTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ProviderStartTimeout = TimeSpan.FromSeconds(45);
    private static readonly ResourceKind[] QuestResources =
    [
        ResourceKind.Teleport,
        ResourceKind.Movement,
        ResourceKind.Navigation,
        ResourceKind.Targeting,
        ResourceKind.Combat,
        ResourceKind.Rotation,
        ResourceKind.UiInteraction,
    ];

    private readonly ProgressAtlasService atlas;
    private readonly ProgressionProviderService questProvider;
    private readonly NexusRouteTravelProvider travel;
    private readonly ResourceLeaseManager leases;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private ResourceLeaseHandle? lease;
    private Objective? objective;
    private Phase phase;
    private DateTimeOffset startedAt;
    private DateTimeOffset phaseStartedAt;
    private DateTimeOffset nextInteractionAt;
    private bool providerStartObserved;
    private string? pendingStopReason;
    private ProgressAtlasActionOutcome pendingStopOutcome;
    private Guid operationId;
    private ProgressAtlasActionKind? actionKind;
    private ProgressAtlasActionOutcome outcome;
    private string message = "No Progress Atlas action is running.";

    internal ProgressAtlasActionService(
        ProgressAtlasService atlas,
        ProgressionProviderService questProvider,
        NexusRouteTravelProvider travel,
        ResourceLeaseManager leases,
        IClientState clientState,
        ICondition condition,
        IObjectTable objectTable,
        ITargetManager targetManager)
    {
        this.atlas = atlas;
        this.questProvider = questProvider;
        this.travel = travel;
        this.leases = leases;
        this.clientState = clientState;
        this.condition = condition;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
    }

    internal ProgressAtlasActionStatus Status => new(
        operationId,
        phase != Phase.Idle,
        actionKind,
        outcome,
        objective?.Title ?? "Progress Atlas",
        message);

    internal bool IsQuestExecutionActive => phase == Phase.Questing;

    internal int RemainingAetherytes => atlas.AetheryteTargets.Count(target =>
        !ProgressAtlasService.IsAetheryteUnlocked(target.Id) && IsTerritoryAccessible(target.TerritoryId));

    internal int RemainingFieldCurrents => atlas.AetherCurrentTargets.Count(target =>
        !ProgressAtlasService.IsAetherCurrentUnlocked(target.AetherCurrentId) && IsTerritoryAccessible(target.TerritoryId));

    internal int ReadyAetherCurrentQuests => !clientState.IsLoggedIn || objectTable.LocalPlayer is null
        ? 0
        : questProvider.EligibleAetherCurrentQuests(atlas.AetherCurrentQuestTargets, objectTable.LocalPlayer.Level).Count;

    internal int RemainingAetherCurrentQuests => atlas.AetherCurrentQuestTargets.Count(target =>
        !ProgressAtlasService.IsAetherCurrentUnlocked(target.AetherCurrentId));

    internal int RemainingExplorationRegions => atlas.ExplorationTargets.Count(target =>
        !ProgressAtlasService.IsExplorationComplete(target.MapId, target.DiscoveryId) &&
        target.Positions.Count > 0 && IsTerritoryAccessible(target.TerritoryId));

    internal int RemainingSupportedAchievements => atlas.AchievementTargets.Count(target =>
        !ProgressAtlasService.IsAchievementComplete(target.Id) && HasRunnableAchievementStep(target));

    internal bool CanReachTerritory(uint territoryId) => IsTerritoryAccessible(territoryId);

    internal bool StartNextAetheryte(out string result)
    {
        ProgressAtlasService.AetheryteAtlasTarget? target = atlas.AetheryteTargets
            .Where(candidate => !ProgressAtlasService.IsAetheryteUnlocked(candidate.Id))
            .Where(candidate => IsTerritoryAccessible(candidate.TerritoryId))
            .OrderByDescending(candidate => candidate.TerritoryId == clientState.TerritoryType)
            .ThenBy(candidate => DistanceFromPlayer(candidate.TerritoryId, candidate.Position))
            .ThenBy(candidate => candidate.TerritoryId)
            .ThenBy(candidate => candidate.Id)
            .FirstOrDefault();
        if (target is null)
        {
            result = "No reachable locked Aetheryte or Aethernet shard is available.";
            return false;
        }

        return StartAetheryte(target, out result);
    }

    internal bool StartAetheryte(ProgressAtlasService.AetheryteAtlasTarget target, out string result)
    {
        if (ProgressAtlasService.IsAetheryteUnlocked(target.Id))
        {
            result = $"{target.Name} is already attuned.";
            return false;
        }
        if (!IsTerritoryAccessible(target.TerritoryId))
        {
            result = $"{target.Name} is not reachable with the character's currently unlocked travel network.";
            return false;
        }
        return Start(new Objective(
            AtlasActionKind.Aetheryte,
            target.IsShard ? $"Attune {target.Name}" : $"Attune {target.Name} Aetheryte",
            target.TerritoryId,
            target.Position,
            target.Id,
            target.Id,
            0,
            0,
            target.IsShard ? 2.5f : 9f), out result);
    }

    internal bool StartNextFieldCurrent(out string result)
    {
        ProgressAtlasService.AetherCurrentAtlasTarget? target = atlas.AetherCurrentTargets
            .Where(candidate => !ProgressAtlasService.IsAetherCurrentUnlocked(candidate.AetherCurrentId))
            .Where(candidate => IsTerritoryAccessible(candidate.TerritoryId))
            .OrderByDescending(candidate => candidate.TerritoryId == clientState.TerritoryType)
            .ThenBy(candidate => DistanceFromPlayer(candidate.TerritoryId, candidate.Position))
            .ThenBy(candidate => candidate.TerritoryId)
            .ThenBy(candidate => candidate.AetherCurrentId)
            .FirstOrDefault();
        if (target is null)
        {
            result = "No reachable uncollected field Aether Current is available.";
            return false;
        }

        return StartFieldCurrent(target, out result);
    }

    internal bool StartFieldCurrent(ProgressAtlasService.AetherCurrentAtlasTarget target, out string result)
    {
        if (ProgressAtlasService.IsAetherCurrentUnlocked(target.AetherCurrentId))
        {
            result = "That Aether Current is already collected.";
            return false;
        }
        if (!IsTerritoryAccessible(target.TerritoryId))
        {
            result = $"{target.TerritoryName} is not reachable with the character's currently unlocked travel network.";
            return false;
        }
        return Start(new Objective(
            AtlasActionKind.FieldAetherCurrent,
            $"Collect field Aether Current in {target.TerritoryName}",
            target.TerritoryId,
            target.Position,
            target.DataId,
            target.AetherCurrentId,
            0,
            0,
            2.5f), out result);
    }

    internal bool StartNextAetherCurrentQuest(out string result)
    {
        if (!clientState.IsLoggedIn || objectTable.LocalPlayer is null)
        {
            result = "Log into a character before starting an Aether Current quest.";
            return false;
        }

        ProgressionQuestCandidate? quest = questProvider
            .EligibleAetherCurrentQuests(atlas.AetherCurrentQuestTargets, objectTable.LocalPlayer.Level)
            .FirstOrDefault();
        if (quest is null)
        {
            result = "No accepted or currently unlockable Aether Current quest has a supported provider path.";
            return false;
        }

        return StartQuest(quest, out result);
    }

    internal bool StartAetherCurrentQuest(ProgressAtlasService.AetherCurrentQuestAtlasTarget target, out string result)
    {
        if (ProgressAtlasService.IsAetherCurrentUnlocked(target.AetherCurrentId))
        {
            result = $"{target.QuestName} is already complete.";
            return false;
        }
        ProgressionQuestCandidate? quest = questProvider
            .EligibleAetherCurrentQuests([target], objectTable.LocalPlayer?.Level ?? 0)
            .FirstOrDefault();
        if (quest is null)
        {
            result = $"{target.QuestName} is not accepted or currently unlockable with a supported stock Questionable path.";
            return false;
        }
        return StartQuest(quest, out result);
    }

    internal bool StartNextExploration(out string result)
    {
        var candidates = atlas.ExplorationTargets
            .Where(candidate => !ProgressAtlasService.IsExplorationComplete(candidate.MapId, candidate.DiscoveryId))
            .Where(candidate => candidate.Positions.Count > 0 && IsTerritoryAccessible(candidate.TerritoryId))
            .SelectMany(candidate => candidate.Positions.Select(position => (Target: candidate, Position: position)))
            .OrderByDescending(candidate => candidate.Target.TerritoryId == clientState.TerritoryType)
            .ThenBy(candidate => DistanceFromPlayer(candidate.Target.TerritoryId, candidate.Position))
            .ThenBy(candidate => candidate.Target.TerritoryId)
            .ThenBy(candidate => candidate.Target.MapId)
            .ThenBy(candidate => candidate.Target.DiscoveryId)
            .FirstOrDefault();
        if (candidates.Target is null)
        {
            result = "No reachable unexplored Mapping/Remapping region is available.";
            return false;
        }

        return StartExploration(candidates.Target, candidates.Position, out result);
    }

    internal bool StartExploration(ProgressAtlasService.MapDiscoveryRegion target, Vector3 position, out string result)
    {
        if (ProgressAtlasService.IsExplorationComplete(target.MapId, target.DiscoveryId))
        {
            result = $"{target.Name} is already discovered.";
            return false;
        }
        if (!IsTerritoryAccessible(target.TerritoryId))
        {
            result = $"{target.TerritoryName} is not reachable with the character's currently unlocked travel network.";
            return false;
        }
        return Start(new Objective(
            AtlasActionKind.Exploration,
            $"Explore {target.Name}",
            target.TerritoryId,
            position,
            0,
            0,
            target.MapId,
            target.DiscoveryId,
            3.5f), out result);
    }

    internal bool StartNextSupportedAchievement(out string result)
    {
        foreach (ProgressAtlasService.AchievementAtlasTarget achievement in atlas.AchievementTargets
                     .Where(target => !ProgressAtlasService.IsAchievementComplete(target.Id))
                     .OrderBy(target => target.AutomationPriority)
                     .ThenBy(target => target.Id))
        {
            if (achievement.Type == 8)
            {
                ProgressAtlasService.MapDiscoveryRegion? region = atlas.ExplorationTargets
                    .Where(target => target.MapId == achievement.Key)
                    .Where(target => !ProgressAtlasService.IsExplorationComplete(target.MapId, target.DiscoveryId))
                    .Where(target => target.Positions.Count > 0 && IsTerritoryAccessible(target.TerritoryId))
                    .OrderByDescending(target => target.TerritoryId == clientState.TerritoryType)
                    .ThenBy(target => target.DiscoveryId)
                    .FirstOrDefault();
                if (region is not null)
                {
                    Vector3 position = region.Positions
                        .OrderBy(point => DistanceFromPlayer(region.TerritoryId, point))
                        .First();
                    return StartAchievement(
                        achievement,
                        new Objective(
                            AtlasActionKind.Exploration,
                            $"Pursue {achievement.Name}",
                            region.TerritoryId,
                            position,
                            0,
                            0,
                            region.MapId,
                            region.DiscoveryId,
                            3.5f),
                        out result);
                }
            }
            else if (achievement.Type == 20 && achievement.TerritoryId > 0)
            {
                ProgressAtlasService.AetherCurrentAtlasTarget? field = atlas.AetherCurrentTargets
                    .Where(target => target.TerritoryId == achievement.TerritoryId)
                    .Where(target => !ProgressAtlasService.IsAetherCurrentUnlocked(target.AetherCurrentId))
                    .Where(target => IsTerritoryAccessible(target.TerritoryId))
                    .OrderBy(target => DistanceFromPlayer(target.TerritoryId, target.Position))
                    .ThenBy(target => target.AetherCurrentId)
                    .FirstOrDefault();
                if (field is not null)
                {
                    return StartAchievement(
                        achievement,
                        new Objective(
                            AtlasActionKind.FieldAetherCurrent,
                            $"Pursue {achievement.Name}",
                            field.TerritoryId,
                            field.Position,
                            field.DataId,
                            field.AetherCurrentId,
                            0,
                            0,
                            2.5f),
                        out result);
                }

                ProgressionQuestCandidate? quest = questProvider
                    .EligibleAetherCurrentQuests(
                        atlas.AetherCurrentQuestTargets,
                        objectTable.LocalPlayer?.Level ?? 0)
                    .Where(candidate => candidate.TerritoryId == achievement.TerritoryId)
                    .FirstOrDefault();
                if (quest is not null)
                    return StartAchievementQuest(achievement, quest, out result);
            }
        }

        result = "No directly runnable incomplete achievement is available. Manual, group, crafting, gathering, PvP, collection, and time-gated achievements remain tracked in Progress Atlas.";
        return false;
    }

    internal bool Stop(out string result)
    {
        if (phase == Phase.QuestStopping)
        {
            result = "Stop is already requested; Nexus is waiting for the quest provider to confirm inactivity.";
            message = result;
            return true;
        }
        if (phase == Phase.Questing)
        {
            if (!questProvider.TryStopQuest(out result))
            {
                message = $"Stop has not been confirmed: {result}";
                return false;
            }
            pendingStopReason = "Progress Atlas action stopped. Nothing will replay automatically.";
            pendingStopOutcome = ProgressAtlasActionOutcome.Stopped;
            phase = Phase.QuestStopping;
            message = "Stop requested; Nexus is retaining ownership until the quest provider confirms it is inactive.";
            result = message;
            return true;
        }
        else
            travel.Stop();
        ReleaseLease();
        if (objective?.DataId is > 0 && targetManager.Target?.BaseId == objective.DataId)
            targetManager.Target = null;
        objective = null;
        phase = Phase.Idle;
        providerStartObserved = false;
        pendingStopReason = null;
        pendingStopOutcome = ProgressAtlasActionOutcome.None;
        outcome = ProgressAtlasActionOutcome.Stopped;
        message = "Progress Atlas action stopped. Nothing will replay automatically.";
        result = message;
        return true;
    }

    internal void Update(DateTimeOffset now)
    {
        if (phase == Phase.Idle || objective is null)
            return;
        if (phase == Phase.QuestStopping)
        {
            UpdateQuestStopping();
            return;
        }
        TimeSpan timeout = phase == Phase.Questing ? QuestOverallTimeout : OverallTimeout;
        if (now - startedAt >= timeout)
        {
            Fail(phase == Phase.Questing
                ? "The Aether Current quest exceeded its one-hour safety limit."
                : "The Progress Atlas action exceeded its 20-minute safety limit.");
            return;
        }
        if (lease is null || !lease.Heartbeat(LeaseLifetime))
        {
            Fail("Progress Atlas ownership expired; Nexus stopped the action.");
            return;
        }
        if (phase != Phase.Questing && condition[ConditionFlag.InCombat])
        {
            Fail("Combat began during the Progress Atlas action; Nexus stopped travel and interaction.");
            return;
        }
        if (phase != Phase.Questing && IsComplete(objective))
        {
            Complete();
            return;
        }

        try
        {
            if (phase == Phase.Travelling)
                UpdateTravel(now);
            else if (phase == Phase.Interacting)
                UpdateInteraction(now);
            else if (phase == Phase.Questing)
                UpdateQuest();
            else
                UpdateVerification(now);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Progress Atlas action failed during {Phase}.", phase);
            Fail("A stock travel or game interaction operation failed; Nexus stopped the action.");
        }
    }

    internal void Shutdown()
    {
        if (phase != Phase.Idle)
            Stop(out _);
    }

    private bool Start(Objective selected, out string result)
    {
        if (phase != Phase.Idle)
        {
            result = "Stop the current Progress Atlas action before starting another one.";
            return false;
        }
        if (!clientState.IsLoggedIn || objectTable.LocalPlayer is null)
        {
            result = "Log into a character before starting a Progress Atlas action.";
            return false;
        }
        if (condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty] ||
            condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95])
        {
            result = "Progress Atlas actions cannot start during combat or a duty.";
            return false;
        }

        LeaseOwner owner = new(GoalId.New(), TaskId.New(), AttemptId.New(), 60, $"Progress Atlas: {selected.Title}");
        ResourceKind[] resources = selected.Kind == AtlasActionKind.Exploration
            ? [ResourceKind.Teleport]
            : [ResourceKind.Teleport, ResourceKind.UiInteraction, ResourceKind.Targeting];
        if (!leases.TryAcquire(owner, resources, LeaseLifetime, out lease, out ResourceLeaseSnapshot? blocking))
        {
            result = blocking is null
                ? "Another Nexus action owns a required resource."
                : $"Wait for {blocking.Owner.Reason} to finish.";
            return false;
        }

        objective = selected;
        operationId = Guid.NewGuid();
        actionKind = selected.Kind switch
        {
            AtlasActionKind.Aetheryte => ProgressAtlasActionKind.Aetheryte,
            AtlasActionKind.FieldAetherCurrent => ProgressAtlasActionKind.FieldAetherCurrent,
            AtlasActionKind.AetherCurrentQuest => ProgressAtlasActionKind.AetherCurrentQuest,
            _ => ProgressAtlasActionKind.Exploration,
        };
        outcome = ProgressAtlasActionOutcome.Active;
        startedAt = DateTimeOffset.UtcNow;
        NavigationRouteSnapshot route = new(
            Guid.NewGuid(), selected.Title, selected.TerritoryId,
            [new NavigationRoutePoint(selected.Position.X, selected.Position.Y, selected.Position.Z)],
            string.Empty, "progress-atlas", true, true, selected.Tolerance, selected.Tolerance,
            0, 0, string.Empty, false, DateTime.UtcNow);
        SuiteRouteDispatchResult dispatch = travel.Dispatch(
            NavigationSuiteRouteRequest.Create(route, NavigationRoutePlanKind.TravelToStart));
        if (!dispatch.Started)
        {
            ReleaseLease();
            objective = null;
            outcome = ProgressAtlasActionOutcome.Failed;
            result = dispatch.Message;
            return false;
        }

        phase = Phase.Travelling;
        phaseStartedAt = startedAt;
        message = $"Nexus started one exact Atlas objective: {selected.Title}.";
        result = message;
        return true;
    }

    private bool StartQuest(ProgressionQuestCandidate quest, out string result)
    {
        if (phase != Phase.Idle)
        {
            result = "Stop the current Progress Atlas action before starting another one.";
            return false;
        }
        if (condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty] ||
            condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95])
        {
            result = "Aether Current quests cannot start during combat or a duty.";
            return false;
        }

        string title = $"Complete {quest.Name}";
        LeaseOwner owner = new(GoalId.New(), TaskId.New(), AttemptId.New(), 60, $"Progress Atlas: {title}");
        if (!leases.TryAcquire(owner, QuestResources, LeaseLifetime, out lease, out ResourceLeaseSnapshot? blocking))
        {
            result = blocking is null
                ? "Another Nexus action owns a required resource."
                : $"Wait for {blocking.Owner.Reason} to finish.";
            return false;
        }

        objective = new Objective(
            AtlasActionKind.AetherCurrentQuest,
            title,
            quest.TerritoryId,
            Vector3.Zero,
            0,
            quest.AetherCurrentId,
            0,
            0,
            0,
            quest);
        operationId = Guid.NewGuid();
        actionKind = ProgressAtlasActionKind.AetherCurrentQuest;
        outcome = ProgressAtlasActionOutcome.Active;
        ProgressionQuestStartResult start = questProvider.TryStartQuest(quest);
        if (!start.Success)
        {
            ReleaseLease();
            objective = null;
            outcome = ProgressAtlasActionOutcome.Failed;
            result = start.Message;
            return false;
        }

        startedAt = DateTimeOffset.UtcNow;
        phaseStartedAt = startedAt;
        providerStartObserved = false;
        phase = Phase.Questing;
        message = start.Message;
        result = message;
        return true;
    }

    private void UpdateQuest()
    {
        ProgressionQuestCandidate quest = objective!.Quest
            ?? throw new InvalidDataException("The active Atlas quest is missing its exact quest identity.");
        ProgressionQuestProviderObservation observation = questProvider.ObserveQuest(quest.QuestId);
        if (!observation.IsAvailable || observation.IsRunning is null)
        {
            Fail("The quest provider became unavailable; Nexus requested Stop and released Atlas ownership.");
            return;
        }

        bool matching = string.Equals(observation.CurrentQuestId, quest.QuestId, StringComparison.Ordinal);
        providerStartObserved |= observation.IsRunning == true && matching;
        bool currentUnlocked = ProgressAtlasService.IsAetherCurrentUnlocked(quest.AetherCurrentId);
        if ((observation.IsComplete == true || currentUnlocked) && observation.IsRunning == false)
        {
            Complete();
            return;
        }
        if (observation.IsRunning == true && !matching)
        {
            Fail("The quest provider switched to different work; Nexus requested Stop instead of surrendering ownership.");
            return;
        }
        if (observation.IsRunning == true)
        {
            message = $"{quest.Name} is running; Nexus is waiting for exact quest and Aether Current confirmation.";
            return;
        }
        if (providerStartObserved)
        {
            Fail($"{quest.Name} stopped before its Aether Current was verified.");
            return;
        }
        if (DateTimeOffset.UtcNow - phaseStartedAt >= ProviderStartTimeout)
            Fail($"{quest.Name} did not start within 45 seconds.");
    }

    private void UpdateQuestStopping()
    {
        ProgressionQuestCandidate quest = objective!.Quest
            ?? throw new InvalidDataException("The stopping Atlas quest is missing its exact quest identity.");
        ProgressionQuestProviderObservation observation = questProvider.ObserveQuest(quest.QuestId);
        if (!observation.IsAvailable || observation.IsRunning is null)
        {
            message = "Stop was requested; Nexus is retaining ownership until the quest provider becomes observable and inactive.";
            return;
        }
        if (observation.IsRunning == true)
        {
            message = "Stop was requested; waiting for the quest provider to become inactive.";
            return;
        }

        string stopped = pendingStopReason ?? "Progress Atlas quest stopped.";
        ReleaseLease();
        objective = null;
        phase = Phase.Idle;
        providerStartObserved = false;
        pendingStopReason = null;
        outcome = pendingStopOutcome == ProgressAtlasActionOutcome.None
            ? ProgressAtlasActionOutcome.Stopped
            : pendingStopOutcome;
        pendingStopOutcome = ProgressAtlasActionOutcome.None;
        message = stopped;
    }

    private void UpdateTravel(DateTimeOffset now)
    {
        SuiteRouteProviderObservation observation = travel.Observe(now);
        if (observation.State == SuiteRouteProviderState.Failed)
        {
            Fail(observation.Message);
            return;
        }
        if (observation.State != SuiteRouteProviderState.Completed)
        {
            message = observation.Message;
            return;
        }
        if (objective!.Kind == AtlasActionKind.Exploration)
        {
            phase = Phase.Verifying;
            phaseStartedAt = now;
            message = $"Nexus reached {objective.Title}; waiting for exact map-discovery confirmation.";
            return;
        }

        phase = Phase.Interacting;
        phaseStartedAt = now;
        nextInteractionAt = now;
        message = $"Nexus reached {objective.Title} and is verifying the attunement object.";
    }

    private unsafe void UpdateInteraction(DateTimeOffset now)
    {
        if (now - phaseStartedAt >= InteractionTimeout)
        {
            Fail($"Nexus could not verify {objective!.Title} within 45 seconds.");
            return;
        }
        IGameObject? target = objectTable
            .Where(candidate => candidate.BaseId == objective!.DataId && candidate.IsTargetable)
            .OrderBy(candidate => objectTable.LocalPlayer is { } player
                ? Vector3.DistanceSquared(player.Position, candidate.Position)
                : float.MaxValue)
            .FirstOrDefault();
        if (target is null)
        {
            message = $"Waiting for the {objective!.Title} object to become available.";
            return;
        }

        if (condition[ConditionFlag.Mounted])
        {
            if (now >= nextInteractionAt)
            {
                ActionManager* actions = ActionManager.Instance();
                if (actions is not null)
                    actions->UseAction(ActionType.GeneralAction, 23);
                nextInteractionAt = now.AddSeconds(1);
            }
            message = $"Nexus reached {objective!.Title} and is dismounting before attunement.";
            return;
        }

        targetManager.Target = target;
        if (now < nextInteractionAt)
            return;
        nextInteractionAt = now.AddSeconds(2);
        TargetSystem* targets = TargetSystem.Instance();
        if (targets is not null)
            targets->InteractWithObject((GameObject*)target.Address, false);
        message = $"Attuning {objective!.Title}; waiting for exact game-state confirmation.";
    }

    private void UpdateVerification(DateTimeOffset now)
    {
        if (now - phaseStartedAt >= TimeSpan.FromSeconds(10))
            Fail($"Nexus reached {objective!.Title}, but the game did not record map discovery.");
    }

    private bool IsTerritoryAccessible(uint territoryId) =>
        territoryId == clientState.TerritoryType ||
        atlas.AetheryteTargets.Any(target =>
            target.TerritoryId == territoryId && target.IsShard == false &&
            ProgressAtlasService.IsAetheryteUnlocked(target.Id));

    private float DistanceFromPlayer(uint territoryId, Vector3 position) =>
        territoryId == clientState.TerritoryType && objectTable.LocalPlayer is { } player
            ? Vector3.DistanceSquared(player.Position, position)
            : float.MaxValue;

    private static bool IsComplete(Objective value) => value.Kind switch
    {
        AtlasActionKind.Aetheryte => ProgressAtlasService.IsAetheryteUnlocked(value.CompletionId),
        AtlasActionKind.FieldAetherCurrent => ProgressAtlasService.IsAetherCurrentUnlocked(value.CompletionId),
        AtlasActionKind.AetherCurrentQuest => ProgressAtlasService.IsAetherCurrentUnlocked(value.CompletionId),
        AtlasActionKind.Exploration => ProgressAtlasService.IsExplorationComplete(value.MapId, value.DiscoveryId),
        _ => false,
    };

    private void Complete()
    {
        string completed = objective!.Title;
        if (objective.DataId > 0 && targetManager.Target?.BaseId == objective.DataId)
            targetManager.Target = null;
        ReleaseLease();
        objective = null;
        phase = Phase.Idle;
        providerStartObserved = false;
        pendingStopReason = null;
        pendingStopOutcome = ProgressAtlasActionOutcome.None;
        outcome = ProgressAtlasActionOutcome.Completed;
        message = $"Verified complete: {completed}.";
    }

    private void Fail(string reason)
    {
        if (phase == Phase.Questing)
        {
            if (!questProvider.TryStopQuest(out string stopMessage))
            {
                message = $"{reason} Stop is not yet confirmed: {stopMessage}";
                return;
            }
            pendingStopReason = reason;
            pendingStopOutcome = ProgressAtlasActionOutcome.Failed;
            phase = Phase.QuestStopping;
            message = $"{reason} Stop was requested; Nexus is retaining ownership until inactivity is confirmed.";
            return;
        }
        else
            travel.Stop();
        ReleaseLease();
        objective = null;
        phase = Phase.Idle;
        providerStartObserved = false;
        pendingStopReason = null;
        pendingStopOutcome = ProgressAtlasActionOutcome.None;
        outcome = ProgressAtlasActionOutcome.Failed;
        message = reason;
    }

    private bool StartAchievement(
        ProgressAtlasService.AchievementAtlasTarget achievement,
        Objective selected,
        out string result)
    {
        bool started = Start(selected, out result);
        if (!started)
            return false;
        actionKind = ProgressAtlasActionKind.Achievement;
        message = $"Nexus started the next exact step for {achievement.Name}.";
        result = message;
        return true;
    }

    private bool StartAchievementQuest(
        ProgressAtlasService.AchievementAtlasTarget achievement,
        ProgressionQuestCandidate quest,
        out string result)
    {
        bool started = StartQuest(quest, out result);
        if (!started)
            return false;
        actionKind = ProgressAtlasActionKind.Achievement;
        message = $"Nexus asked Questionable to complete the next exact quest step for {achievement.Name}.";
        result = message;
        return true;
    }

    private bool HasRunnableAchievementStep(ProgressAtlasService.AchievementAtlasTarget achievement)
    {
        if (achievement.Type == 8)
        {
            return atlas.ExplorationTargets.Any(target =>
                target.MapId == achievement.Key &&
                !ProgressAtlasService.IsExplorationComplete(target.MapId, target.DiscoveryId) &&
                target.Positions.Count > 0 && IsTerritoryAccessible(target.TerritoryId));
        }
        if (achievement.Type != 20 || achievement.TerritoryId == 0)
            return false;
        if (atlas.AetherCurrentTargets.Any(target =>
                target.TerritoryId == achievement.TerritoryId &&
                !ProgressAtlasService.IsAetherCurrentUnlocked(target.AetherCurrentId) &&
                IsTerritoryAccessible(target.TerritoryId)))
            return true;
        return questProvider.EligibleAetherCurrentQuests(
                atlas.AetherCurrentQuestTargets,
                objectTable.LocalPlayer?.Level ?? 0)
            .Any(candidate => candidate.TerritoryId == achievement.TerritoryId);
    }

    private void ReleaseLease()
    {
        lease?.Dispose();
        lease = null;
    }

    private sealed record Objective(
        AtlasActionKind Kind,
        string Title,
        uint TerritoryId,
        Vector3 Position,
        uint DataId,
        uint CompletionId,
        uint MapId,
        byte DiscoveryId,
        float Tolerance,
        ProgressionQuestCandidate? Quest = null);

    private enum AtlasActionKind
    {
        Aetheryte,
        FieldAetherCurrent,
        AetherCurrentQuest,
        Exploration,
    }

    private enum Phase
    {
        Idle,
        Travelling,
        Interacting,
        Verifying,
        Questing,
        QuestStopping,
    }
}
