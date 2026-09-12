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

public sealed record StagedNavigationReadResult(
    bool Success,
    string Message,
    MigrationReceipt? Receipt = null,
    NavigationLibrarySnapshot? Snapshot = null);

public sealed record AutoDutyOverlayPreferences(
    bool ShowOverlay,
    bool HideWhenStopped,
    bool LockPosition,
    bool TransparentBackground,
    bool AnchorBottom,
    bool ShowDutyStatus,
    bool ShowActionStatus,
    bool ShowGoto,
    bool ShowGear,
    bool ShowRepair,
    bool ShowExtract,
    bool ShowDesynth,
    bool ShowSell,
    bool ShowTurnIn,
    bool ShowCoffers,
    bool ShowTripleTriad);

public sealed record AutoDutyMaintenancePolicy(
    bool AutoBuyVendorGear,
    uint MinimumGilReserve,
    bool AutoEquipRecommendedGear,
    bool AutoRepair,
    uint RepairBelowPercent,
    bool RepairWithCrafter,
    string? PreferredRepairVendorJson,
    bool AutoExtract,
    bool ExtractAllCategories,
    bool AutoOpenCoffers,
    byte? CofferGearset,
    bool UseCofferBlacklist,
    IReadOnlyDictionary<uint, string> CofferBlacklist,
    bool AutoDesynth,
    bool DesynthForSkill,
    int DesynthSkillGapLimit,
    bool DesynthNormalQualityOnly,
    bool ProtectGearsetsFromDesynth,
    ulong DesynthCategories,
    bool AutoGrandCompanyTurnIn,
    bool TurnInAtFreeSlotThreshold,
    int TurnInFreeSlotThreshold,
    bool UseGrandCompanyAetheryteTickets,
    bool EntrustArmoire,
    bool EntrustGlamourChest,
    bool RegisterTripleTriadCards,
    bool RegisterMinions,
    bool RegisterOrchestrionRolls,
    bool SellTripleTriadCards,
    int TripleTriadMinimumItemCount,
    int TripleTriadMinimumFreeSlots,
    bool AutoSell,
    string AutoSellMode,
    bool SellAtOccupiedSlotThreshold,
    int SellOccupiedSlotThreshold,
    bool SellAtBagPercentThreshold,
    int SellBagPercentThreshold,
    bool ProtectGearsetsFromSelling,
    string? PreferredSellVendorJson,
    bool InDutyMaintenance,
    bool WithdrawForDurability,
    int InDutyDurabilityPercent,
    bool WithdrawForInventory,
    bool ExtractBeforeSelling,
    bool DesynthBeforeSelling,
    bool ReturnToInnAfterMaintenance);

public sealed record AutoDutyProfileSnapshot(
    string Name,
    IReadOnlyList<ulong> CharacterIds,
    AutoDutyOverlayPreferences Overlay,
    AutoDutyMaintenancePolicy Maintenance);

public sealed record AutoDutyMigrationSnapshot(
    int SchemaVersion,
    string DefaultProfileName,
    IReadOnlyList<AutoDutyProfileSnapshot> Profiles,
    IReadOnlyList<string> RetiredEquipmentTransfersJson);

public sealed record AutoDutyMigrationPreview(
    AutoDutyMigrationSnapshot? Snapshot,
    IReadOnlyList<MigrationIssue> Issues)
{
    public bool CanImport => Snapshot is not null && Issues.All(issue => issue.Severity != MigrationIssueSeverity.Error);
}

public sealed record StagedAutoDutyReadResult(
    bool Success,
    string Message,
    MigrationReceipt? Receipt = null,
    AutoDutyMigrationSnapshot? Snapshot = null);

public sealed record CommandCenterCustomCommand(string Command, string Description);

public sealed record CommandCenterSnapshot(
    int SchemaVersion,
    int SourceConfigurationVersion,
    bool ShowUnloadedPlugins,
    bool HidePluginsWithoutActions,
    bool CloseAfterOpeningPlugin,
    bool CommandPanelOpen,
    bool OnlyShowFavorites,
    string SelectedPluginId,
    float SourceListWidth,
    float SourceCommandWidth,
    float SourceWindowHeight,
    float SourceUiScale,
    bool HasSourceWindowPosition,
    float SourceWindowPositionX,
    float SourceWindowPositionY,
    bool HotkeyEnabled,
    int Hotkey,
    bool HotkeyControl,
    bool HotkeyShift,
    bool HotkeyAlt,
    bool ExactModifiers,
    IReadOnlyList<string> Favorites,
    IReadOnlyList<string> HiddenPlugins,
    IReadOnlyDictionary<string, string> PreferredCommands,
    IReadOnlyDictionary<string, IReadOnlyList<CommandCenterCustomCommand>> CustomCommands);

public sealed record CommandCenterMigrationPreview(
    CommandCenterSnapshot? Snapshot,
    IReadOnlyList<MigrationIssue> Issues)
{
    public bool CanImport => Snapshot is not null && Issues.All(issue => issue.Severity != MigrationIssueSeverity.Error);
}

public sealed record StagedCommandCenterReadResult(
    bool Success,
    string Message,
    MigrationReceipt? Receipt = null,
    CommandCenterSnapshot? Snapshot = null);
