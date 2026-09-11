using VieriNexus.Domain;

namespace VieriNexus.Application;

public sealed record GearCurrentSlot(
    int SlotKey,
    string Name,
    string CurrentEquipment,
    uint CurrentItemId,
    uint CurrentItemLevel,
    bool ActiveExperienceBonus,
    bool IsMainHand = false,
    bool IsOffHand = false,
    bool IsRing = false);

public sealed record GearUpgradeCandidate(
    int SlotKey,
    uint ItemId,
    string Name,
    uint ItemLevel,
    byte EquipLevel,
    uint UnitPrice,
    string Vendor,
    int Quantity,
    int OwnedCopies,
    int PrimaryStat,
    bool IsMainHand = false,
    bool IsOffHand = false,
    bool IsTwoHandedMainHand = false,
    uint VendorDataId = 0,
    uint VendorTerritoryId = 0);

public sealed record GearUpgradeSnapshot(
    int SchemaVersion,
    ulong CharacterId,
    ulong EquipmentSignature,
    string Job,
    short Level,
    byte VendorLevel,
    IReadOnlyList<GearCurrentSlot> Slots,
    IReadOnlyList<GearUpgradeCandidate> Candidates,
    bool CurrentMainHandIsTwoHanded = false,
    string? UnavailableReason = null)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record GearUpgradeReplacement(
    uint ItemId,
    string Name,
    uint ItemLevel,
    byte EquipLevel,
    uint UnitPrice,
    string Vendor,
    int Quantity,
    int OwnedCopies,
    uint VendorDataId = 0,
    uint VendorTerritoryId = 0);

public sealed record GearUpgradeSlot(
    int SlotKey,
    string Name,
    string CurrentEquipment,
    bool ActiveExperienceBonus,
    bool Recommended,
    GearUpgradeReplacement? Replacement);

public sealed record GearUpgradePreview(
    int SchemaVersion,
    ulong CharacterId,
    ulong EquipmentSignature,
    string Job,
    short Level,
    byte VendorLevel,
    IReadOnlyList<GearUpgradeSlot> Slots,
    string? UnavailableReason = null)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record GearShoppingApprovalLine(
    int SlotKey,
    uint ItemId,
    uint MaximumUnitPrice,
    int Quantity,
    uint VendorDataId = 0,
    uint VendorTerritoryId = 0);

public sealed record GearShoppingApproval(
    int SchemaVersion,
    ulong CharacterId,
    ulong EquipmentSignature,
    int MinimumGilReserve,
    IReadOnlyList<GearShoppingApprovalLine> Lines)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record GearShoppingApprovalResult(
    bool Success,
    string Message,
    GearShoppingApproval? Approval = null,
    ulong EstimatedCost = 0);

public sealed record GearReadinessDecision(
    bool Success,
    bool RequiresShopping,
    string Message,
    GearShoppingApproval? Approval = null);

/// <summary>
/// Uses the same Nexus-owned exact approval for automatic Progression and the manual Gear page.
/// A verified no-upgrade snapshot completes locally and never starts a provider operation.
/// </summary>
public static class GearReadinessDecisionPolicy
{
    public static GearReadinessDecision Build(
        GearUpgradePreview preview,
        int currentGil,
        int minimumGilReserve)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!string.IsNullOrWhiteSpace(preview.UnavailableReason))
            return new(false, false, preview.UnavailableReason);

        int[] selectedSlots = preview.Slots
            .Where(slot => slot.Recommended && !slot.ActiveExperienceBonus && slot.Replacement is not null)
            .Select(slot => slot.SlotKey)
            .ToArray();
        if (selectedSlots.Length == 0)
            return new(true, false, $"Nexus verified the current {preview.Job} gear; no vendor upgrades are needed.");

        GearShoppingApprovalResult approval = GearShoppingApprovalPolicy.Build(
            preview,
            selectedSlots,
            currentGil,
            minimumGilReserve);
        return approval.Success && approval.Approval is not null
            ? new(true, true, approval.Message, approval.Approval)
            : new(false, false, approval.Message);
    }
}

