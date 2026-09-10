using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

/// <summary>
/// Read-only capability probe for the temporary Vieri providers and their intended stock replacements.
/// Merely having the expected plugin name is not enough: every required IPC member must be present.
/// No provider function is invoked by this service.
/// </summary>
internal sealed class ProgressionProviderService
{
    private static readonly ProviderId CodexProviderId = new("vieri.provider.codex-compat/v1");
    private static readonly ProviderId QuestionableProviderId = new("vieri.provider.questionable-stock/v1");
    private static readonly ProviderId AutoDutyCompatibilityProviderId = new("vieri.provider.autoduty-compat/v1");
    private static readonly ProviderId AutoDutyStockProviderId = new("vieri.provider.autoduty-stock/v1");

    private readonly DependencyService dependencies;
    private readonly ICallGateSubscriber<bool> codexIsRunning;
    private readonly ICallGateSubscriber<string, bool> codexStartSingleQuest;
    private readonly ICallGateSubscriber<string, bool> codexStop;
    private readonly ICallGateSubscriber<bool> questionableIsRunning;
    private readonly ICallGateSubscriber<string, bool> questionableStartSingleQuest;
    private readonly ICallGateSubscriber<string, bool> questionableStop;
    private readonly ICallGateSubscriber<uint, bool> autoDutyContentHasPath;
    private readonly ICallGateSubscriber<bool> autoDutyIsStopped;
    private readonly ICallGateSubscriber<uint, int, bool, object> autoDutyRun;
    private readonly ICallGateSubscriber<object> autoDutyStop;
    private readonly ICallGateSubscriber<int, string> vieriAutoDutyProgression;

    internal ProgressionProviderService(
        IDalamudPluginInterface pluginInterface,
        DependencyService dependencies)
    {
        this.dependencies = dependencies;
        codexIsRunning = pluginInterface.GetIpcSubscriber<bool>("VieriCodex.IsRunning");
        codexStartSingleQuest = pluginInterface.GetIpcSubscriber<string, bool>("VieriCodex.StartSingleQuest");
        codexStop = pluginInterface.GetIpcSubscriber<string, bool>("VieriCodex.Stop");
        questionableIsRunning = pluginInterface.GetIpcSubscriber<bool>("Questionable.IsRunning");
        questionableStartSingleQuest = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.StartSingleQuest");
        questionableStop = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.Stop");
        autoDutyContentHasPath = pluginInterface.GetIpcSubscriber<uint, bool>("AutoDuty.ContentHasPath");
        autoDutyIsStopped = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
        autoDutyRun = pluginInterface.GetIpcSubscriber<uint, int, bool, object>("AutoDuty.Run");
        autoDutyStop = pluginInterface.GetIpcSubscriber<object>("AutoDuty.Stop");
        vieriAutoDutyProgression = pluginInterface.GetIpcSubscriber<int, string>("AutoDuty.StartProgressionLeveling");
    }

    internal ProgressionProviderSnapshot Snapshot()
    {
        ProgressionProviderCandidate[] dutyCandidates = DutyCandidates();
        ProgressionProviderCandidate[] candidates =
        [
            QuestCandidate(
                "VieriCodex",
                "VieriCodex",
                CodexProviderId,
                ProgressionProviderFlavor.VieriCompatibility,
                codexIsRunning.HasFunction && codexStartSingleQuest.HasFunction && codexStop.HasFunction),
            QuestCandidate(
                "Questionable",
                "Questionable",
                QuestionableProviderId,
                ProgressionProviderFlavor.Stock,
                questionableIsRunning.HasFunction && questionableStartSingleQuest.HasFunction && questionableStop.HasFunction),
            .. dutyCandidates,
        ];

        return new ProgressionProviderSnapshot(
            ProgressionProviderPolicy.Select(ProgressionProviderRole.Questing, candidates),
            ProgressionProviderPolicy.Select(ProgressionProviderRole.Duties, candidates));
    }

