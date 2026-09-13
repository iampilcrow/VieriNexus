using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using VieriNexus.Application;
using VieriNexus.Contracts;
using VieriNexus.Domain;
using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace VieriNexus.Services;

internal sealed class NexusIpcProvider : IDisposable
{
    private readonly ICallGateProvider<NexusStatusDto> statusProvider;
    private readonly ICallGateProvider<NexusCommandDto, NexusCommandResultDto> commandProvider;
    private readonly ICallGateProvider<NexusOperationsStatusDto> operationsStatusProvider;
    private readonly ICallGateProvider<DependencyDto[]> dependencyProvider;
    private readonly ICallGateProvider<int> navigationVersionProvider;
    private readonly ICallGateProvider<NavigationLibraryStatusDto> navigationStatusProvider;
    private readonly ICallGateProvider<string> navigationListProvider;
    private readonly ICallGateProvider<string, string?> navigationGetProvider;
    private readonly ICallGateProvider<uint, uint, string> navigationResolveVendorProvider;
    private readonly ICallGateProvider<NavigationActivationStatusDto> navigationActivationProvider;
    private readonly ICallGateProvider<string> linkStatusProvider;
    private readonly ICallGateProvider<string, string, string> linkCommandProvider;
    private readonly DependencyService dependencies;
    private readonly NavigationLibraryService navigation;
    private readonly NavigationActivationService navigationActivation;
    private readonly ProgressionRuntimeService progression;
    private readonly ProgressionProviderService progressionProviders;
    private readonly WorldStateStore world;
    private readonly NexusControlService control;
    private readonly ICondition condition;
    private NexusOperationsStatusDto? previousLinkStatus;
    private long queueReadySequence;
    private int completedDuties;
    private int deathsThisDuty;
    private DateTimeOffset? activeSinceUtc;
    private DateTimeOffset? dutyStartedUtc;
    private long lastDutyDurationSeconds;
    private string lastCompletedDuty = string.Empty;

    internal NexusIpcProvider(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies,
        NavigationLibraryService navigation,
        NavigationActivationService navigationActivation,
        ProgressionRuntimeService progression,
        ProgressionProviderService progressionProviders,
        WorldStateStore world,
        NexusControlService control,
        ICondition condition)
    {
        this.dependencies = dependencies;
        this.navigation = navigation;
        this.navigationActivation = navigationActivation;
        this.progression = progression;
        this.progressionProviders = progressionProviders;
        this.world = world;
        this.control = control;
        this.condition = condition;
        statusProvider = pluginInterface.GetIpcProvider<NexusStatusDto>(NexusIpc.GetStatus);
        commandProvider = pluginInterface.GetIpcProvider<NexusCommandDto, NexusCommandResultDto>(NexusIpc.ExecuteCommand);
        operationsStatusProvider = pluginInterface.GetIpcProvider<NexusOperationsStatusDto>(NexusIpc.GetOperationsStatus);
        dependencyProvider = pluginInterface.GetIpcProvider<DependencyDto[]>(NexusIpc.GetDependencies);
        navigationVersionProvider = pluginInterface.GetIpcProvider<int>(NexusIpc.GetNavigationApiVersion);
        navigationStatusProvider = pluginInterface.GetIpcProvider<NavigationLibraryStatusDto>(NexusIpc.GetNavigationStatus);
        navigationListProvider = pluginInterface.GetIpcProvider<string>(NexusIpc.ListNavigationRoutes);
        navigationGetProvider = pluginInterface.GetIpcProvider<string, string?>(NexusIpc.GetNavigationRoute);
        navigationResolveVendorProvider = pluginInterface.GetIpcProvider<uint, uint, string>(NexusIpc.ResolveGearVendorOverride);
        navigationActivationProvider = pluginInterface.GetIpcProvider<NavigationActivationStatusDto>(NexusIpc.GetNavigationActivationStatus);
        linkStatusProvider = pluginInterface.GetIpcProvider<string>("VieriNexus.Link.V1.GetStatus");
        linkCommandProvider = pluginInterface.GetIpcProvider<string, string, string>("VieriNexus.Link.V1.Execute");
        statusProvider.RegisterFunc(GetStatus);
        commandProvider.RegisterFunc(control.Execute);
        operationsStatusProvider.RegisterFunc(control.Status);
        dependencyProvider.RegisterFunc(GetDependencies);
        navigationVersionProvider.RegisterFunc(() => NexusIpc.CurrentVersion);
        navigationStatusProvider.RegisterFunc(GetNavigationStatus);
        navigationListProvider.RegisterFunc(ListNavigationRoutes);
        navigationGetProvider.RegisterFunc(GetNavigationRoute);
        navigationResolveVendorProvider.RegisterFunc(ResolveGearVendorOverride);
        navigationActivationProvider.RegisterFunc(GetNavigationActivationStatus);
        linkStatusProvider.RegisterFunc(GetLinkStatus);
        linkCommandProvider.RegisterFunc(ExecuteLinkCommand);
    }

