namespace VieriNexus.Application;

public enum MigrationIssueSeverity
{
    Information,
    Warning,
    Error,
}

public sealed record MigrationIssue(MigrationIssueSeverity Severity, string Message);

public sealed record NavigationRoutePoint(float X, float Y, float Z);

public sealed record NavigationRouteSnapshot(
    Guid Id,
    string Name,
    uint TerritoryId,
    IReadOnlyList<NavigationRoutePoint> Points,
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

public sealed record NavigationLibrarySnapshot(
    int SchemaVersion,
    int SourceConfigurationVersion,
    float RecordingIntervalSeconds,
    float MinimumPointDistance,
    bool ShowWorldPreview,
    bool ShowPointNumbers,
    bool ShowLiveNavigationPath,
    float LibraryPaneWidth,
    Guid? SelectedRouteId,
    IReadOnlyList<NavigationRouteSnapshot> Routes);

public sealed record NavigationMigrationPreview(
    NavigationLibrarySnapshot? Snapshot,
    IReadOnlyList<MigrationIssue> Issues)
{
    public bool CanImport => Snapshot is not null && Issues.All(issue => issue.Severity != MigrationIssueSeverity.Error);
}

public sealed record MigrationReceipt(
    int SchemaVersion,
    Guid Id,
    string SourceId,
    DateTimeOffset CreatedAtUtc,
    string SourcePath,
    string SourceBackupPath,
    string NexusTargetPath,
    bool PreviousNexusTargetExisted,
    string? PreviousNexusTargetBackupPath,
    string SourceSha256,
    string NexusTargetSha256);

public sealed record MigrationWriteResult(bool Success, string Message, MigrationReceipt? Receipt = null);
