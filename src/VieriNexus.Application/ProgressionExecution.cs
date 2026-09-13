using System.Text.Json;
using System.Text.Json.Serialization;
using VieriNexus.Domain;

namespace VieriNexus.Application;

public sealed record ProgressionDutyCandidate(
    uint TerritoryId,
    uint ContentId,
    string Name,
    int RequiredLevel,
    int RequiredItemLevel);

public sealed record ProgressionDutyProviderObservation(
    bool IsAvailable,
    bool? IsStopped,
    string Detail);

public interface IProgressionDutyProvider
{
    ProviderId Id { get; }

    IReadOnlyList<ProgressionDutyCandidate> EligibleDuties(int currentLevel);

    ProgressionDutyProviderObservation Observe();

    bool TryStart(uint territoryId, out string message);

    bool TryStop(out string message);
}

public enum ProgressionQuestKind
{
    ClassJobRole,
    GeneralSideQuest,
    AetherCurrent,
    MainScenario,
}

public sealed record ProgressionQuestCandidate(
    string QuestId,
    string Name,
    int RequiredLevel,
    bool IsAccepted,
    ProgressionQuestKind Kind = ProgressionQuestKind.ClassJobRole,
    uint TerritoryId = 0,
    uint AetherCurrentId = 0);

public sealed record ProgressionQuestStartResult(
    bool Success,
    bool UnsupportedQuest,
    string Message);

public sealed record ProgressionQuestProviderObservation(
    bool IsAvailable,
    bool? IsRunning,
    string? CurrentQuestId,
    bool? IsComplete,
    string Detail);

public interface IProgressionQuestProvider
{
    ProviderId Id { get; }

    IReadOnlyList<ProgressionQuestCandidate> EligibleQuests(
        uint classJobId,
        int currentLevel,
        bool includeMainScenario,
        bool includeClassJobRole,
        bool includeGeneralSideQuests);

    ProgressionQuestProviderObservation ObserveQuest(string questId);

    ProgressionQuestStartResult TryStartQuest(ProgressionQuestCandidate quest);

    bool TryStopQuest(out string message);
}

public sealed record ProgressionHuntingTargetCandidate(
    uint LogKey,
    string LogName,
    int Rank,
    int TaskIndex,
    int MonsterIndex,
    uint NameId,
    string TargetName,
    int Killed,
    int Required,
    IReadOnlyList<HuntingLogLocation> Locations)
{
    public bool HasOpenWorldLocation => Locations.Any(location => location.IsOpenWorld);
    public bool HasDutyLocation => Locations.Any(location => !location.IsOpenWorld && location.DutyTerritoryId != 0);
    public bool IsDutyOnly => !HasOpenWorldLocation && HasDutyLocation;
}

public sealed record ProgressionHuntingProviderObservation(
    bool IsAvailable,
    bool? IsBusy,
    bool IsComplete,
    bool HasFailed,
    int Killed,
    int Required,
    string Detail);

public interface IProgressionHuntingProvider
{
    ProviderId Id { get; }

    IReadOnlyList<ProgressionHuntingTargetCandidate> EligibleTargets(uint classJobId, int currentLevel);

    ProgressionHuntingProviderObservation ObserveHunt(ProgressionHuntingTargetCandidate target);

    bool TryStartHunt(ProgressionHuntingTargetCandidate target, out string message);

    bool TryStopHunt(out string message);
}

public sealed record ProgressionGearProviderObservation(
    bool IsAvailable,
    bool? IsBusy,
    long StartedSequence,
    long CompletedSequence,
    int StartingItemLevel,
    int EndingItemLevel,
    int ItemsPurchased,
    string Detail);

public interface IProgressionGearProvider
{
    ProviderId Id { get; }

    ProgressionGearProviderObservation ObserveGearReadiness();

    bool TryStartGearReadiness(int minimumGilReserve, out string message);

    bool TryStopGearReadiness(out string message);
}

public sealed record ProgressionMaintenanceProviderObservation(
    bool IsAvailable,
    bool HasConfiguredOperations,
    bool? IsBusy,
    long StartedSequence,
    long CompletedSequence,
    string Detail);

public interface IProgressionMaintenanceProvider
{
    ProviderId Id { get; }

    ProgressionMaintenanceProviderObservation ObserveMaintenance();

    bool TryStartConfiguredMaintenance(out string message);

    bool TryStopMaintenance(out string message);
}

public sealed record ReachJobLevelDesiredState(
    uint ClassJobId,
    int TargetLevel,
    bool AllowJobQuests,
    bool AllowHuntingLog,
    bool AllowSideQuests,
    bool AllowDuties,
    int MinimumGilReserve,
    bool AllowMainScenario = false);

public sealed record ProgressionDutyTaskPayload(
    uint TerritoryId,
    uint ContentId,
    string DutyName,
    int StartingLevel,
    int RequiredLevel,
    int RequiredItemLevel);

public sealed record ProgressionGearTaskPayload(
    int MinimumGilReserve,
    int StartingItemLevel,
    int StartingGil,
    long BaselineStartedSequence,
    long BaselineCompletedSequence);

public sealed record ProgressionMaintenanceTaskPayload(
    int StartingLevel,
    long BaselineStartedSequence,
    long BaselineCompletedSequence);

public sealed record ProgressionQuestTaskPayload(
    string QuestId,
    string QuestName,
    int StartingLevel,
    int RequiredLevel,
    ProgressionQuestKind Kind = ProgressionQuestKind.ClassJobRole);

public sealed record ProgressionHuntingTaskPayload(
    uint LogKey,
    string LogName,
    int Rank,
    int TaskIndex,
    int MonsterIndex,
    uint NameId,
    string TargetName,
    int StartingKilled,
    int Required,
    IReadOnlyList<HuntingLogLocation> Locations);

public sealed record ProgressionGoalState(
    int SchemaVersion,
    NexusGoal Goal,
    IReadOnlyList<NexusTask> Tasks,
    TaskId? ActiveTaskId,
    bool StopAfterCurrentDuty,
    bool ProviderStartObserved,
    bool DutyEntryObserved,
    bool DutyCompletionObserved,
    DateTimeOffset UpdatedAtUtc)
{
    public const int CurrentSchemaVersion = 1;

    public NexusTask? ActiveTask => ActiveTaskId is { } id
        ? Tasks.FirstOrDefault(task => task.Id == id)
        : null;
}

public sealed record ProgressionWorldObservation(
    CharacterKey Character,
    uint ClassJobId,
    int Level,
    bool IsAvailable,
    bool IsInDuty,
    int ItemLevel = 0,
    int Gil = 0);

public sealed record ProgressionCharacterMetrics(int ItemLevel, int Gil);

public sealed record ProgressionActionResult(bool Success, string Message);

public interface IProgressionGoalStore
{
    ProgressionGoalState? Load();

    void Save(ProgressionGoalState state);
}

/// <summary>
/// Character-scoped durable Progression intent. The file stores desired state and bounded task
/// checkpoints, never a provider instruction pointer. Each replacement is atomic and preserves
/// the previous complete document for recovery.
/// </summary>
public sealed class FileProgressionGoalStore(string path) : IProgressionGoalStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(), new ReadOnlyResourceSetJsonConverter() },
    };

    public ProgressionGoalState? Load()
    {
        if (!File.Exists(path))
            return null;

        ProgressionGoalState state = JsonSerializer.Deserialize<ProgressionGoalState>(
            File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("The Progression goal state is empty.");
        Validate(state);
        return state;
    }

    public void Save(ProgressionGoalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Validate(state);

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("The Progression goal path has no parent directory.");

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        string backupPath = path + ".previous";
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, state, Options);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
                File.Copy(path, backupPath, overwrite: true);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void Validate(ProgressionGoalState state)
    {
        if (state.SchemaVersion != ProgressionGoalState.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported Progression goal schema {state.SchemaVersion}.");
        if (!state.Goal.Character.IsKnown)
            throw new InvalidDataException("The Progression goal has no character scope.");
        if (state.Goal.Id.Value == Guid.Empty)
            throw new InvalidDataException("The Progression goal ID is missing.");
        if (state.Tasks.Count > 100)
            throw new InvalidDataException("The Progression task history exceeds the 100-task safety limit.");
        if (state.Tasks.Any(task => task.Id.Value == Guid.Empty || task.GoalId != state.Goal.Id))
            throw new InvalidDataException("The Progression task history contains an invalid task identity.");
        if (state.Tasks.Select(task => task.Id).Distinct().Count() != state.Tasks.Count)
            throw new InvalidDataException("The Progression task history contains duplicate task IDs.");
        if (state.ActiveTaskId is { } active && state.Tasks.All(task => task.Id != active))
            throw new InvalidDataException("The active Progression task does not exist in task history.");
    }

    private sealed class ReadOnlyResourceSetJsonConverter : JsonConverter<IReadOnlySet<ResourceKind>>
    {
        public override IReadOnlySet<ResourceKind> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            JsonSerializer.Deserialize<HashSet<ResourceKind>>(ref reader, options)
            ?? throw new JsonException("The task resource set is empty.");

        public override void Write(
            Utf8JsonWriter writer,
            IReadOnlySet<ResourceKind> value,
            JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Order().ToArray(), options);
    }
}

public static class LevelingDutyPolicy
{
    // Stable leveling list migrated from VieriAutoDuty. Runtime eligibility still requires the
    // content to be unlocked, meet level/item-level requirements, and have a provider path.
    public static readonly IReadOnlyList<uint> StableTerritories =
    [
        1036, 1037, 1039, 1041, 1303, 1042, 1330, 1331,
        1043, 1366, 1064, 1065, 1066, 1109, 1142,
        1367, 1144, 1145,
        837, 821, 823, 836, 822,
        952, 969, 970, 974, 978,
        1167, 1193, 1194, 1198, 1208,
    ];

    public static ProgressionDutyCandidate? SelectHighest(
        IEnumerable<ProgressionDutyCandidate> candidates,
        int currentLevel)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates
            .Where(candidate => candidate.RequiredLevel <= currentLevel)
            .OrderBy(candidate => candidate.RequiredLevel)
            .ThenBy(candidate => candidate.RequiredItemLevel)
            .ThenBy(candidate => candidate.TerritoryId)
            .LastOrDefault();
    }
}