    internal void UpdateLinkTelemetry()
    {
        NexusOperationsStatusDto current = control.Status();
        NexusOperationsStatusDto? previous = previousLinkStatus;
        if (current.ActiveModule is not null && previous?.ActiveModule is null)
            activeSinceUtc = DateTimeOffset.UtcNow;
        else if (current.ActiveModule is null)
            activeSinceUtc = null;

        if (current.IsInDuty && previous?.IsInDuty != true)
        {
            dutyStartedUtc = DateTimeOffset.UtcNow;
            deathsThisDuty = 0;
        }
        if (!current.IsInDuty && previous?.IsInDuty == true)
            dutyStartedUtc = null;
        if (current.IsInDutyQueue && previous?.IsInDutyQueue != true)
            queueReadySequence++;
        if (condition[ConditionFlag.Unconscious] && previous is not null &&
            !PreviousWasUnconscious && current.IsInDuty)
            deathsThisDuty++;

        int newCompletions = Math.Max(0, current.CompletedDuties - (previous?.CompletedDuties ?? 0));
        if (newCompletions > 0)
        {
            completedDuties += newCompletions;
            lastCompletedDuty = current.LastCompletedActivity ?? previous?.Activity ?? string.Empty;
            lastDutyDurationSeconds = dutyStartedUtc is { } started
                ? Math.Max(0, (long)(DateTimeOffset.UtcNow - started).TotalSeconds)
                : 0;
        }
        PreviousWasUnconscious = condition[ConditionFlag.Unconscious];
        previousLinkStatus = current;
    }

    private bool PreviousWasUnconscious { get; set; }

    private NexusStatusDto GetStatus()
    {
        var snapshot = world.Current;
        var ready = dependencies.RequiredReady && snapshot.Session.IsLoggedIn && snapshot.Session.IsPlayerAvailable;
        ProgressionGoalState? progressionState = progression.State;
        NexusTask? activeTask = progressionState?.ActiveTask;
        bool paused = progressionState?.Goal.Status == GoalStatus.Paused;
        string state = progressionState?.Goal.Status.ToString() ?? (ready ? "Idle" : "SetupRequired");
        return new NexusStatusDto(
            NexusIpc.CurrentVersion,
            ready,
            paused,
            state,
            progressionState?.Goal.Title,
            activeTask?.Title,
            progressionState?.Goal.StatusDetail ??
                (ready ? "No active goal." : "Waiting for required dependencies and a ready character."),
            activeTask?.Provider?.Value,
            progressionState?.Goal.Status == GoalStatus.Active
                ? "Verify the current bounded activity, then replan from current character state."
                : null,
            progressionState?.Goal.Status == GoalStatus.Blocked
                ? progressionState.Goal.StatusDetail
                : ready ? null : "Complete the Dependencies page.",
            snapshot.Revision);
    }