/// <summary>
/// Selects the exact live gil-vendor replacement for each equipment slot from Nexus's native
/// equipment/catalog snapshot. Nexus owns role scoring, two-handed weapon handling, EXP-item
/// protection, upgrade comparison, and the final preview presented for approval.
/// </summary>
public static class GearUpgradeCandidatePolicy
{
    public static GearUpgradePreview BuildPreview(GearUpgradeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != GearUpgradeSnapshot.CurrentSchemaVersion)
            return Unavailable(snapshot, "The gear scan uses an unsupported format. Update both VieriNexus and VieriAutoDuty.");
        if (!string.IsNullOrWhiteSpace(snapshot.UnavailableReason))
            return Unavailable(snapshot, snapshot.UnavailableReason);
        if (snapshot.CharacterId == 0 || snapshot.EquipmentSignature == 0)
            return Unavailable(snapshot, "The gear scan is not tied to a verified character and equipment snapshot.");
        if (snapshot.Slots.GroupBy(slot => slot.SlotKey).Any(group => group.Count() != 1))
            return Unavailable(snapshot, "The gear scan contains duplicate equipment slots. Refresh before shopping.");

        Dictionary<int, GearUpgradeCandidate> selected = snapshot.Candidates
            .Where(IsComplete)
            .GroupBy(candidate => candidate.SlotKey)
            .Select(group => SelectBest(group))
            .Where(candidate => candidate is not null)
            .ToDictionary(candidate => candidate!.SlotKey, candidate => candidate!);
        if (snapshot.Slots.Count(slot => slot.IsMainHand) > 1 ||
            selected.Values.Count(candidate => candidate.IsMainHand) > 1)
            return Unavailable(snapshot, "The gear scan contains conflicting main-hand data. Refresh before shopping.");

        GearCurrentSlot? currentMainHand = snapshot.Slots.FirstOrDefault(slot => slot.IsMainHand);
        GearUpgradeCandidate? plannedMainHand = selected.Values.FirstOrDefault(candidate => candidate.IsMainHand);
        bool plannedMainHandIsTwoHanded = plannedMainHand?.IsTwoHandedMainHand ??
                                          (currentMainHand is not null && snapshot.CurrentMainHandIsTwoHanded);

        List<GearUpgradeSlot> slots = [];
        foreach (GearCurrentSlot slot in snapshot.Slots)
        {
            selected.TryGetValue(slot.SlotKey, out GearUpgradeCandidate? candidate);
            if (slot.IsOffHand && plannedMainHandIsTwoHanded)
                candidate = null;

            bool recommended = candidate is not null &&
                !slot.ActiveExperienceBonus &&
                candidate.ItemLevel > slot.CurrentItemLevel &&
                (candidate.Quantity > 0 || candidate.OwnedCopies > 0);
            GearUpgradeReplacement? replacement = recommended
                ? new(candidate!.ItemId, candidate.Name, candidate.ItemLevel, candidate.EquipLevel,
                    candidate.UnitPrice, candidate.Vendor, candidate.Quantity, candidate.OwnedCopies,
                    candidate.VendorDataId, candidate.VendorTerritoryId)
                : null;
            slots.Add(new GearUpgradeSlot(slot.SlotKey, slot.Name, slot.CurrentEquipment,
                slot.ActiveExperienceBonus, recommended, replacement));
        }

        return new GearUpgradePreview(
            GearUpgradePreview.CurrentSchemaVersion,
            snapshot.CharacterId,
            snapshot.EquipmentSignature,
            snapshot.Job,
            snapshot.Level,
            snapshot.VendorLevel,
            slots);
    }

    private static GearUpgradeCandidate? SelectBest(IEnumerable<GearUpgradeCandidate> candidates)
    {
        GearUpgradeCandidate[] available = candidates.ToArray();
        if (available.Length == 0)
            return null;

        IEnumerable<GearUpgradeCandidate> suitable = available.Any(candidate => candidate.PrimaryStat > 0)
            ? available.Where(candidate => candidate.PrimaryStat > 0)
            : available;
        return suitable.OrderByDescending(candidate => candidate.ItemLevel)
            .ThenByDescending(candidate => candidate.PrimaryStat)
            .ThenBy(candidate => candidate.UnitPrice)
            .First();
    }

    private static bool IsComplete(GearUpgradeCandidate candidate) =>
        candidate.ItemId != 0 && candidate.UnitPrice != 0 && candidate.Quantity >= 0 &&
        candidate.OwnedCopies >= 0 && !string.IsNullOrWhiteSpace(candidate.Name) &&
        !string.IsNullOrWhiteSpace(candidate.Vendor);

    private static GearUpgradePreview Unavailable(GearUpgradeSnapshot snapshot, string? reason) => new(
        GearUpgradePreview.CurrentSchemaVersion,
        snapshot.CharacterId,
        snapshot.EquipmentSignature,
        snapshot.Job,
        snapshot.Level,
        snapshot.VendorLevel,
        [],
        reason ?? "The live gear scan is unavailable.");
}