    internal IReadOnlyDictionary<ProviderId, ProviderHealthSnapshot> ProviderHealth()
    {
        ProgressionProviderSnapshot snapshot = Snapshot();
        return snapshot.Questing.Candidates
            .Concat(snapshot.Duties.Candidates)
            .ToDictionary(
                candidate => candidate.Id,
                candidate => new ProviderHealthSnapshot(
                    candidate.Id,
                    candidate.DisplayName,
                    candidate.Readiness.ToString(),
                    candidate.Version,
                    candidate.Detail));
    }

    private ProgressionProviderCandidate QuestCandidate(
        string internalName,
        string displayName,
        ProviderId providerId,
        ProgressionProviderFlavor flavor,
        bool contractReady)
    {
        PluginPresence presence = dependencies.FindPlugin(internalName);
        return Candidate(
            providerId,
            displayName,
            ProgressionProviderRole.Questing,
            flavor,
            presence,
            contractReady,
            "IsRunning, StartSingleQuest, and Stop");
    }

    private ProgressionProviderCandidate[] DutyCandidates()
    {
        PluginPresence presence = dependencies.FindPlugin("AutoDuty");
        bool stockContractReady = autoDutyContentHasPath.HasFunction && autoDutyIsStopped.HasFunction &&
                                  autoDutyRun.HasAction && autoDutyStop.HasAction;
        bool isVieriCompatibilityProvider = vieriAutoDutyProgression.HasFunction ||
            string.Equals(presence.DisplayName, "VieriAutoDuty", StringComparison.OrdinalIgnoreCase);
        ProgressionProviderCandidate compatibility = isVieriCompatibilityProvider
            ? Candidate(
                AutoDutyCompatibilityProviderId,
                "VieriAutoDuty",
                ProgressionProviderRole.Duties,
                ProgressionProviderFlavor.VieriCompatibility,
                presence,
                stockContractReady,
                "ContentHasPath, Run, IsStopped, and Stop")
            : UnavailableDutyCandidate(
                AutoDutyCompatibilityProviderId,
                "VieriAutoDuty",
                ProgressionProviderFlavor.VieriCompatibility,
                "The VieriAutoDuty migration source is not active.");
        ProgressionProviderCandidate stock = !isVieriCompatibilityProvider
            ? Candidate(
                AutoDutyStockProviderId,
                "AutoDuty",
                ProgressionProviderRole.Duties,
                ProgressionProviderFlavor.Stock,
                presence,
                stockContractReady,
                "ContentHasPath, Run, IsStopped, and Stop")
            : UnavailableDutyCandidate(
                AutoDutyStockProviderId,
                "AutoDuty",
                ProgressionProviderFlavor.Stock,
                "Target provider after Nexus absorbs the remaining VieriAutoDuty behavior; do not enable it beside the fork.");
        return [compatibility, stock];
    }

    private static ProgressionProviderCandidate UnavailableDutyCandidate(
        ProviderId id,
        string displayName,
        ProgressionProviderFlavor flavor,
        string detail) => new(
        id,
        displayName,
        ProgressionProviderRole.Duties,
        flavor,
        ProgressionProviderReadiness.Missing,
        null,
        detail);

    private static ProgressionProviderCandidate Candidate(
        ProviderId id,
        string displayName,
        ProgressionProviderRole role,
        ProgressionProviderFlavor flavor,
        PluginPresence presence,
        bool contractReady,
        string contract)
    {
        ProgressionProviderReadiness readiness = !presence.IsInstalled
            ? ProgressionProviderReadiness.Missing
            : !presence.IsLoaded
                ? ProgressionProviderReadiness.Disabled
                : contractReady
                    ? ProgressionProviderReadiness.Ready
                    : ProgressionProviderReadiness.Incompatible;
        string detail = readiness switch
        {
            ProgressionProviderReadiness.Missing => $"{displayName} is not installed.",
            ProgressionProviderReadiness.Disabled => $"{displayName} is installed but disabled.",
            ProgressionProviderReadiness.Incompatible =>
                $"{displayName} is loaded but does not expose the required contract: {contract}.",
            _ => $"Required contract detected: {contract}.",
        };
        return new ProgressionProviderCandidate(
            id,
            displayName,
            role,
            flavor,
            readiness,
            presence.Version,
            detail);
    }
}

internal sealed record ProgressionProviderSnapshot(
    ProgressionProviderSelection Questing,
    ProgressionProviderSelection Duties);