    private DependencyDto[] GetDependencies() => dependencies.Snapshot().Select(status => new DependencyDto(
        status.Definition.Id,
        status.Definition.DisplayName,
        status.Health.ToString(),
        status.Version,
        status.Definition.Required,
        status.Definition.Capability,
        status.Definition.Description)).ToArray();

    private NavigationLibraryStatusDto GetNavigationStatus()
    {
        NavigationLibrarySnapshot? snapshot = navigation.Current;
        NavigationActivationAssessment activation = navigationActivation.Assess();
        return new NavigationLibraryStatusDto(
            NexusIpc.CurrentVersion,
            snapshot is not null,
            activation.State == NavigationActivationState.Active,
            activation.IsSourcePluginAuthoritative,
            snapshot?.Routes.Count ?? 0,
            snapshot?.Routes.Count(route => route.OverrideEnabled) ?? 0,
            snapshot is null ? "NotStaged" : navigation.HasWorkingLibrary ? "NexusWorkingLibrary" : "ReadOnlyStaged",
            snapshot is null
                ? "No verified staged route library is available."
                : navigation.HasWorkingLibrary
                    ? "The Nexus working route library is available."
                    : activation.IsSourcePluginAuthoritative
                    ? "Verified staged route data is available read-only; VieriNavPlotter remains authoritative."
                    : "Verified staged route data is available read-only; Nexus navigation execution is disabled.");
    }

    private string ListNavigationRoutes()
    {
        NavigationLibrarySnapshot? snapshot = navigation.Current;
        if (snapshot is null)
            return "[]";

        NavigationRouteListEntryDto[] routes = snapshot.Routes.Select(route => new NavigationRouteListEntryDto(
            route.Id, route.Name, route.TerritoryId, route.Points.Count, route.Notes, route.Tags)).ToArray();
        return NavigationContractJson.SerializeRouteList(routes);
    }

    private string? GetNavigationRoute(string nameOrId)
    {
        NavigationLibrarySnapshot? snapshot = navigation.Current;
        NavigationRouteSnapshot? route = snapshot is null
            ? null
            : NavigationLibraryQuery.Find(snapshot, nameOrId);
        if (route is null)
            return null;

        return NavigationContractJson.SerializeRoute(ToDto(route));
    }

    private string ResolveGearVendorOverride(uint territoryId, uint targetDataId)
    {
        NavigationActivationAssessment activation = navigationActivation.Assess();
        if (activation.State != NavigationActivationState.Active)
        {
            return NavigationContractJson.SerializeRouteResolution(new NavigationRouteResolutionDto(
                false,
                "navigation-not-authoritative",
                "Nexus route overrides are unavailable until Nexus owns navigation for this session.",
                null));
        }
        NavigationRouteOverrideResolution result = NavigationRouteOverrideResolver.ResolveGearVendor(
            navigation.Current, territoryId, targetDataId);
        return NavigationContractJson.SerializeRouteResolution(new NavigationRouteResolutionDto(
            result.Success,
            result.Code,
            result.Message,
            result.Route is null ? null : ToDto(result.Route)));
    }

    private static NavigationRouteDto ToDto(NavigationRouteSnapshot route) => new(
            route.Id,
            route.Name,
            route.TerritoryId,
            route.Points.Select(point => new NavigationRoutePointDto(point.X, point.Y, point.Z)).ToArray(),
            route.Notes,
            route.Tags,
            route.UseMesh,
            route.UseFlight,
            route.Tolerance,
            route.LastPointTolerance,
            route.BindingKind,
            route.TargetDataId,
            route.TargetLabel,
            route.OverrideEnabled,
            route.UpdatedAtUtc);

    private NavigationActivationStatusDto GetNavigationActivationStatus()
    {
        NavigationActivationAssessment assessment = navigationActivation.Assess();
        bool executionEnabled = assessment.State == NavigationActivationState.Active;
        return new NavigationActivationStatusDto(
            NexusIpc.CurrentVersion,
            assessment.State.ToString(),
            assessment.CanActivate,
            executionEnabled,
            assessment.IsSourcePluginInstalled,
            assessment.IsSourcePluginLoaded,
            assessment.IsSourcePluginAuthoritative,
            assessment.Blockers.Select(blocker =>
                new NavigationActivationBlockerDto(blocker.Code, blocker.Message)).ToArray());
    }

