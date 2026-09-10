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

public sealed record ReachJobLevelDesiredState(
    uint ClassJobId,
    int TargetLevel,
    bool AllowJobQuests,
    bool AllowHuntingLog,
    bool AllowSideQuests,
    bool AllowDuties,
    int MinimumGilReserve);

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
/// Owns one durable Reach Job Level goal and dispatches one bounded gear or duty provider task at
/// a time. Gear completion requires spending-floor/item-level postconditions; duty completion
/// requires observed entry plus return to the normal world. Reload can only Stop/pause, never replay.
/// </summary>
public sealed class ProgressionExecutionCoordinator
{
    private static readonly GoalKind ReachJobLevelKind = new("vieri.progression.reach-job-level/v1");
    private static readonly TaskKind EnsureGearKind = new("vieri.gear.ensure-readiness/v1");
    private static readonly TaskKind RunDutyKind = new("vieri.duties.run-one/v1");
    private static readonly CapabilityId GearCapability = new("vieri.capability.gear.ensure-readiness/v1");
    private static readonly CapabilityId DutyCapability = new("vieri.capability.duty.run/v1");
    private static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProviderStartTimeout = TimeSpan.FromSeconds(45);
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

    private readonly IProgressionGoalStore store;
    private readonly ResourceLeaseManager leases;
    private readonly IProgressionDutyProvider provider;
    private readonly IProgressionGearProvider? gearProvider;
    private readonly Func<DateTimeOffset> utcNow;
    private ResourceLeaseHandle? activeLease;
    private DateTimeOffset? providerStartRequestedAt;
    private bool reloadStopPending;
    private bool cancelAfterReloadStop;

    public ProgressionExecutionCoordinator(
        IProgressionGoalStore store,
        ResourceLeaseManager leases,
        IProgressionDutyProvider provider,
        IProgressionGearProvider? gearProvider = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.store = store;
        this.leases = leases;
        this.provider = provider;
        this.gearProvider = gearProvider;
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
        if (!draft.AllowDuties)
            return new(false, "The first executable Progression lane requires Duties to be enabled.");
        if (State?.Goal.Status is GoalStatus.Active)
            return new(false, "A Progression goal is already active.");
        if (State?.Goal.Status is GoalStatus.Paused or GoalStatus.Blocked)
            return new(false, "Resume or cancel the saved Progression goal before starting a different one.");

        ProgressionDutyCandidate? duty = LevelingDutyPolicy.SelectHighest(
            provider.EligibleDuties(draft.CurrentLevel), draft.CurrentLevel);
        if (duty is null && gearProvider is null)
            return new(false, "No unlocked leveling duty currently meets the job, item-level, and provider-path requirements.");

        DateTimeOffset now = utcNow();
        GoalId goalId = GoalId.New();
        ReachJobLevelDesiredState desired = new(
            draft.ClassJobId,
            draft.TargetLevel,
            draft.AllowJobQuests,
            draft.AllowHuntingLog,
            draft.AllowSideQuests,
            draft.AllowDuties,
            draft.MinimumGilReserve);
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
                ? $"Preparing one bounded run of {duty!.Name}."
                : "Preparing a Nexus-owned gear-readiness check before the next duty.");
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
            ? StartNextDuty(draft.CurrentLevel, duty!)
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

        ProgressionDutyCandidate? duty = LevelingDutyPolicy.SelectHighest(
            provider.EligibleDuties(world.Level), world.Level);
        return duty is null
            ? new(false, "No eligible leveling duty is currently available.")
            : StartNextDuty(world.Level, duty);
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
                StatusDetail = "Last Run armed. Nexus will pause this goal after the current duty.",
            },
            UpdatedAtUtc = utcNow(),
        };
        Save();
        return new(true, State.Goal.StatusDetail!);
    }

    public ProgressionActionResult StopNow()
    {
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
        ReplaceTask(task with
        {
            Status = requested ? NexusTaskStatus.Cancelling : NexusTaskStatus.NeedsReconciliation,
            StatusDetail = requested ? "Stop requested; waiting for the provider to confirm inactivity." : message,
            Failure = requested ? null : new TaskFailure(
                FailureKind.DependencyUnavailable,
                "provider-stop-unavailable",
                "Nexus could not confirm that the duty provider stopped.",
                message,
                true),
        });
        UpdateGoal(requested ? GoalStatus.Active : GoalStatus.Blocked,
            requested ? "Stopping the Nexus-owned duty before cancelling the goal." :
                "Stop could not be confirmed. Do not start other automated work yet.");
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

        if (activeLease is null || !activeLease.Heartbeat(LeaseLifetime))
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
            "Nexus owns the spending floor and transaction while the migration provider performs the approved vendor/equip mechanics.",
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
        UpdateGoal(GoalStatus.Active, "Gear readiness verified; selecting one eligible duty.");

        ProgressionDutyCandidate? duty = LevelingDutyPolicy.SelectHighest(
            provider.EligibleDuties(currentLevel: world.Level), world.Level);
        if (duty is null)
        {
            UpdateGoal(GoalStatus.Blocked,
                "Gear readiness finished, but no unlocked duty meets the current level, item-level, and provider-path requirements.",
                incrementPlanRevision: true);
            return;
        }
        StartNextDuty(world.Level, duty);
    }

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
                $"Last Run complete at level {world.Level}. Resume when you want Nexus to plan another bounded duty.",
                incrementPlanRevision: true);
            return;
        }

        State = State with { ActiveTaskId = null };
        UpdateGoal(GoalStatus.Active,
            $"Verified {task.Title}; replanning from level {world.Level}.",
            incrementPlanRevision: true);
        if (gearProvider is not null)
        {
            StartGearReadiness(world.Level, desired.MinimumGilReserve, world.ItemLevel, world.Gil);
            return;
        }

        ProgressionDutyCandidate? next = LevelingDutyPolicy.SelectHighest(
            provider.EligibleDuties(world.Level), world.Level);
        if (next is null)
        {
            UpdateGoal(GoalStatus.Blocked,
                "The duty finished, but no eligible next leveling duty is available. Check unlocks, item level, and provider health.",
                incrementPlanRevision: true);
            return;
        }
        StartNextDuty(world.Level, next);
    }

    private void CompleteCancellation(NexusTask task)
    {
        ReplaceTask(task with
        {
            Status = NexusTaskStatus.Cancelled,
            StatusDetail = "Provider inactivity confirmed; bounded Progression task cancelled.",
            Failure = new TaskFailure(FailureKind.Cancelled, "user-stop",
                "The user stopped this Progression goal.", null, false),
        });
        ReleaseLease();
        State = State! with { ActiveTaskId = null, StopAfterCurrentDuty = false };
        UpdateGoal(GoalStatus.Cancelled, "Progression goal stopped. No additional duty will be scheduled.");
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