/// <summary>
/// Owns one durable Reach Job Level goal and dispatches one bounded gear, quest, or duty task at a
/// time. Quest completion is checked by exact ID; gear and duty work retain their stronger domain
/// postconditions. Reload can only Stop/pause, never replay.
/// </summary>
public sealed class ProgressionExecutionCoordinator
{
    private static readonly GoalKind ReachJobLevelKind = new("vieri.progression.reach-job-level/v1");
    private static readonly TaskKind EnsureGearKind = new("vieri.gear.ensure-readiness/v1");
    private static readonly TaskKind RunMaintenanceKind = new("vieri.inventory.run-between-duty-maintenance/v1");
    private static readonly TaskKind RunQuestKind = new("vieri.quest.run-one/v1");
    private static readonly TaskKind RunHuntingLogKind = new("vieri.hunting-log.complete-target/v1");
    private static readonly TaskKind RunDutyKind = new("vieri.duties.run-one/v1");
    private static readonly CapabilityId GearCapability = new("vieri.capability.gear.ensure-readiness/v1");
    private static readonly CapabilityId MaintenanceCapability = new("vieri.capability.inventory.run-maintenance/v1");
    private static readonly CapabilityId QuestCapability = new("vieri.capability.quest.run-supported/v1");
    private static readonly CapabilityId HuntingLogCapability = new("vieri.capability.hunting-log.complete-target/v1");
    private static readonly CapabilityId DutyCapability = new("vieri.capability.duty.run/v1");
    private static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProviderStartTimeout = TimeSpan.FromSeconds(45);
    private const int MaximumUnsupportedQuestFallbacks = 24;
    private static readonly ResourceKind[] DutyResources =
    [
        ResourceKind.DutyQueue,
        ResourceKind.Teleport,
        ResourceKind.UiInteraction,
        ResourceKind.InventoryMutation,
        ResourceKind.Targeting,
        ResourceKind.Rotation,
    ];
    private static readonly ResourceKind[] GearResources =
    [
        ResourceKind.Teleport,
        ResourceKind.UiInteraction,
        ResourceKind.InventoryMutation,
    ];
    private static readonly ResourceKind[] MaintenanceResources =
    [
        ResourceKind.Teleport,
        ResourceKind.UiInteraction,
        ResourceKind.InventoryMutation,
    ];
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
    private static readonly ResourceKind[] HuntingLogResources =
    [
        ResourceKind.Teleport,
        ResourceKind.Navigation,
        ResourceKind.Targeting,
        ResourceKind.Combat,
        ResourceKind.Rotation,
    ];
    private static readonly ResourceKind[] DutyHuntingLogResources =
    [
        ResourceKind.DutyQueue,
        ResourceKind.Teleport,
        ResourceKind.UiInteraction,
        ResourceKind.Targeting,
        ResourceKind.Combat,
        ResourceKind.Rotation,
    ];

    private readonly IProgressionGoalStore store;
    private readonly ResourceLeaseManager leases;
    private readonly IProgressionDutyProvider provider;
    private readonly IProgressionGearProvider? gearProvider;
    private readonly IProgressionMaintenanceProvider? maintenanceProvider;
    private readonly IProgressionQuestProvider? questProvider;
    private readonly IProgressionHuntingProvider? huntingProvider;
    private readonly Func<DateTimeOffset> utcNow;
    private ResourceLeaseHandle? activeLease;
    private DateTimeOffset? providerStartRequestedAt;
    private bool reloadStopPending;
    private bool cancelAfterReloadStop;
    private bool pauseAfterStop;
    private int unsupportedQuestFallbackDepth;

    public ProgressionExecutionCoordinator(
        IProgressionGoalStore store,
        ResourceLeaseManager leases,
        IProgressionDutyProvider provider,
        IProgressionGearProvider? gearProvider = null,
        IProgressionQuestProvider? questProvider = null,
        IProgressionHuntingProvider? huntingProvider = null,
        IProgressionMaintenanceProvider? maintenanceProvider = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.store = store;
        this.leases = leases;
        this.provider = provider;
        this.gearProvider = gearProvider;
        this.maintenanceProvider = maintenanceProvider;
        this.questProvider = questProvider;
        this.huntingProvider = huntingProvider;
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        State = store.Load();
        ReconcileLoadedState();
    }

    public ProgressionGoalState? State { get; private set; }

    public ProgressionActionResult Start(ReachJobLevelGoalDraft draft, ReachJobLevelPlan plan)
    {
        if (!draft.Character.IsKnown)
            return new(false, "Wait for the current character to finish loading.");
        if (!plan.IsValid || plan.IsSatisfied)
            return new(false, plan.Summary);
        bool questEnabled = (draft.AllowMainScenario || draft.AllowJobQuests || draft.AllowSideQuests) && questProvider is not null;
        bool huntingEnabled = draft.AllowHuntingLog && huntingProvider is not null;
        if (!draft.AllowDuties && !questEnabled && !huntingEnabled)
            return new(false, "Enable Main Scenario, Class/Job/Role, Hunting Log, general side quests, or Duties with a compatible provider before starting.");
        if (State?.Goal.Status is GoalStatus.Active)
            return new(false, "A Progression goal is already active.");
        if (State?.Goal.Status is GoalStatus.Paused or GoalStatus.Blocked)
            return new(false, "Resume or cancel the saved Progression goal before starting a different one.");

        ProgressionDutyCandidate? duty = draft.AllowDuties
            ? LevelingDutyPolicy.SelectHighest(provider.EligibleDuties(draft.CurrentLevel), draft.CurrentLevel)
            : null;
        ProgressionQuestCandidate? quest = SelectQuest(
            draft.ClassJobId,
            draft.CurrentLevel,
            draft.AllowMainScenario,
            draft.AllowJobQuests,
            draft.AllowSideQuests);
        ProgressionHuntingTargetCandidate? hunt = SelectHuntingTarget(
            draft.ClassJobId,
            draft.CurrentLevel,
            draft.AllowHuntingLog);
        if (duty is null && quest is null && hunt is null && gearProvider is null)
            return new(false, "No eligible quest, Hunting Log target, or leveling duty is currently available.");

        DateTimeOffset now = utcNow();
        GoalId goalId = GoalId.New();
        ReachJobLevelDesiredState desired = new(
            draft.ClassJobId,
            draft.TargetLevel,
            draft.AllowJobQuests,
            draft.AllowHuntingLog,
            draft.AllowSideQuests,
            draft.AllowDuties,
            draft.MinimumGilReserve,
            draft.AllowMainScenario);
        NexusGoal goal = new(
            goalId,
            ReachJobLevelKind,
            1,
            draft.Character,
            $"Reach level {draft.TargetLevel} on the current job",
            JsonSerializer.Serialize(desired),
            [
                new GoalConstraint("minimum-gil-reserve", ConstraintStrength.Required,
                    draft.MinimumGilReserve.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    $"Keep at least {draft.MinimumGilReserve:N0} gil."),
            ],
            50,
            GoalStatus.Active,
            now,
            now,
            1,
            gearProvider is null
                ? quest is not null
                    ? $"Preparing the quest {quest.Name}."
                    : hunt is not null
                        ? $"Preparing the Hunting Log target {hunt.TargetName}."
                        : $"Preparing one bounded run of {duty!.Name}."
                : "Preparing a Nexus-owned gear-readiness check before the next activity.");
        State = new ProgressionGoalState(
            ProgressionGoalState.CurrentSchemaVersion,
            goal,
            [],
            null,
            false,
            false,
            false,
            false,
            now);
        Save();
        return gearProvider is null
            ? StartNextActivity(desired, draft.CurrentLevel, quest, hunt, duty)
            : StartGearReadiness(draft.CurrentLevel, draft.MinimumGilReserve,
                draft.CurrentItemLevel, draft.CurrentGil);
    }

    public ProgressionActionResult Resume(ProgressionWorldObservation world)
    {
        if (State is null)
            return new(false, "There is no saved Progression goal to resume.");
        if (reloadStopPending || State.ActiveTaskId is not null)
            return new(false, "Wait for reload reconciliation to confirm the prior provider task is inactive.");
        if (State.Goal.Status is not (GoalStatus.Paused or GoalStatus.Blocked))
            return new(false, "The Progression goal is not paused or blocked.");
        if (State.Goal.Character != world.Character || !world.IsAvailable)
            return new(false, "Load the character that owns this Progression goal before resuming.");

        ReachJobLevelDesiredState desired = ReadDesiredState();
        if (world.ClassJobId != desired.ClassJobId)
            return new(false, "Switch back to the job that owns this goal before resuming.");
        if (world.Level >= desired.TargetLevel)
        {
            MarkGoalSatisfied(world.Level);
            return new(true, State!.Goal.StatusDetail!);
        }

        UpdateGoal(GoalStatus.Active, "Replanning the next bounded activity.", incrementPlanRevision: true);
        if (gearProvider is not null)
            return StartGearReadiness(world.Level, desired.MinimumGilReserve, world.ItemLevel, world.Gil);

        return StartNextActivity(desired, world.Level);
    }