/// <summary>
/// Nexus-owned approval boundary for manual gear shopping. A provider may describe live candidates,
/// but only Nexus chooses the exact slots, item ids, quantities, maximum prices, and spending floor.
/// </summary>
public static class GearShoppingApprovalPolicy
{
    public static GearShoppingApprovalResult Build(
        GearUpgradePreview preview,
        IEnumerable<int> selectedSlotKeys,
        int currentGil,
        int minimumGilReserve)
    {
        if (preview.SchemaVersion != GearUpgradePreview.CurrentSchemaVersion)
            return Failed("The gear preview uses an unsupported format. Refresh after updating the provider.");
        if (preview.CharacterId == 0 || preview.EquipmentSignature == 0)
            return Failed("The gear preview is not tied to a verified character and equipment snapshot.");
        if (!string.IsNullOrWhiteSpace(preview.UnavailableReason))
            return Failed(preview.UnavailableReason);

        int reserve = Math.Clamp(minimumGilReserve, 0, 999_999_999);
        int[] selected = selectedSlotKeys.Distinct().ToArray();
        if (selected.Length == 0)
            return Failed("Select at least one displayed upgrade.");

        List<GearShoppingApprovalLine> lines = [];
        ulong estimatedCost = 0;
        foreach (int slotKey in selected)
        {
            GearUpgradeSlot[] matchingSlots = preview.Slots.Where(candidate => candidate.SlotKey == slotKey)
                .Take(2)
                .ToArray();
            if (matchingSlots.Length != 1)
                return Failed("A selected equipment slot is not present in this preview. Refresh before shopping.");
            GearUpgradeSlot slot = matchingSlots[0];
            if (slot.ActiveExperienceBonus)
                return Failed($"{slot.Name} contains a protected EXP item and cannot be approved.");
            if (slot.Replacement is not { } replacement)
                return Failed($"{slot.Name} has no verified gil-vendor upgrade to approve.");
            if (replacement.ItemId == 0 || replacement.Quantity < 0 || replacement.UnitPrice == 0)
                return Failed($"{slot.Name} has incomplete purchase data. Refresh before shopping.");

            checked
            {
                estimatedCost += (ulong)replacement.UnitPrice * (ulong)replacement.Quantity;
            }
            lines.Add(new GearShoppingApprovalLine(
                slot.SlotKey,
                replacement.ItemId,
                replacement.UnitPrice,
                replacement.Quantity,
                replacement.VendorDataId,
                replacement.VendorTerritoryId));
        }

        ulong spendable = (ulong)Math.Max(0, currentGil - reserve);
        if (estimatedCost > spendable)
            return Failed($"The selected upgrades cost about {estimatedCost:N0} gil, but only {spendable:N0} gil is available above the protected reserve.");

        GearShoppingApproval approval = new(
            GearShoppingApproval.CurrentSchemaVersion,
            preview.CharacterId,
            preview.EquipmentSignature,
            reserve,
            lines);
        return new(true,
            $"Approved {lines.Count} equipment slot{(lines.Count == 1 ? string.Empty : "s")} with an estimated cost of {estimatedCost:N0} gil.",
            approval,
            estimatedCost);
    }

    private static GearShoppingApprovalResult Failed(string message) => new(false, message);
}

public interface IManualGearShoppingProvider
{
    ProviderId Id { get; }

    ProgressionGearProviderObservation Observe();

    bool TryStart(GearShoppingApproval approval, out string message);

    bool TryStop(out string message);
}

public enum ManualGearShoppingState
{
    Idle,
    Starting,
    Running,
    Stopping,
    Completed,
    Failed,
}

public sealed record ManualGearShoppingStatus(
    ManualGearShoppingState State,
    string Message,
    DateTimeOffset UpdatedAtUtc)
{
    public bool IsActive => State is ManualGearShoppingState.Starting or
        ManualGearShoppingState.Running or ManualGearShoppingState.Stopping;
}

/// <summary>
/// Owns the complete lifetime of an explicitly approved shopping run. It prevents a manual gear
/// action from racing Progression or another movement/UI/inventory owner and retains the lease
/// until the mechanics provider confirms inactivity after completion or Stop.
/// </summary>
public sealed class ManualGearShoppingCoordinator
{
    private static readonly ResourceKind[] Resources =
    [
        ResourceKind.Teleport,
        ResourceKind.Navigation,
        ResourceKind.Movement,
        ResourceKind.UiInteraction,
        ResourceKind.InventoryMutation,
    ];

