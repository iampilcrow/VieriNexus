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
    private readonly IPluginLog log;
    private readonly IDutyState dutyState;
    private ProgressionExecutionCoordinator? coordinator;
    private CharacterKey coordinatorCharacter;
    private ProgressionWorldObservation? lastWorld;

    internal ProgressionRuntimeService(
        string pluginConfigurationDirectory,
        ResourceLeaseManager leases,
        ProgressionProviderService provider,
        IDutyState dutyState,
        IPluginLog log)
    {
        stateDirectory = Path.Combine(pluginConfigurationDirectory, "NexusData", "progression");
        this.leases = leases;
        this.provider = provider;
        this.dutyState = dutyState;
        this.log = log;
        dutyState.DutyCompleted += OnDutyCompleted;
    }

    internal ProgressionGoalState? State => coordinator?.State;

    internal string? LoadError { get; private set; }

    internal IReadOnlyList<ProgressionDutyCandidate> EligibleDuties(int currentLevel) =>
        provider.EligibleDuties(currentLevel);

    internal void Update(CharacterSnapshot? character, bool isInDuty)
    {
        if (character is { Key.IsKnown: true })
        {
            EnsureCoordinator(character.Key);
            lastWorld = new ProgressionWorldObservation(
                character.Key,
                character.ClassJobId,
                character.Level,
                true,
                isInDuty);
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
                provider);
        }
        catch (Exception ex)
        {
            LoadError = "The saved Progression goal could not be validated. Automation remains blocked.";
            log.Error(ex, "Failed to load character-scoped Progression goal state from {Path}.", path);
        }
    }
}