    public ProgressionActionResult StopAfterCurrentDuty()
    {
        if (State?.Goal.Status != GoalStatus.Active || State.ActiveTask is null)
            return new(false, "No bounded Progression activity is currently active.");

        State = State with
        {
            StopAfterCurrentDuty = true,
            Goal = State.Goal with
            {
                UpdatedAt = utcNow(),
                StatusDetail = "Stop-after armed. Nexus will pause this goal after the current bounded activity.",
            },
            UpdatedAtUtc = utcNow(),
        };
        Save();
        return new(true, State.Goal.StatusDetail!);
    }

    public ProgressionActionResult StopNow()
    {
        pauseAfterStop = false;
        if (State is null || State.Goal.Status is GoalStatus.Cancelled or GoalStatus.Satisfied)
            return new(false, "No active Progression goal needs to be stopped.");

        if (reloadStopPending)
        {
            cancelAfterReloadStop = true;
            bool stopRequested = TryStopProvider(State.ActiveTask, out string stopMessage);
            UpdateGoal(GoalStatus.Paused,
                stopRequested
                    ? "Cancelling the saved goal after reload Stop is confirmed."
                    : "The saved goal cannot be cancelled until provider inactivity can be confirmed.");
            return new(stopRequested, stopMessage);
        }

        NexusTask? task = State.ActiveTask;
        if (task is null)
        {
            UpdateGoal(GoalStatus.Cancelled, "Progression goal cancelled.");
            return new(true, State!.Goal.StatusDetail!);
        }

        bool requested = TryStopProvider(task, out string message);
        if (!requested)
            pauseAfterStop = false;
        ReplaceTask(task with
        {
            Status = requested ? NexusTaskStatus.Cancelling : NexusTaskStatus.NeedsReconciliation,
            StatusDetail = requested ? "Stop requested; waiting for the provider to confirm inactivity." : message,
            Failure = requested ? null : new TaskFailure(
                FailureKind.DependencyUnavailable,
                "provider-stop-unavailable",
                "Nexus could not confirm that the activity provider stopped.",
                message,
                true),
        });
        UpdateGoal(requested ? GoalStatus.Active : GoalStatus.Blocked,
            requested ? "Stopping the Nexus-owned activity before cancelling the goal." :
                "Stop could not be confirmed. Do not start other automated work yet.");
        return new(requested, message);
    }

    public ProgressionActionResult PauseNow()
    {
        if (State is null || State.Goal.Status is GoalStatus.Cancelled or GoalStatus.Satisfied)
            return new(false, "No active Progression goal needs to be paused.");
        if (State.Goal.Status == GoalStatus.Paused && State.ActiveTask is null)
            return new(true, "Progression is already paused.");

        NexusTask? task = State.ActiveTask;
        if (task is null)
        {
            UpdateGoal(GoalStatus.Paused, "Progression paused. Resume will create a fresh bounded plan.");
            return new(true, State!.Goal.StatusDetail!);
        }

        pauseAfterStop = true;
        bool requested = TryStopProvider(task, out string message);
        if (!requested)
            pauseAfterStop = false;
        ReplaceTask(task with
        {
            Status = requested ? NexusTaskStatus.Cancelling : NexusTaskStatus.NeedsReconciliation,
            StatusDetail = requested ? "Pause requested; waiting for the provider to confirm inactivity." : message,
            Failure = requested ? null : new TaskFailure(
                FailureKind.DependencyUnavailable,
                "provider-pause-stop-unavailable",
                "Nexus could not confirm that the activity provider stopped for Pause.",
                message,
                true),
        });
        UpdateGoal(requested ? GoalStatus.Active : GoalStatus.Blocked,
            requested ? "Stopping the current activity before pausing Progression." :
                "Pause could not be confirmed. Do not start other automated work yet.");
        return new(requested, message);
    }

    public void ObserveDutyCompletion(uint territoryId)
    {
        if (State?.ActiveTask is not { } task ||
            task.Status is not (NexusTaskStatus.Running or NexusTaskStatus.Verifying))
            return;

        ProgressionDutyTaskPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ProgressionDutyTaskPayload>(task.PayloadJson);
        }
        catch (JsonException)
        {
            return;
        }
        if (payload is null || payload.TerritoryId != territoryId)
            return;