    private string GetLinkStatus()
    {
        NexusOperationsStatusDto status = control.Status();
        ProgressionGearProviderObservation gear = progressionProviders.ObserveGearReadiness();
        return JsonSerializer.Serialize(new
        {
            Available = status.IsAvailable,
            ContentId = status.ContentId,
            status.Character,
            status.Location,
            status.Job,
            status.Level,
            status.ItemLevel,
            Duty = status.Activity ?? string.Empty,
            Stage = status.ActiveModule ?? string.Empty,
            status.State,
            Action = status.Detail ?? string.Empty,
            InCombat = status.IsInCombat,
            status.InventoryUsed,
            status.InventoryTotal,
            status.DurabilityPercent,
            RuntimeSeconds = activeSinceUtc is { } started
                ? Math.Max(0, (long)(DateTimeOffset.UtcNow - started).TotalSeconds)
                : 0,
            IsLooping = status.ActiveModule == "Progression",
            IsPaused = status.State.Equals("Paused", StringComparison.OrdinalIgnoreCase),
            IsStopped = status.ActiveModule is null,
            status.IsInDutyQueue,
            QueueReadySequence = queueReadySequence,
            DutiesCompleted = completedDuties,
            DeathsThisDuty = deathsThisDuty,
            LastCompletedDuty = lastCompletedDuty,
            LastDutyDurationSeconds = lastDutyDurationSeconds,
            GearShoppingStartedSequence = gear.StartedSequence,
            GearShoppingCompletedSequence = gear.CompletedSequence,
            GearShoppingInProgress = gear.IsBusy == true,
        });
    }

    private string ExecuteLinkCommand(string command, string argument)
    {
        string normalized = command.Trim().ToLowerInvariant();
        NexusCommandResultDto result = normalized switch
        {
            "start" => control.ExecuteLocal("progression.start"),
            "resume" => control.ExecuteLocal("progression.resume"),
            "stop" or "pause" => control.ExecuteLocal("stop"),
            "sell" => control.ExecuteLocal("maintenance.sell-review"),
            "repair" => control.ExecuteLocal("maintenance.repair"),
            "job" => control.ExecuteLocal("job.switch", JsonSerializer.Serialize(new { job = argument })),
            "leave" => new NexusCommandResultDto(Guid.NewGuid(), false, "leave-review-required",
                "Nexus stopped automatic leave commands during migration; use Stop, then leave the duty normally when safe."),
            "loops" => new NexusCommandResultDto(Guid.NewGuid(), false, "queue-owned",
                "Duty repetition is owned by the Nexus Job Queue; update the queue in Nexus."),
            "inn" => new NexusCommandResultDto(Guid.NewGuid(), false, "route-review-required",
                "Choose the saved inn route on the Nexus Routes page so the destination is explicit."),
            _ => new NexusCommandResultDto(Guid.NewGuid(), false, "unsupported-link-command",
                $"Nexus does not expose the '{normalized}' remote command."),
        };
        return result.Message;
    }

    public void Dispose()
    {
        linkCommandProvider.UnregisterFunc();
        linkStatusProvider.UnregisterFunc();
        navigationActivationProvider.UnregisterFunc();
        navigationResolveVendorProvider.UnregisterFunc();
        navigationGetProvider.UnregisterFunc();
        navigationListProvider.UnregisterFunc();
        navigationStatusProvider.UnregisterFunc();
        navigationVersionProvider.UnregisterFunc();
        operationsStatusProvider.UnregisterFunc();
        commandProvider.UnregisterFunc();
        statusProvider.UnregisterFunc();
        dependencyProvider.UnregisterFunc();
    }
}
