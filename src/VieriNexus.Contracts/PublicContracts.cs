namespace VieriNexus.Contracts;

public static class NexusIpc
{
    public const int CurrentVersion = 1;
    public const string GetStatus = "VieriNexus.Status.V1.Get";
    public const string ExecuteCommand = "VieriNexus.Commands.V1.Execute";
    public const string GetOperationsStatus = "VieriNexus.Operations.V1.GetStatus";
    public const string GetDependencies = "VieriNexus.Dependencies.V1.Get";
    public const string GetNavigationApiVersion = "VieriNexus.Navigation.V1.GetApiVersion";
    public const string GetNavigationStatus = "VieriNexus.Navigation.V1.GetStatus";
    public const string ListNavigationRoutes = "VieriNexus.Navigation.V1.ListRoutes";
    public const string GetNavigationRoute = "VieriNexus.Navigation.V1.GetRoute";
    public const string ResolveGearVendorOverride = "VieriNexus.Navigation.V1.ResolveGearVendorOverride";
    public const string GetNavigationActivationStatus = "VieriNexus.Navigation.V1.GetActivationStatus";
}

public sealed record NexusStatusDto(
    int ContractVersion,
    bool IsReady,
    bool IsPaused,
    string State,
    string? Goal,
    string? CurrentTask,
    string? Reason,
    string? Provider,
    string? NextTask,
    string? BlockedReason,
    long SnapshotRevision);

public sealed record NexusCommandDto(
    int ContractVersion,
    Guid RequestId,
    string Command,
    string PayloadJson,
    ulong? CharacterContentId);

public sealed record NexusCommandResultDto(
    Guid RequestId,
    bool Accepted,
    string Code,
    string Message);

public sealed record NexusOperationsStatusDto(
    int ContractVersion,
    bool IsAvailable,
    string Character,
    ulong ContentId,
    string Job,
    int Level,
    int ItemLevel,
    int Gil,
    uint TerritoryId,
    string Location,
    bool IsInCombat,
    bool IsInDuty,
    bool IsInDutyQueue,
    int InventoryUsed,
    int InventoryTotal,
    float DurabilityPercent,
    string State,
    string? ActiveModule,
    string? Activity,
    string? Detail,
    string? Provider,
    bool StopAfterCurrentActivity,
    int CompletedQuests,
    int CompletedHuntingTargets,
    int CompletedDuties,
    string? LastCompletedActivity,
    DateTimeOffset UpdatedAtUtc);

public sealed record DependencyDto(
    string Id,
    string Name,
    string State,
    string? Version,
    bool Required,
    string Capability,
    string Detail);

public sealed record NavigationLibraryStatusDto(
    int ContractVersion,
    bool HasVerifiedStagedLibrary,
    bool IsExecutionEnabled,
    bool IsSourcePluginAuthoritative,
    int PersonalRouteCount,
    int EnabledOverrideCount,
    string State,
    string Message);

public sealed record NavigationRouteListEntryDto(
    Guid Id,
    string Name,
    uint TerritoryId,
    int PointCount,
    string Notes,
    string Tags);

public sealed record NavigationRoutePointDto(float X, float Y, float Z);

public sealed record NavigationRouteDto(
    Guid Id,
    string Name,
    uint TerritoryId,
    IReadOnlyList<NavigationRoutePointDto> Points,
    string Notes,
    string Tags,
    bool UseMesh,
    bool UseFlight,
    float Tolerance,
    float LastPointTolerance,
    int BindingKind,
    uint TargetDataId,
    string TargetLabel,
    bool OverrideEnabled,
    DateTime UpdatedAtUtc);

public sealed record NavigationRouteResolutionDto(
    bool Success,
    string Code,
    string Message,
    NavigationRouteDto? Route);

public sealed record NavigationActivationBlockerDto(string Code, string Message);

public sealed record NavigationActivationStatusDto(
    int ContractVersion,
    string State,
    bool CanActivate,
    bool IsExecutionEnabled,
    bool IsSourcePluginInstalled,
    bool IsSourcePluginLoaded,
    bool IsSourcePluginAuthoritative,
    IReadOnlyList<NavigationActivationBlockerDto> Blockers);

public static class NavigationContractJson
{
    public static string SerializeRouteList(IReadOnlyList<NavigationRouteListEntryDto> routes) =>
        System.Text.Json.JsonSerializer.Serialize(routes);

    public static string SerializeRoute(NavigationRouteDto route) =>
        System.Text.Json.JsonSerializer.Serialize(route);

    public static string SerializeRouteResolution(NavigationRouteResolutionDto resolution) =>
        System.Text.Json.JsonSerializer.Serialize(resolution);
}