        State = State with
        {
            DutyCompletionObserved = true,
            UpdatedAtUtc = utcNow(),
        };
        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Verifying,
            StatusDetail = $"{payload.DutyName} completed; waiting for provider shutdown and return to the world.",
        });
        Save();
    }

    public void Update(ProgressionWorldObservation world)
    {
        if (State is null)
            return;

        if (State.Goal.Character != world.Character)
        {
            if (State.Goal.Status == GoalStatus.Active)
                StopNow();
            return;
        }

        if (reloadStopPending)
        {
            ReconcileReloadStop();
            return;
        }

        NexusTask? task = State.ActiveTask;
        if (task is null || State.Goal.Status is not (GoalStatus.Active or GoalStatus.Blocked))
            return;

        if (task.Status is NexusTaskStatus.Cancelling or NexusTaskStatus.NeedsReconciliation)
        {
            ReconcileStoppingTask(task);
            return;
        }

        ReachJobLevelDesiredState desired = ReadDesiredState();
        if (world.IsAvailable && world.ClassJobId != desired.ClassJobId)
        {
            BeginFailureStop(task, FailureKind.UnsafeState, "job-changed",
                "The active job changed. Nexus is stopping the duty provider before pausing this goal.", true);
            return;
        }

        if (task.Kind != RunMaintenanceKind && (activeLease is null || !activeLease.Heartbeat(LeaseLifetime)))
        {
            BeginFailureStop(task, FailureKind.UnsafeState, "lease-lost",
                "Progression ownership expired. Nexus is stopping the provider and will not schedule more work.", true);
            return;
        }

        if (task.Kind == EnsureGearKind)
        {
            UpdateGearReadiness(task, world);
            return;
        }

        if (task.Kind == RunMaintenanceKind)
        {
            UpdateMaintenance(task, world);
            return;
        }

        if (task.Kind == RunQuestKind)
        {
            UpdateQuestWork(task, world);
            return;
        }

        if (task.Kind == RunHuntingLogKind)
        {
            UpdateHuntingLogWork(task, world);
            return;
        }

        ProgressionDutyProviderObservation observation = provider.Observe();
        if (!observation.IsAvailable || observation.IsStopped is null)
        {
            BeginFailureStop(task, FailureKind.DependencyUnavailable, "provider-unavailable",
                "The duty provider became unavailable. Nexus is retaining ownership until inactivity is confirmed.", true);
            return;
        }

        bool providerObserved = State.ProviderStartObserved || observation.IsStopped == false;
        bool dutyObserved = State.DutyEntryObserved || world.IsInDuty;
        bool checkpointChanged = providerObserved != State.ProviderStartObserved ||
                                 dutyObserved != State.DutyEntryObserved;
        if (checkpointChanged)
        {
            State = State with
            {
                ProviderStartObserved = providerObserved,
                DutyEntryObserved = dutyObserved,
                UpdatedAtUtc = utcNow(),
            };
        }

        if (task.Status == NexusTaskStatus.Acquiring)
        {
            if (observation.IsStopped == false)
            {
                ReplaceTask(task with
                {
                    Status = NexusTaskStatus.Running,
                    StatusDetail = "Duty provider accepted the bounded run; waiting for verified duty entry and completion.",
                });
                Save();
                return;
            }

            if (providerStartRequestedAt is { } requestedAt && utcNow() - requestedAt >= ProviderStartTimeout)
            {
                FailTask(task, FailureKind.TransientExternal, "provider-start-timeout",
                    "The duty provider did not start the bounded run in time.", observation.Detail, true);
            }
            else if (checkpointChanged)
            {
                Save();
            }
            return;
        }

        if (task.Status == NexusTaskStatus.Cancelling)
        {
            if (observation.IsStopped == true)
                CompleteCancellation(task);
            return;
        }

        if (task.Status is not (NexusTaskStatus.Running or NexusTaskStatus.Verifying))
            return;

        if (observation.IsStopped == false)
        {
            if (task.Status != NexusTaskStatus.Running)
            {
                ReplaceTask(task with { Status = NexusTaskStatus.Running, StatusDetail = "Bounded duty is running." });
                Save();
            }
            else if (checkpointChanged)
            {
                Save();
            }
            return;
        }

        if (!State.ProviderStartObserved || !State.DutyEntryObserved)
        {
            FailTask(task, FailureKind.TransientExternal, "provider-ended-before-duty",
                "The duty provider stopped before Nexus could verify duty entry.", observation.Detail, true);
            return;
        }

        if (!State.DutyCompletionObserved)
        {
            if (world.IsInDuty || !world.IsAvailable)
            {
                if (task.Status != NexusTaskStatus.Verifying ||
                    task.StatusDetail != "Provider stopped; waiting for the game's duty-completion confirmation.")
                {
                    ReplaceTask(task with
                    {
                        Status = NexusTaskStatus.Verifying,
                        StatusDetail = "Provider stopped; waiting for the game's duty-completion confirmation.",
                    });
                    Save();
                }
                return;
            }

            FailTask(task, FailureKind.TransientExternal, "provider-ended-without-completion",
                "The duty ended without a completion confirmation. Nexus paused the goal and will not count or replay it.",
                observation.Detail, true);
            return;
        }

        if (world.IsInDuty || !world.IsAvailable)
        {
            if (task.Status != NexusTaskStatus.Verifying ||
                task.StatusDetail != "Provider stopped; waiting to verify return from the duty.")
            {
                ReplaceTask(task with
                {
                    Status = NexusTaskStatus.Verifying,
                    StatusDetail = "Provider stopped; waiting to verify return from the duty.",
                });
                Save();
            }
            return;
        }

        CompleteDuty(task, world);
    }

    public void Shutdown()
    {
        if (State?.Goal.Status != GoalStatus.Active || State.ActiveTask is null)
            return;

        TryStopProvider(State.ActiveTask, out string detail);
        NexusTask task = State.ActiveTask;
        ReplaceTask(task with
        {
            Status = NexusTaskStatus.NeedsReconciliation,
            StatusDetail = "Nexus unloaded during this task. The provider was stopped; resume requires a fresh plan.",
            Failure = new TaskFailure(
                FailureKind.UserIntervention,
                "plugin-unloaded",
            "Nexus unloaded during the bounded Progression task.",
                detail,
                false),
        });
        ReleaseLease();
        UpdateGoal(GoalStatus.Paused,
            "Nexus stopped the prior provider operation during unload. Resume builds a fresh bounded task; nothing replays automatically.");
    }

    private ProgressionActionResult StartGearReadiness(
        int currentLevel,
        int minimumGilReserve,
        int startingItemLevel,
        int startingGil)
    {
        if (State is null || gearProvider is null)
            return new(false, "The Nexus gear-readiness provider is unavailable.");

        ProgressionGearProviderObservation observation = gearProvider.ObserveGearReadiness();
        if (!observation.IsAvailable || observation.IsBusy is null)
        {
            UpdateGoal(GoalStatus.Blocked, observation.Detail);
            return new(false, observation.Detail);
        }
        if (observation.IsBusy == true)
        {
            UpdateGoal(GoalStatus.Blocked,
                "The gear provider is already doing work Nexus does not own. Stop it before resuming this goal.");
            return new(false, State.Goal.StatusDetail!);
        }

        DateTimeOffset now = utcNow();
        TaskId taskId = TaskId.New();
        AttemptId attemptId = AttemptId.New();
        ProgressionGearTaskPayload payload = new(
            minimumGilReserve,
            Math.Max(0, startingItemLevel),
            Math.Max(0, startingGil),
            observation.StartedSequence,
            observation.CompletedSequence);
        NexusTask task = new(
            taskId,
            State.Goal.Id,
            EnsureGearKind,
            1,
            "Check and equip gear upgrades",
            "Nexus owns the complete vendor trip, exact purchases, equipment verification, cleanup, and spending floor.",
            GearCapability,
            gearProvider.Id,
            GearResources.ToHashSet(),
            NexusTaskStatus.Ready,
            JsonSerializer.Serialize(payload),
            $"Waiting to protect {minimumGilReserve:N0} gil and acquire gear resources.",
            null);

        State = State with
        {
            Tasks = [.. State.Tasks.TakeLast(99), task],
            ActiveTaskId = taskId,
            ProviderStartObserved = false,
            DutyEntryObserved = false,
            DutyCompletionObserved = false,
            Goal = State.Goal with
            {
                UpdatedAt = now,
                Status = GoalStatus.Active,
                StatusDetail = "Preparing the current job's gear before selecting the next duty.",
            },
            UpdatedAtUtc = now,
        };
        Save();

        LeaseOwner owner = new(State.Goal.Id, taskId, attemptId, State.Goal.Priority,
            "Progression: Nexus-owned gear readiness");
        if (!leases.TryAcquire(owner, GearResources, LeaseLifetime, out activeLease,
                out ResourceLeaseSnapshot? blocking))
        {
            string blocker = blocking is null ? "another task" : blocking.Owner.Reason;
            FailTask(task, FailureKind.ResourceConflict, "gear-resource-conflict",
                $"Gear readiness is waiting because {blocker} owns a required resource.", null, true);
            return new(false, State!.Goal.StatusDetail!);
        }

        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Acquiring,
            StatusDetail = "Resources acquired; applying the Nexus gil floor and starting gear readiness.",
        });
        Save();

        if (!gearProvider.TryStartGearReadiness(minimumGilReserve, out string message))
        {
            FailTask(State.ActiveTask!, FailureKind.DependencyUnavailable, "gear-provider-start-rejected",
                "The gear provider rejected the Nexus-owned transaction.", message, true);
            return new(false, message);
        }

        providerStartRequestedAt = now;
        ReplaceTask(State.ActiveTask! with
        {
            Status = NexusTaskStatus.Running,
            StatusDetail = "The provider accepted the transaction; Nexus is monitoring shopping and equipment verification.",
        });
        State = State with
        {
            ProviderStartObserved = true,
            Goal = State.Goal with { StatusDetail = message, UpdatedAt = now },
            UpdatedAtUtc = now,
        };
        Save();
        return new(true, message);
    }

    private void UpdateGearReadiness(NexusTask task, ProgressionWorldObservation world)
    {
        if (gearProvider is null)
        {
            BeginFailureStop(task, FailureKind.DependencyUnavailable, "gear-provider-unavailable",
                "The gear-readiness provider is unavailable.", true);
            return;
        }

        ProgressionGearProviderObservation observation = gearProvider.ObserveGearReadiness();
        if (!observation.IsAvailable || observation.IsBusy is null)
        {
            BeginFailureStop(task, FailureKind.DependencyUnavailable, "gear-provider-unavailable",
                "The gear provider became unavailable. Nexus is retaining ownership until inactivity is confirmed.", true);
            return;
        }

        bool started = State!.ProviderStartObserved || observation.IsBusy == true;
        if (started != State.ProviderStartObserved)
            State = State with { ProviderStartObserved = started, UpdatedAtUtc = utcNow() };

        if (task.Status == NexusTaskStatus.Acquiring)
        {
            if (observation.IsBusy == true)
            {
                ReplaceTask(task with
                {
                    Status = NexusTaskStatus.Running,
                    StatusDetail = "Checking vendor upgrades, purchasing within the Nexus gil floor, and verifying equipment.",
                });
                Save();
                return;
            }

            if (providerStartRequestedAt is { } requestedAt && utcNow() - requestedAt >= ProviderStartTimeout)
                FailTask(task, FailureKind.TransientExternal, "gear-provider-start-timeout",
                    "Gear readiness did not start in time.", observation.Detail, true);
            else if (started)
                Save();
            return;
        }

        if (observation.IsBusy == true)
            return;
        if (!State.ProviderStartObserved)
        {
            FailTask(task, FailureKind.TransientExternal, "gear-provider-ended-before-start",
                "Gear readiness ended before Nexus could verify that it started.", observation.Detail, true);
            return;
        }
        if (!world.IsAvailable || world.IsInDuty)
        {
            if (task.Status != NexusTaskStatus.Verifying)
            {
                ReplaceTask(task with
                {
                    Status = NexusTaskStatus.Verifying,
                    StatusDetail = "Gear work ended; waiting for the character snapshot before verifying results.",
                });
                Save();
            }
            return;
        }

        ProgressionGearTaskPayload payload = JsonSerializer.Deserialize<ProgressionGearTaskPayload>(task.PayloadJson)
            ?? throw new InvalidDataException("The gear-readiness task payload is empty.");
        if (observation.CompletedSequence <= payload.BaselineCompletedSequence)
        {
            FailTask(task, FailureKind.TransientExternal, "gear-transaction-not-completed",
                "Nexus gear shopping ended before the approved transaction completed.", observation.Detail, true);
            return;
        }
        int minimumAllowedGil = Math.Min(payload.StartingGil, payload.MinimumGilReserve);
        if (world.Gil < minimumAllowedGil)
        {
            FailTask(task, FailureKind.UnsafeState, "gear-gil-floor-violated",
                $"Gear readiness stopped because gil fell below the protected floor ({world.Gil:N0} remaining; {minimumAllowedGil:N0} required).",
                observation.Detail, false);
            return;
        }
        if (world.ItemLevel < payload.StartingItemLevel)
        {
            FailTask(task, FailureKind.UnsafeState, "gear-item-level-regressed",
                $"Gear verification detected an item-level regression from {payload.StartingItemLevel} to {world.ItemLevel}.",
                observation.Detail, false);
            return;
        }

        int purchased = observation.CompletedSequence > payload.BaselineCompletedSequence
            ? Math.Max(0, observation.ItemsPurchased)
            : 0;
        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Succeeded,
            StatusDetail = $"Verified gear readiness: item level {payload.StartingItemLevel} → {world.ItemLevel}, " +
                           $"{purchased} item(s) purchased, {world.Gil:N0} gil remaining.",
            Failure = null,
        });
        ReleaseLease();
        State = State with { ActiveTaskId = null, ProviderStartObserved = false };
        UpdateGoal(GoalStatus.Active, "Gear readiness verified; selecting the next eligible activity.");
        StartNextActivity(ReadDesiredState(), world.Level);
    }

    private ProgressionActionResult StartBetweenDutyMaintenance(ProgressionWorldObservation world)
    {
        if (State is null || maintenanceProvider is null)
            return ContinueAfterMaintenance(world);

        ProgressionMaintenanceProviderObservation observation = maintenanceProvider.ObserveMaintenance();
        if (!observation.HasConfiguredOperations)
            return ContinueAfterMaintenance(world);
        if (!observation.IsAvailable || observation.IsBusy is null)
        {
            UpdateGoal(GoalStatus.Blocked, observation.Detail);
            return new(false, observation.Detail);
        }
        if (observation.IsBusy == true)
        {
            const string detail = "Nexus maintenance is already running outside this Progression goal. Stop it before resuming.";
            UpdateGoal(GoalStatus.Blocked, detail);
            return new(false, detail);
        }

        DateTimeOffset now = utcNow();
        TaskId taskId = TaskId.New();
        ProgressionMaintenanceTaskPayload payload = new(
            world.Level,
            observation.StartedSequence,
            observation.CompletedSequence);
        NexusTask task = new(
            taskId,
            State.Goal.Id,
            RunMaintenanceKind,
            1,
            "Run enabled between-duty maintenance",
            "Nexus runs the imported safe maintenance profile after a verified duty and before planning another activity.",
            MaintenanceCapability,
            maintenanceProvider.Id,
            MaintenanceResources.ToHashSet(),
            NexusTaskStatus.Ready,
            JsonSerializer.Serialize(payload),
            "Starting the enabled Nexus maintenance sequence.",
            null);

        State = State with
        {
            Tasks = [.. State.Tasks.TakeLast(99), task],
            ActiveTaskId = taskId,
            ProviderStartObserved = false,
            DutyEntryObserved = false,
            DutyCompletionObserved = false,
            Goal = State.Goal with
            {
                UpdatedAt = now,
                Status = GoalStatus.Active,
                StatusDetail = "Running enabled maintenance before the next bounded activity.",
            },
            UpdatedAtUtc = now,
        };
        Save();

        if (!maintenanceProvider.TryStartConfiguredMaintenance(out string message))
        {
            FailTask(State.ActiveTask!, FailureKind.DependencyUnavailable, "maintenance-start-rejected",
                "The enabled between-duty maintenance sequence could not start.", message, true);
            return new(false, message);
        }

        ReplaceTask(State.ActiveTask! with
        {
            Status = NexusTaskStatus.Running,
            StatusDetail = message,
        });
        State = State with
        {
            ProviderStartObserved = true,
            Goal = State.Goal with { StatusDetail = message, UpdatedAt = now },
            UpdatedAtUtc = now,
        };
        Save();
        return new(true, message);
    }

    private void UpdateMaintenance(NexusTask task, ProgressionWorldObservation world)
    {
        if (maintenanceProvider is null)
        {
            BeginFailureStop(task, FailureKind.DependencyUnavailable, "maintenance-provider-unavailable",
                "The Nexus maintenance provider is unavailable.", true);
            return;
        }

        ProgressionMaintenanceProviderObservation observation = maintenanceProvider.ObserveMaintenance();
        if (!observation.IsAvailable || observation.IsBusy is null)
        {
            BeginFailureStop(task, FailureKind.DependencyUnavailable, "maintenance-provider-unavailable",
                "Nexus maintenance became unavailable. Ownership remains blocked until inactivity is confirmed.", true);
            return;
        }
        if (observation.IsBusy == true)
        {
            if (task.StatusDetail != observation.Detail)
            {
                ReplaceTask(task with { Status = NexusTaskStatus.Running, StatusDetail = observation.Detail });
                UpdateGoal(GoalStatus.Active, observation.Detail);
            }
            return;
        }
        if (!world.IsAvailable || world.IsInDuty)
        {
            ReplaceTask(task with
            {
                Status = NexusTaskStatus.Verifying,
                StatusDetail = "Maintenance ended; waiting for a stable character snapshot before continuing.",
            });
            Save();
            return;
        }

        ProgressionMaintenanceTaskPayload payload =
            JsonSerializer.Deserialize<ProgressionMaintenanceTaskPayload>(task.PayloadJson)
            ?? throw new InvalidDataException("The maintenance task payload is empty.");
        if (observation.CompletedSequence <= payload.BaselineCompletedSequence)
        {
            FailTask(task, FailureKind.TransientExternal, "maintenance-not-completed",
                "Nexus maintenance stopped before the enabled sequence was verified complete.", observation.Detail, true);
            return;
        }

        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Succeeded,
            StatusDetail = observation.Detail,
            Failure = null,
        });
        State = State! with { ActiveTaskId = null, ProviderStartObserved = false };
        UpdateGoal(GoalStatus.Active, "Between-duty maintenance is verified; checking gear before the next activity.");
        ContinueAfterMaintenance(world);
    }

    private ProgressionActionResult ContinueAfterMaintenance(ProgressionWorldObservation world)
    {
        ReachJobLevelDesiredState desired = ReadDesiredState();
        if (gearProvider is not null)
            return StartGearReadiness(world.Level, desired.MinimumGilReserve, world.ItemLevel, world.Gil);
        return StartNextActivity(desired, world.Level);
    }

    private ProgressionActionResult StartNextActivity(
        ReachJobLevelDesiredState desired,
        int currentLevel,
        ProgressionQuestCandidate? quest,
        ProgressionHuntingTargetCandidate? hunt,
        ProgressionDutyCandidate? duty) =>
        quest is not null
            ? StartNextQuest(currentLevel, quest)
            : hunt is not null
                ? StartNextHuntingLog(currentLevel, hunt)
                : duty is not null
                    ? StartNextDuty(currentLevel, duty)
                    : BlockNoActivity(desired);

    private ProgressionActionResult StartNextActivity(ReachJobLevelDesiredState desired, int currentLevel)
    {
        ProgressionQuestCandidate? quest = SelectQuest(
            desired.ClassJobId,
            currentLevel,
            desired.AllowMainScenario,
            desired.AllowJobQuests,
            desired.AllowSideQuests);
        if (quest is not null)
            return StartNextQuest(currentLevel, quest);

        ProgressionHuntingTargetCandidate? hunt = SelectHuntingTarget(
            desired.ClassJobId,
            currentLevel,
            desired.AllowHuntingLog);
        if (hunt is not null)
            return StartNextHuntingLog(currentLevel, hunt);

        ProgressionDutyCandidate? duty = desired.AllowDuties
            ? LevelingDutyPolicy.SelectHighest(provider.EligibleDuties(currentLevel), currentLevel)
            : null;
        if (duty is not null)
            return StartNextDuty(currentLevel, duty);

        return BlockNoActivity(desired);
    }

    private ProgressionActionResult BlockNoActivity(ReachJobLevelDesiredState desired)
    {
        bool anyQuest = desired.AllowMainScenario || desired.AllowJobQuests || desired.AllowSideQuests;
        bool anyOpenWorld = anyQuest || desired.AllowHuntingLog;
        string reason = anyOpenWorld && desired.AllowDuties
            ? "No eligible supported quest, Hunting Log target, or unlocked leveling duty is currently available."
            : anyOpenWorld
                ? "No eligible supported quest or Hunting Log target is currently available for the selected methods."
                : "No unlocked leveling duty currently meets the job, item-level, and provider-path requirements.";
        UpdateGoal(GoalStatus.Blocked, reason, incrementPlanRevision: true);
        return new(false, reason);
    }

    private ProgressionQuestCandidate? SelectQuest(
        uint classJobId,
        int currentLevel,
        bool includeMainScenario,
        bool includeClassJobRole,
        bool includeGeneralSideQuests) =>
        questProvider?.EligibleQuests(classJobId, currentLevel, includeMainScenario, includeClassJobRole, includeGeneralSideQuests)
            .OrderByDescending(candidate => candidate.IsAccepted)
            .ThenBy(candidate => candidate.Kind == ProgressionQuestKind.MainScenario ? 0 :
                candidate.Kind == ProgressionQuestKind.ClassJobRole ? 1 : 2)
            .ThenBy(candidate => candidate.RequiredLevel)
            .ThenBy(candidate => candidate.QuestId, StringComparer.Ordinal)
            .FirstOrDefault();

    private ProgressionHuntingTargetCandidate? SelectHuntingTarget(
        uint classJobId,
        int currentLevel,
        bool includeHuntingLog) =>
        includeHuntingLog
            ? huntingProvider?.EligibleTargets(classJobId, currentLevel).FirstOrDefault()
            : null;

    private ProgressionActionResult StartNextHuntingLog(
        int currentLevel,
        ProgressionHuntingTargetCandidate target)
    {
        if (State is null || huntingProvider is null)
            return new(false, "The Hunting Log provider is unavailable.");

        DateTimeOffset now = utcNow();
        TaskId taskId = TaskId.New();
        AttemptId attemptId = AttemptId.New();
        ProgressionHuntingTaskPayload payload = new(
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
        ResourceKind[] requiredResources = target.IsDutyOnly
            ? DutyHuntingLogResources
            : HuntingLogResources;
        NexusTask task = new(
            taskId,
            State.Goal.Id,
            RunHuntingLogKind,
            1,
            $"Complete {target.TargetName}",
            target.IsDutyOnly
                ? "Nexus selected one exact Grand Company target and owns the single duty run, Stop, and kill verification."
                : "Nexus selected one exact Hunting Log target and owns travel, targeting, combat, Stop, and kill verification.",
            HuntingLogCapability,
            huntingProvider.Id,
            requiredResources.ToHashSet(),
            NexusTaskStatus.Ready,
            JsonSerializer.Serialize(payload),
            "Waiting to acquire Hunting Log resources.",
            null);

        State = State with
        {
            Tasks = [.. State.Tasks.TakeLast(99), task],
            ActiveTaskId = taskId,
            StopAfterCurrentDuty = false,
            ProviderStartObserved = false,
            DutyEntryObserved = false,
            DutyCompletionObserved = false,
            Goal = State.Goal with
            {
                UpdatedAt = now,
                Status = GoalStatus.Active,
                StatusDetail = $"Preparing Hunting Log target {target.TargetName}.",
            },
            UpdatedAtUtc = now,
        };
        Save();

        LeaseOwner owner = new(State.Goal.Id, taskId, attemptId, State.Goal.Priority,
            $"Progression: Hunting Log target {target.TargetName}");
        if (!leases.TryAcquire(owner, requiredResources, LeaseLifetime, out activeLease,
                out ResourceLeaseSnapshot? blocking))
        {
            string blocker = blocking is null ? "another task" : blocking.Owner.Reason;
            FailTask(task, FailureKind.ResourceConflict, "hunting-log-resource-conflict",
                $"The Hunting Log target is waiting because {blocker} owns a required resource.", null, true);
            return new(false, State!.Goal.StatusDetail!);
        }

        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Acquiring,
            StatusDetail = "Resources acquired; starting Nexus-owned travel to the selected Hunting Log target.",
        });
        Save();

        if (!huntingProvider.TryStartHunt(target, out string message))
        {
            FailTask(State.ActiveTask!, FailureKind.DependencyUnavailable, "hunting-log-start-rejected",
                "Nexus could not start the selected Hunting Log target.", message, true);
            return new(false, message);
        }

        providerStartRequestedAt = now;
        State = State with
        {
            Goal = State.Goal with { StatusDetail = message, UpdatedAt = now },
            UpdatedAtUtc = now,
        };
        Save();
        return new(true, message);
    }

    private void UpdateHuntingLogWork(NexusTask task, ProgressionWorldObservation world)
    {
        if (huntingProvider is null)
        {
            BeginFailureStop(task, FailureKind.DependencyUnavailable, "hunting-log-provider-unavailable",
                "The Hunting Log provider is unavailable.", true);
            return;
        }

        ProgressionHuntingTaskPayload payload = JsonSerializer.Deserialize<ProgressionHuntingTaskPayload>(task.PayloadJson)
            ?? throw new InvalidDataException("The Hunting Log task payload is empty.");
        ProgressionHuntingTargetCandidate target = new(
            payload.LogKey,
            payload.LogName,
            payload.Rank,
            payload.TaskIndex,
            payload.MonsterIndex,
            payload.NameId,
            payload.TargetName,
            payload.StartingKilled,
            payload.Required,
            payload.Locations);
        ProgressionHuntingProviderObservation observation = huntingProvider.ObserveHunt(target);
        if (!observation.IsAvailable || observation.IsBusy is null)
        {
            BeginFailureStop(task, FailureKind.DependencyUnavailable, "hunting-log-provider-unavailable",
                "A Hunting Log provider became unavailable. Nexus is retaining ownership until inactivity is confirmed.", true);
            return;
        }

        if (observation.HasFailed)
        {
            FailTask(task, FailureKind.TransientExternal, "hunting-log-provider-failed",
                "The selected Hunting Log target could not be completed safely.", observation.Detail, true);
            return;
        }

        bool started = State!.ProviderStartObserved || observation.IsBusy == true;
        if (started != State.ProviderStartObserved)
            State = State with { ProviderStartObserved = started, UpdatedAtUtc = utcNow() };

        if (observation.IsComplete && observation.IsBusy == false)
        {
            CompleteHuntingLog(task, world, payload, observation.Killed);
            return;
        }

        if (task.Status == NexusTaskStatus.Acquiring)
        {
            if (observation.IsBusy == true)
            {
                ReplaceTask(task with
                {
                    Status = NexusTaskStatus.Running,
                    StatusDetail = observation.Detail,
                });
                Save();
                return;
            }

            if (providerStartRequestedAt is { } requestedAt && utcNow() - requestedAt >= ProviderStartTimeout)
                FailTask(task, FailureKind.TransientExternal, "hunting-log-start-timeout",
                    "The selected Hunting Log target did not start in time.", observation.Detail, true);
            return;
        }

        if (observation.IsBusy == true)
        {
            if (task.StatusDetail != observation.Detail)
            {
                ReplaceTask(task with { Status = NexusTaskStatus.Running, StatusDetail = observation.Detail });
                Save();
            }
            return;
        }

        FailTask(task, FailureKind.TransientExternal, "hunting-log-ended-without-credit",
            "Hunting Log work stopped before the selected target was complete.", observation.Detail, true);
    }

    private void CompleteHuntingLog(
        NexusTask task,
        ProgressionWorldObservation world,
        ProgressionHuntingTaskPayload payload,
        int killed)
    {
        if (killed < payload.Required)
        {
            FailTask(task, FailureKind.PreconditionChanged, "hunting-log-credit-unverified",
                "The target stopped without the required Hunting Log credit.", null, true);
            return;
        }

        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Succeeded,
            StatusDetail = $"Verified {payload.TargetName} complete ({killed}/{payload.Required}).",
            Failure = null,
        });
        ReleaseLease();
        State = State! with { ActiveTaskId = null, ProviderStartObserved = false };

        ReachJobLevelDesiredState desired = ReadDesiredState();
        if (world.Level >= desired.TargetLevel)
        {
            MarkGoalSatisfied(world.Level);
            return;
        }

        if (State.StopAfterCurrentDuty)
        {
            State = State with { StopAfterCurrentDuty = false };
            UpdateGoal(GoalStatus.Paused,
                $"Current activity complete at level {world.Level}. Resume when you want Nexus to continue.",
                incrementPlanRevision: true);
            return;
        }

        UpdateGoal(GoalStatus.Active,
            $"Verified {payload.TargetName}; selecting the next bounded activity.",
            incrementPlanRevision: true);
        bool completedDutyTarget = payload.Locations.Count > 0 &&
                                   payload.Locations.All(location =>
                                       !location.IsOpenWorld && location.DutyTerritoryId != 0);
        if (completedDutyTarget)
            StartBetweenDutyMaintenance(world);
        else
            StartNextActivity(desired, world.Level);
    }

    private ProgressionActionResult StartNextQuest(int currentLevel, ProgressionQuestCandidate quest)
    {
        if (State is null || questProvider is null)
            return new(false, "The quest provider is unavailable.");

        DateTimeOffset now = utcNow();
        TaskId taskId = TaskId.New();
        AttemptId attemptId = AttemptId.New();
        ProgressionQuestTaskPayload payload = new(
            quest.QuestId, quest.Name, currentLevel, quest.RequiredLevel, quest.Kind);
        string questKind = QuestKindName(quest.Kind);
        NexusTask task = new(
            taskId,
            State.Goal.Id,
            RunQuestKind,
            1,
            $"Complete {quest.Name}",
            $"Nexus selected one exact supported {questKind}; the provider executes only that quest and returns control.",
            QuestCapability,
            questProvider.Id,
            QuestResources.ToHashSet(),
            NexusTaskStatus.Ready,
            JsonSerializer.Serialize(payload),
            "Waiting to acquire quest resources.",
            null);

        State = State with
        {
            Tasks = [.. State.Tasks.TakeLast(99), task],
            ActiveTaskId = taskId,
            StopAfterCurrentDuty = false,
            ProviderStartObserved = false,
            DutyEntryObserved = false,
            DutyCompletionObserved = false,
            Goal = State.Goal with
            {
                UpdatedAt = now,
                Status = GoalStatus.Active,
                StatusDetail = $"Preparing the quest {quest.Name}.",
            },
            UpdatedAtUtc = now,
        };
        Save();

        LeaseOwner owner = new(State.Goal.Id, taskId, attemptId, State.Goal.Priority,
            $"Progression: quest {quest.Name}");
        if (!leases.TryAcquire(owner, QuestResources, LeaseLifetime, out activeLease,
                out ResourceLeaseSnapshot? blocking))
        {
            string blocker = blocking is null ? "another task" : blocking.Owner.Reason;
            FailTask(task, FailureKind.ResourceConflict, "quest-resource-conflict",
                $"The quest is waiting because {blocker} owns a required resource.", null, true);
            return new(false, State!.Goal.StatusDetail!);
        }

        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Acquiring,
            StatusDetail = "Resources acquired; asking the quest provider to run the selected quest once.",
        });
        Save();

        ProgressionQuestStartResult start = questProvider.TryStartQuest(quest);
        if (!start.Success)
        {
            if (start.UnsupportedQuest)
            {
                ReplaceTask(State.ActiveTask! with
                {
                    Status = NexusTaskStatus.Cancelled,
                    StatusDetail = start.Message,
                    Failure = new TaskFailure(
                        FailureKind.DependencyUnavailable,
                        "quest-path-unsupported",
                        "The provider does not support this quest; Nexus will choose another eligible activity.",
                        start.Message,
                        false),
                });
                ReleaseLease();
                State = State! with { ActiveTaskId = null };
                Save();
                if (unsupportedQuestFallbackDepth >= MaximumUnsupportedQuestFallbacks)
                {
                    const string reason = "Questionable rejected 24 eligible quest paths in this selection pass. Nexus stopped safely instead of retrying indefinitely.";
                    UpdateGoal(GoalStatus.Blocked, reason, incrementPlanRevision: true);
                    return new(false, reason);
                }

                unsupportedQuestFallbackDepth++;
                try
                {
                    return StartNextActivity(ReadDesiredState(), currentLevel);
                }
                finally
                {
                    unsupportedQuestFallbackDepth--;
                }
            }

            FailTask(State.ActiveTask!, FailureKind.DependencyUnavailable, "quest-provider-start-rejected",
                "The quest provider rejected the exact quest selected by Nexus.", start.Message, true);
            return new(false, start.Message);
        }

        providerStartRequestedAt = now;
        State = State with
        {
            Goal = State.Goal with { StatusDetail = start.Message, UpdatedAt = now },
            UpdatedAtUtc = now,
        };
        Save();
        return new(true, start.Message);
    }

    private void UpdateQuestWork(NexusTask task, ProgressionWorldObservation world)
    {
        if (questProvider is null)
        {
            BeginFailureStop(task, FailureKind.DependencyUnavailable, "quest-provider-unavailable",
                "The quest provider is unavailable.", true);
            return;
        }

        ProgressionQuestTaskPayload payload = JsonSerializer.Deserialize<ProgressionQuestTaskPayload>(task.PayloadJson)
            ?? throw new InvalidDataException("The quest task payload is empty.");
        ProgressionQuestProviderObservation observation = questProvider.ObserveQuest(payload.QuestId);
        if (!observation.IsAvailable || observation.IsRunning is null || observation.IsComplete is null)
        {
            BeginFailureStop(task, FailureKind.DependencyUnavailable, "quest-provider-unavailable",
                "The quest provider became unavailable. Nexus is retaining ownership until inactivity is confirmed.", true);
            return;
        }

        bool matchingQuest = string.Equals(observation.CurrentQuestId, payload.QuestId, StringComparison.Ordinal);
        bool started = State!.ProviderStartObserved || observation.IsRunning == true && matchingQuest;
        if (started != State.ProviderStartObserved)
            State = State with { ProviderStartObserved = started, UpdatedAtUtc = utcNow() };

        if (observation.IsComplete == true)
        {
            if (observation.IsRunning == true)
            {
                if (task.Status != NexusTaskStatus.Verifying)
                {
                    ReplaceTask(task with
                    {
                        Status = NexusTaskStatus.Verifying,
                        StatusDetail = "Quest completion is verified; waiting for the provider to return control.",
                    });
                    Save();
                }
                return;
            }

            CompleteQuest(task, world, payload);
            return;
        }

        if (observation.IsRunning == true && !matchingQuest)
        {
            BeginFailureStop(task, FailureKind.UnsafeState, "quest-provider-mismatch",
                "The quest provider switched to a different quest. Nexus is stopping it before releasing ownership.", true);
            return;
        }

        if (task.Status == NexusTaskStatus.Acquiring)
        {
            if (observation.IsRunning == true && matchingQuest)
            {
                ReplaceTask(task with
                {
                    Status = NexusTaskStatus.Running,
                    StatusDetail = $"The provider is running only {payload.QuestName}; Nexus is monitoring completion.",
                });
                Save();
                return;
            }

            if (providerStartRequestedAt is { } requestedAt && utcNow() - requestedAt >= ProviderStartTimeout)
                FailTask(task, FailureKind.TransientExternal, "quest-provider-start-timeout",
                    "The selected quest did not start in time.", observation.Detail, true);
            else if (started)
                Save();
            return;
        }

        if (observation.IsRunning == true && matchingQuest)
            return;

        FailTask(task, FailureKind.TransientExternal, "quest-ended-without-completion",
            "The quest provider stopped before the selected quest was complete.", observation.Detail, true);
    }

    private void CompleteQuest(
        NexusTask task,
        ProgressionWorldObservation world,
        ProgressionQuestTaskPayload payload)
    {
        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Succeeded,
            StatusDetail = $"Verified completion of {payload.QuestName}.",
            Failure = null,
        });
        ReleaseLease();
        State = State! with { ActiveTaskId = null, ProviderStartObserved = false };

        ReachJobLevelDesiredState desired = ReadDesiredState();
        if (world.Level >= desired.TargetLevel)
        {
            MarkGoalSatisfied(world.Level);
            return;
        }

        if (State.StopAfterCurrentDuty)
        {
            State = State with { StopAfterCurrentDuty = false };
            UpdateGoal(GoalStatus.Paused,
                $"Current activity complete at level {world.Level}. Resume when you want Nexus to continue.",
                incrementPlanRevision: true);
            return;
        }

        UpdateGoal(GoalStatus.Active,
            $"Verified {payload.QuestName}; replanning from level {world.Level}.",
            incrementPlanRevision: true);
        StartNextActivity(desired, world.Level);
    }

    private static string QuestKindName(ProgressionQuestKind kind) => kind switch
    {
        ProgressionQuestKind.ClassJobRole => "Class/Job/Role quest",
        ProgressionQuestKind.GeneralSideQuest => "general side quest",
        ProgressionQuestKind.AetherCurrent => "Aether Current quest",
        ProgressionQuestKind.MainScenario => "Main Scenario quest",
        _ => "quest",
    };

    private ProgressionActionResult StartNextDuty(int currentLevel, ProgressionDutyCandidate duty)
    {
        if (State is null)
            return new(false, "The Progression goal is unavailable.");

        DateTimeOffset now = utcNow();
        TaskId taskId = TaskId.New();
        AttemptId attemptId = AttemptId.New();
        ProgressionDutyTaskPayload payload = new(
            duty.TerritoryId,
            duty.ContentId,
            duty.Name,
            currentLevel,
            duty.RequiredLevel,
            duty.RequiredItemLevel);
        NexusTask task = new(
            taskId,
            State.Goal.Id,
            RunDutyKind,
            1,
            $"Run {duty.Name} once",
            "One bounded duty advances the level goal, then Nexus verifies the result and replans.",
            DutyCapability,
            provider.Id,
            DutyResources.ToHashSet(),
            NexusTaskStatus.Ready,
            JsonSerializer.Serialize(payload),
            "Waiting to acquire duty resources.",
            null);

        NexusTask[] history = [.. State.Tasks.TakeLast(99), task];
        State = State with
        {
            Tasks = history,
            ActiveTaskId = taskId,
            StopAfterCurrentDuty = false,
            ProviderStartObserved = false,
            DutyEntryObserved = false,
            DutyCompletionObserved = false,
            Goal = State.Goal with
            {
                UpdatedAt = now,
                Status = GoalStatus.Active,
                StatusDetail = $"Preparing one bounded run of {duty.Name}.",
            },
            UpdatedAtUtc = now,
        };
        Save();

        LeaseOwner owner = new(State.Goal.Id, taskId, attemptId, State.Goal.Priority,
            $"Progression: one bounded run of {duty.Name}");
        if (!leases.TryAcquire(owner, DutyResources, LeaseLifetime, out activeLease, out ResourceLeaseSnapshot? blocking))
        {
            string blocker = blocking is null
                ? "another task"
                : blocking.Owner.Reason;
            FailTask(task, FailureKind.ResourceConflict, "resource-conflict",
                $"Progression is waiting because {blocker} owns a required resource.", null, true);
            return new(false, State!.Goal.StatusDetail!);
        }

        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Acquiring,
            StatusDetail = "Resources acquired; asking the duty provider to run once.",
        });
        Save();

        if (!provider.TryStart(duty.TerritoryId, out string message))
        {
            FailTask(State.ActiveTask!, FailureKind.DependencyUnavailable, "provider-start-rejected",
                "The duty provider rejected the bounded run.", message, true);
            return new(false, message);
        }

        providerStartRequestedAt = now;
        State = State with
        {
            Goal = State.Goal with { StatusDetail = message, UpdatedAt = now },
            UpdatedAtUtc = now,
        };
        Save();
        return new(true, message);
    }

    private void CompleteDuty(NexusTask task, ProgressionWorldObservation world)
    {
        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Succeeded,
            StatusDetail = $"Verified duty completion and return at level {world.Level}.",
            Failure = null,
        });
        ReleaseLease();

        ReachJobLevelDesiredState desired = ReadDesiredState();
        if (world.Level >= desired.TargetLevel)
        {
            MarkGoalSatisfied(world.Level);
            return;
        }

        if (State!.StopAfterCurrentDuty)
        {
            State = State with { ActiveTaskId = null, StopAfterCurrentDuty = false };
            UpdateGoal(GoalStatus.Paused,
                $"Current activity complete at level {world.Level}. Resume when you want Nexus to plan another bounded activity.",
                incrementPlanRevision: true);
            return;
        }

        State = State with { ActiveTaskId = null };
        UpdateGoal(GoalStatus.Active,
            $"Verified {task.Title}; replanning from level {world.Level}.",
            incrementPlanRevision: true);
        StartBetweenDutyMaintenance(world);
    }

    private void CompleteCancellation(NexusTask task)
    {
        bool pause = pauseAfterStop;
        pauseAfterStop = false;
        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Cancelled,
            StatusDetail = pause
                ? "Provider inactivity confirmed; bounded activity paused."
                : "Provider inactivity confirmed; bounded Progression task cancelled.",
            Failure = new TaskFailure(FailureKind.Cancelled, "user-stop",
                pause ? "The user paused this Progression goal." : "The user stopped this Progression goal.", null, false),
        });
        ReleaseLease();
        State = State! with { ActiveTaskId = null, StopAfterCurrentDuty = false };
        UpdateGoal(pause ? GoalStatus.Paused : GoalStatus.Cancelled,
            pause
                ? "Progression paused. Resume will create a fresh bounded plan; stopped work will not replay."
                : "Progression goal stopped. No additional duty will be scheduled.");
    }

    private void BeginFailureStop(
        NexusTask task,
        FailureKind kind,
        string code,
        string userMessage,
        bool retryable)
    {
        bool requested = TryStopProvider(task, out string stopDetail);
        ReplaceTask(task with
        {
            Status = NexusTaskStatus.NeedsReconciliation,
            StatusDetail = requested
                ? $"{userMessage} Waiting for provider inactivity."
                : $"{userMessage} Stop is not currently available; Nexus will keep checking.",
            Failure = new TaskFailure(kind, code, userMessage, stopDetail, retryable),
        });
        UpdateGoal(GoalStatus.Blocked,
            requested
                ? $"{userMessage} Waiting for provider inactivity before releasing ownership."
                : $"{userMessage} Provider Stop is unavailable; do not start other automated work.");
    }

    private void ReconcileStoppingTask(NexusTask task)
    {
        activeLease?.Heartbeat(LeaseLifetime);
        ProviderOperationObservation observation = ObserveProvider(task);
        if (!observation.IsAvailable || observation.IsInactive is null)
        {
            TryStopProvider(task, out _);
            return;
        }

        if (observation.IsInactive == false)
        {
            TryStopProvider(task, out _);
            return;
        }

        if (task.Status == NexusTaskStatus.Cancelling && task.Failure is null)
        {
            CompleteCancellation(task);
            return;
        }

        TaskFailure failure = task.Failure ?? new TaskFailure(
            FailureKind.UnsafeState,
            "stop-reconciled",
            "The provider was stopped after an unsafe task state.",
            observation.Detail,
            true);
        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Failed,
            StatusDetail = $"{failure.UserMessage} Provider inactivity is now confirmed.",
            Failure = failure,
        });
        ReleaseLease();
        State = State! with { ActiveTaskId = null };
        UpdateGoal(GoalStatus.Blocked, $"{failure.UserMessage} Provider inactivity is confirmed; Resume will create a fresh task.");
    }

    private void FailTask(
        NexusTask task,
        FailureKind kind,
        string code,
        string userMessage,
        string? technicalDetail,
        bool retryable)
    {
        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Failed,
            StatusDetail = userMessage,
            Failure = new TaskFailure(kind, code, userMessage, technicalDetail, retryable),
        });
        ReleaseLease();
        State = State! with { ActiveTaskId = null };
        UpdateGoal(GoalStatus.Blocked, userMessage);
    }

    private void MarkGoalSatisfied(int level)
    {
        State = State! with { ActiveTaskId = null, StopAfterCurrentDuty = false };
        UpdateGoal(GoalStatus.Satisfied,
            $"Target satisfied: the current job is level {level}. Nexus will not schedule another duty.",
            incrementPlanRevision: true);
    }

    private void ReconcileLoadedState()
    {
        if (State?.ActiveTask is not { } task ||
            task.Status is not (NexusTaskStatus.Ready or NexusTaskStatus.Acquiring or
                NexusTaskStatus.Running or NexusTaskStatus.Verifying or NexusTaskStatus.Cancelling or
                NexusTaskStatus.NeedsReconciliation))
            return;

        ReplaceTask(task with
        {
            Status = NexusTaskStatus.NeedsReconciliation,
            StatusDetail = "Nexus reloaded during this task. Stopping the prior provider operation before pausing.",
        });
        UpdateGoal(GoalStatus.Paused,
            "Reload recovery is stopping the prior provider operation. Resume will build a fresh task and will not replay it.");
        reloadStopPending = true;
    }

    private void ReconcileReloadStop()
    {
        NexusTask? activeTask = State?.ActiveTask;
        ProviderOperationObservation observation = ObserveProvider(activeTask);
        if (!observation.IsAvailable || observation.IsInactive is null)
            return;

        if (observation.IsInactive == false)
        {
            TryStopProvider(activeTask, out _);
            return;
        }

        reloadStopPending = false;
        if (State?.ActiveTask is { } task)
        {
            ReplaceTask(task with
            {
                Status = NexusTaskStatus.Cancelled,
                StatusDetail = "Prior provider operation is inactive after reload reconciliation.",
                Failure = new TaskFailure(FailureKind.UserIntervention, "reload-reconciled",
                    "The task was stopped during reload recovery and was not replayed.", null, false),
            });
            State = State with { ActiveTaskId = null };
            Save();
        }

        if (cancelAfterReloadStop)
        {
            cancelAfterReloadStop = false;
            UpdateGoal(GoalStatus.Cancelled,
                "Progression goal cancelled after reload recovery confirmed the provider was inactive.");
        }
    }

    private ReachJobLevelDesiredState ReadDesiredState() => JsonSerializer.Deserialize<ReachJobLevelDesiredState>(
        State!.Goal.DesiredStateJson)
        ?? throw new InvalidDataException("The Progression goal desired state is empty.");

    private readonly record struct ProviderOperationObservation(
        bool IsAvailable,
        bool? IsInactive,
        string Detail);

    private ProviderOperationObservation ObserveProvider(NexusTask? task)
    {
        if (task?.Kind == EnsureGearKind)
        {
            if (gearProvider is null)
                return new(false, null, "The gear-readiness provider is unavailable.");
            ProgressionGearProviderObservation gear = gearProvider.ObserveGearReadiness();
            return new(gear.IsAvailable, gear.IsBusy is null ? null : !gear.IsBusy.Value, gear.Detail);
        }

        if (task?.Kind == RunMaintenanceKind)
        {
            if (maintenanceProvider is null)
                return new(false, null, "The Nexus maintenance provider is unavailable.");
            ProgressionMaintenanceProviderObservation maintenance = maintenanceProvider.ObserveMaintenance();
            return new(maintenance.IsAvailable,
                maintenance.IsBusy is null ? null : !maintenance.IsBusy.Value,
                maintenance.Detail);
        }

        if (task?.Kind == RunQuestKind)
        {
            if (questProvider is null)
                return new(false, null, "The quest provider is unavailable.");
            ProgressionQuestTaskPayload? payload = JsonSerializer.Deserialize<ProgressionQuestTaskPayload>(task.PayloadJson);
            if (payload is null)
                return new(false, null, "The quest task payload is unavailable.");
            ProgressionQuestProviderObservation quest = questProvider.ObserveQuest(payload.QuestId);
            return new(quest.IsAvailable, quest.IsRunning is null ? null : !quest.IsRunning.Value, quest.Detail);
        }

        if (task?.Kind == RunHuntingLogKind)
        {
            if (huntingProvider is null)
                return new(false, null, "The Hunting Log provider is unavailable.");
            ProgressionHuntingTaskPayload? payload = JsonSerializer.Deserialize<ProgressionHuntingTaskPayload>(task.PayloadJson);
            if (payload is null)
                return new(false, null, "The Hunting Log task payload is unavailable.");
            ProgressionHuntingTargetCandidate target = new(
                payload.LogKey,
                payload.LogName,
                payload.Rank,
                payload.TaskIndex,
                payload.MonsterIndex,
                payload.NameId,
                payload.TargetName,
                payload.StartingKilled,
                payload.Required,
                payload.Locations);
            ProgressionHuntingProviderObservation hunt = huntingProvider.ObserveHunt(target);
            return new(hunt.IsAvailable, hunt.IsBusy is null ? null : !hunt.IsBusy.Value, hunt.Detail);
        }

        ProgressionDutyProviderObservation duty = provider.Observe();
        return new(duty.IsAvailable, duty.IsStopped, duty.Detail);
    }

    private bool TryStopProvider(NexusTask? task, out string message)
    {
        if (task?.Kind == EnsureGearKind)
        {
            if (gearProvider is null)
            {
                message = "The gear-readiness Stop contract is unavailable.";
                return false;
            }
            return gearProvider.TryStopGearReadiness(out message);
        }

        if (task?.Kind == RunMaintenanceKind)
        {
            if (maintenanceProvider is null)
            {
                message = "The Nexus maintenance Stop contract is unavailable.";
                return false;
            }
            return maintenanceProvider.TryStopMaintenance(out message);
        }

        if (task?.Kind == RunQuestKind)
        {
            if (questProvider is null)
            {
                message = "The quest provider Stop contract is unavailable.";
                return false;
            }
            return questProvider.TryStopQuest(out message);
        }

        if (task?.Kind == RunHuntingLogKind)
        {
            if (huntingProvider is null)
            {
                message = "The Hunting Log Stop contract is unavailable.";
                return false;
            }
            return huntingProvider.TryStopHunt(out message);
        }

        return provider.TryStop(out message);
    }

    private void ReplaceTask(NexusTask replacement)
    {
        State = State! with
        {
            Tasks = State.Tasks.Select(task => task.Id == replacement.Id ? replacement : task).ToArray(),
            UpdatedAtUtc = utcNow(),
        };
    }

    private void UpdateGoal(GoalStatus status, string detail, bool incrementPlanRevision = false)
    {
        DateTimeOffset now = utcNow();
        State = State! with
        {
            Goal = State.Goal with
            {
                Status = status,
                StatusDetail = detail,
                UpdatedAt = now,
                PlanRevision = State.Goal.PlanRevision + (incrementPlanRevision ? 1 : 0),
            },
            UpdatedAtUtc = now,
        };
        Save();
    }

    private void ReleaseLease()
    {
        activeLease?.Dispose();
        activeLease = null;
        providerStartRequestedAt = null;
    }

    private void Save()
    {
        if (State is not null)
            store.Save(State);
    }
}