    private static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(15);
    private readonly ResourceLeaseManager leases;
    private readonly IManualGearShoppingProvider provider;
    private ResourceLeaseHandle? activeLease;
    private long baselineCompletedSequence;

    public ManualGearShoppingCoordinator(ResourceLeaseManager leases, IManualGearShoppingProvider provider)
    {
        this.leases = leases;
        this.provider = provider;
        Status = new(ManualGearShoppingState.Idle, "No manual gear shopping is active.", DateTimeOffset.UtcNow);
    }

    public ManualGearShoppingStatus Status { get; private set; }

    public ProgressionActionResult Start(GearShoppingApproval approval, DateTimeOffset now)
    {
        if (Status.IsActive)
            return new(false, "Manual gear shopping is already active.");

        ProgressionGearProviderObservation observation = provider.Observe();
        if (!observation.IsAvailable)
            return new(false, observation.Detail);
        if (observation.IsBusy != false)
            return new(false, "The gear provider is already doing work that Nexus does not own.");
        baselineCompletedSequence = observation.CompletedSequence;

        LeaseOwner owner = new(GoalId.New(), TaskId.New(), AttemptId.New(), 50,
            "Gear & Inventory: approved manual shopping");
        if (!leases.TryAcquire(owner, Resources, LeaseLifetime, out activeLease,
                out ResourceLeaseSnapshot? blocker))
            return new(false, $"Shopping cannot start while {blocker?.Owner.Reason ?? "another Nexus task"} owns a required resource.");

        Status = new(ManualGearShoppingState.Starting,
            "Resources acquired; validating the single-use approval against live equipment and prices.", now);
        if (!provider.TryStart(approval, out string message))
        {
            ReleaseLease();
            Status = new(ManualGearShoppingState.Failed, message, now);
            return new(false, message);
        }

        Status = new(ManualGearShoppingState.Starting, message, now);
        return new(true, message);
    }

    public void Update(DateTimeOffset now)
    {
        if (!Status.IsActive)
            return;

        if (activeLease is null || !activeLease.Heartbeat(LeaseLifetime))
        {
            provider.TryStop(out string stopMessage);
            ReleaseLease();
            Status = new(ManualGearShoppingState.Failed,
                $"Nexus lost the shopping resource lease and stopped the provider. {stopMessage}", now);
            return;
        }

        ProgressionGearProviderObservation observation = provider.Observe();
        if (!observation.IsAvailable || observation.IsBusy is null)
        {
            provider.TryStop(out string stopMessage);
            Status = new(ManualGearShoppingState.Stopping,
                $"The gear provider became unavailable; Nexus requested Stop and is retaining ownership until inactivity is confirmed. {stopMessage}",
                now);
            return;
        }

        if (observation.IsBusy == true)
        {
            Status = Status with
            {
                State = Status.State == ManualGearShoppingState.Stopping
                    ? ManualGearShoppingState.Stopping
                    : ManualGearShoppingState.Running,
                Message = Status.State == ManualGearShoppingState.Stopping
                    ? "Waiting for the gear provider to confirm Stop."
                    : observation.Detail,
                UpdatedAtUtc = now,
            };
            return;
        }

        bool stopped = Status.State == ManualGearShoppingState.Stopping;
        bool completed = observation.CompletedSequence > baselineCompletedSequence;
        ReleaseLease();
        Status = new(
            stopped ? ManualGearShoppingState.Idle : completed
                ? ManualGearShoppingState.Completed
                : ManualGearShoppingState.Failed,
            stopped ? "Manual gear shopping stopped." : completed
                ? "Approved gear shopping and equipment verification completed."
                : observation.Detail,
            now);
    }

    public ProgressionActionResult Stop(DateTimeOffset now)
    {
        if (!Status.IsActive)
            return new(false, "No manual gear shopping is active.");
        if (!provider.TryStop(out string message))
            return new(false, message);

        Status = new(ManualGearShoppingState.Stopping, message, now);
        Update(now);
        return new(true, Status.Message);
    }

    public void Shutdown(DateTimeOffset now)
    {
        if (Status.IsActive)
            provider.TryStop(out _);
        ReleaseLease();
        Status = new(ManualGearShoppingState.Idle, "Manual gear shopping is inactive.", now);
    }

    private void ReleaseLease()
    {
        if (activeLease is not null)
            activeLease.Dispose();
        activeLease = null;
    }
}
