using Dalamud.Plugin.Services;
using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

/// <summary>
/// Character-aware plugin edge for the durable Progression coordinator. A temporary missing
/// character during zoning keeps the same coordinator; an actual character change stops the old
/// owner before a different character's state is loaded.
/// </summary>
internal sealed class ProgressionRuntimeService
{
    private readonly string stateDirectory;
    private readonly ResourceLeaseManager leases;
    private readonly ProgressionProviderService provider;
    private readonly NexusHuntingLogService huntingLog;
    private readonly IPluginLog log;
    private readonly IDutyState dutyState;
    private ProgressionExecutionCoordinator? coordinator;
    private CharacterKey coordinatorCharacter;
    private ProgressionWorldObservation? lastWorld;

    internal ProgressionRuntimeService(
        string pluginConfigurationDirectory,
        ResourceLeaseManager leases,
        ProgressionProviderService provider,
        NexusHuntingLogService huntingLog,
        IDutyState dutyState,
        IPluginLog log)
    {
        stateDirectory = Path.Combine(pluginConfigurationDirectory, "NexusData", "progression");
        this.leases = leases;
        this.provider = provider;
        this.huntingLog = huntingLog;
        this.dutyState = dutyState;
        this.log = log;
        dutyState.DutyCompleted += OnDutyCompleted;
    }

    internal ProgressionGoalState? State => coordinator?.State;

    internal string? LoadError { get; private set; }

    internal IReadOnlyList<ProgressionDutyCandidate> EligibleDuties(int currentLevel) =>
        provider.EligibleDuties(currentLevel);

    internal IReadOnlyList<ProgressionQuestCandidate> EligibleQuests(
        uint classJobId,
        int currentLevel,
        bool includeClassJobRole,
        bool includeGeneralSideQuests) => provider.EligibleQuests(
            classJobId, currentLevel, includeClassJobRole, includeGeneralSideQuests);

    internal bool IsGearReadinessReady => provider.IsGearReadinessReady;

    internal bool IsHuntingLogReady => huntingLog.IsReady;

    internal string HuntingLogReadinessDetail => huntingLog.ReadinessDetail;

    internal IReadOnlyList<ProgressionHuntingTargetCandidate> EligibleHuntingTargets(
        uint classJobId,
        int currentLevel) => huntingLog.EligibleTargets(classJobId, currentLevel);

    internal ProgressionCharacterMetrics CurrentMetrics => provider.CharacterMetrics();

    internal void Update(CharacterSnapshot? character, bool isInDuty)
    {
        if (character is { Key.IsKnown: true })
        {
            EnsureCoordinator(character.Key);
            ProgressionCharacterMetrics metrics = provider.CharacterMetrics();
            lastWorld = new ProgressionWorldObservation(
                character.Key,
                character.ClassJobId,
                character.Level,
                true,
                isInDuty,
                metrics.ItemLevel,
                metrics.Gil);
        }
        else if (lastWorld is { } prior)
        {
            lastWorld = prior with { IsAvailable = false, IsInDuty = isInDuty };
        }

        if (coordinator is not null && lastWorld is { } world)
            coordinator.Update(world);
    }

    internal ProgressionActionResult Start(ReachJobLevelGoalDraft draft, ReachJobLevelPlan plan)
    {
        EnsureCoordinator(draft.Character);
        return coordinator?.Start(draft, plan)
            ?? new ProgressionActionResult(false, LoadError ?? "Progression state is unavailable.");
    }

    internal ProgressionActionResult StartConfigured(
        CharacterSnapshot? character,
        ProgressionDraftConfiguration configuration,
        bool allowAutomation)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (character is null || !character.Key.IsKnown)
            return new ProgressionActionResult(false, "A logged-in character is required to start Progression.");
        if (!allowAutomation)
            return new ProgressionActionResult(false, "Automation is disabled for this character in Nexus settings.");
        if (configuration.TargetLevel == 0)
            configuration.TargetLevel = Math.Min(
                ReachJobLevelPlanner.MaximumSupportedLevel,
                Math.Max(1, character.Level + 2));

        ProgressionCharacterMetrics metrics = CurrentMetrics;
        ReachJobLevelGoalDraft draft = new(
            character.Key,
            character.ClassJobId,
            character.Level,
            configuration.TargetLevel,
            configuration.AllowJobQuests,
            configuration.AllowHuntingLog,
            configuration.AllowSideQuests,
            configuration.AllowDuties,
            configuration.MinimumGilReserve,
            metrics.ItemLevel,
            metrics.Gil);
        ProgressionProviderSnapshot providers = provider.Snapshot();
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            draft,
            providers.Questing,
            providers.Duties,
            IsHuntingLogReady,
            HuntingLogReadinessDetail);
        bool hasEligibleActivity =
            configuration.AllowDuties && EligibleDuties(character.Level).Count > 0 ||
            (configuration.AllowJobQuests || configuration.AllowSideQuests) && EligibleQuests(
                character.ClassJobId,
                character.Level,
                configuration.AllowJobQuests,
                configuration.AllowSideQuests).Count > 0 ||
            configuration.AllowHuntingLog && EligibleHuntingTargets(
                character.ClassJobId,
                character.Level).Count > 0;
        if (!plan.IsValid || plan.IsSatisfied || !plan.IsExecutionConnected || !IsGearReadinessReady ||
            !hasEligibleActivity)
            return new ProgressionActionResult(false,
                plan.Issues.FirstOrDefault(issue => issue.Severity == ProgressionPlanIssueSeverity.Blocker)?.Message ??
                plan.Issues.FirstOrDefault()?.Message ??
                (plan.IsSatisfied ? "The configured level goal is already satisfied." :
                    "No executable progression activity is ready for the configured goal."));
        return Start(draft, plan);
    }

    internal ProgressionActionResult Resume() => coordinator is not null && lastWorld is { } world
        ? coordinator.Resume(world)
        : new ProgressionActionResult(false, LoadError ?? "The current character is unavailable.");

    internal ProgressionActionResult StopAfterCurrentDuty() => coordinator?.StopAfterCurrentDuty()
        ?? new ProgressionActionResult(false, "No Progression goal is loaded.");

    internal ProgressionActionResult StopNow() => coordinator?.StopNow()
        ?? new ProgressionActionResult(false, "No Progression goal is loaded.");

    internal void Shutdown()
    {
        dutyState.DutyCompleted -= OnDutyCompleted;
        coordinator?.Shutdown();
    }

    private void OnDutyCompleted(Dalamud.Game.DutyState.IDutyStateEventArgs args) =>
        coordinator?.ObserveDutyCompletion(args.TerritoryType.RowId);

    private void EnsureCoordinator(CharacterKey character)
    {
        if (!character.IsKnown || coordinatorCharacter == character && coordinator is not null)
            return;

        if (coordinator is not null && coordinator.State?.Goal.Status == GoalStatus.Active)
            coordinator.Shutdown();

        coordinator = null;
        coordinatorCharacter = character;
        LoadError = null;
        string path = Path.Combine(stateDirectory, $"{character.ContentId:X16}-{character.HomeWorldId}.v1.json");
        try
        {
            coordinator = new ProgressionExecutionCoordinator(
                new FileProgressionGoalStore(path),
                leases,
                provider,
                provider,
                provider,
                huntingLog);
        }
        catch (Exception ex)
        {
            LoadError = "The saved Progression goal could not be validated. Automation remains blocked.";
            log.Error(ex, "Failed to load character-scoped Progression goal state from {Path}.", path);
        }
    }
}
