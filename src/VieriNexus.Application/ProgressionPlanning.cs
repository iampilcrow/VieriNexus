using VieriNexus.Domain;

namespace VieriNexus.Application;

public enum ProgressionProviderRole
{
    Questing,
    Duties,
}

public enum ProgressionProviderFlavor
{
    VieriCompatibility,
    Stock,
}

public enum ProgressionProviderReadiness
{
    Missing,
    Disabled,
    Incompatible,
    Ready,
    Conflict,
}

public sealed record ProgressionProviderCandidate(
    ProviderId Id,
    string DisplayName,
    ProgressionProviderRole Role,
    ProgressionProviderFlavor Flavor,
    ProgressionProviderReadiness Readiness,
    string? Version,
    string Detail);

public sealed record ProgressionProviderSelection(
    ProgressionProviderRole Role,
    ProgressionProviderReadiness Readiness,
    ProgressionProviderCandidate? Selected,
    IReadOnlyList<ProgressionProviderCandidate> Candidates,
    string Detail)
{
    public bool IsReady => Readiness == ProgressionProviderReadiness.Ready && Selected is not null;
}

public static class ProgressionProviderPolicy
{
    public static ProgressionProviderSelection Select(
        ProgressionProviderRole role,
        IEnumerable<ProgressionProviderCandidate> candidates)
    {
        ProgressionProviderCandidate[] relevant = candidates
            .Where(candidate => candidate.Role == role)
            .ToArray();
        ProgressionProviderCandidate[] ready = relevant
            .Where(candidate => candidate.Readiness == ProgressionProviderReadiness.Ready)
            .ToArray();

        if (ready.Length > 1)
        {
            return new ProgressionProviderSelection(
                role,
                ProgressionProviderReadiness.Conflict,
                null,
                relevant,
                $"More than one {RoleName(role)} provider is active. Disable one before Nexus can delegate work.");
        }

        if (ready.Length == 1)
        {
            return new ProgressionProviderSelection(
                role,
                ProgressionProviderReadiness.Ready,
                ready[0],
                relevant,
                $"{ready[0].DisplayName} satisfies the {RoleName(role)} provider contract.");
        }

        ProgressionProviderReadiness readiness = relevant.Any(candidate =>
            candidate.Readiness == ProgressionProviderReadiness.Incompatible)
                ? ProgressionProviderReadiness.Incompatible
                : relevant.Any(candidate => candidate.Readiness == ProgressionProviderReadiness.Disabled)
                    ? ProgressionProviderReadiness.Disabled
                    : ProgressionProviderReadiness.Missing;
        string detail = readiness switch
        {
            ProgressionProviderReadiness.Incompatible =>
                $"An installed {RoleName(role)} provider does not satisfy the required IPC contract.",
            ProgressionProviderReadiness.Disabled =>
                $"A compatible {RoleName(role)} provider is installed but disabled.",
            _ => $"No {RoleName(role)} provider is installed.",
        };
        return new ProgressionProviderSelection(role, readiness, null, relevant, detail);
    }

    private static string RoleName(ProgressionProviderRole role) => role switch
    {
        ProgressionProviderRole.Questing => "questing",
        ProgressionProviderRole.Duties => "duty",
        _ => role.ToString().ToLowerInvariant(),
    };
}

public sealed record ReachJobLevelGoalDraft(
    CharacterKey Character,
    uint ClassJobId,
    int CurrentLevel,
    int TargetLevel,
    bool AllowJobQuests,
    bool AllowHuntingLog,
    bool AllowSideQuests,
    bool AllowDuties,
    int MinimumGilReserve);

public enum ProgressionPlanIssueSeverity
{
    Information,
    Warning,
    Blocker,
}

public sealed record ProgressionPlanIssue(
    ProgressionPlanIssueSeverity Severity,
    string Code,
    string Message);

public sealed record ProgressionPlanStep(
    string Code,
    string Title,
    string Reason,
    CapabilityId Capability,
    ProviderId? Provider,
    IReadOnlySet<ResourceKind> Resources);

public sealed record ReachJobLevelPlan(
    bool IsValid,
    bool IsSatisfied,
    bool HasUsableProvider,
    bool IsExecutionConnected,
    string Code,
    string Summary,
    IReadOnlyList<ProgressionPlanIssue> Issues,
    IReadOnlyList<ProgressionPlanStep> Steps);

public static class ReachJobLevelPlanner
{
    public const int MaximumSupportedLevel = 100;

    private static readonly CapabilityId GearCapability = new("vieri.capability.gear.ensure-readiness/v1");
    private static readonly CapabilityId QuestCapability = new("vieri.capability.quest.run-supported/v1");
    private static readonly CapabilityId DutyCapability = new("vieri.capability.duty.run/v1");
    private static readonly CapabilityId VerifyCapability = new("vieri.capability.progression.verify-level/v1");

    public static ReachJobLevelPlan Build(
        ReachJobLevelGoalDraft draft,
        ProgressionProviderSelection questing,
        ProgressionProviderSelection duties)
    {
        List<ProgressionPlanIssue> issues = [];
        List<ProgressionPlanStep> steps = [];

        if (!draft.Character.IsKnown)
            issues.Add(new(ProgressionPlanIssueSeverity.Blocker, "character-unknown",
                "Wait for the current character to finish loading."));
        if (draft.ClassJobId == 0)
            issues.Add(new(ProgressionPlanIssueSeverity.Blocker, "job-unknown",
                "The current combat job could not be identified."));
        if (draft.CurrentLevel is < 1 or > MaximumSupportedLevel)
            issues.Add(new(ProgressionPlanIssueSeverity.Blocker, "current-level-invalid",
                "The current job level is outside the supported range."));
        if (draft.TargetLevel is < 1 or > MaximumSupportedLevel)
            issues.Add(new(ProgressionPlanIssueSeverity.Blocker, "target-level-invalid",
                $"Choose a target level from 1 to {MaximumSupportedLevel}."));
        if (draft.MinimumGilReserve < 0)
            issues.Add(new(ProgressionPlanIssueSeverity.Blocker, "gil-reserve-invalid",
                "The gil reserve cannot be negative."));

        bool questLaneRequested = draft.AllowJobQuests || draft.AllowHuntingLog || draft.AllowSideQuests;
        bool dutyLaneRequested = draft.AllowDuties;
        if (!questLaneRequested && !dutyLaneRequested)
            issues.Add(new(ProgressionPlanIssueSeverity.Blocker, "no-leveling-method",
                "Enable at least one leveling method."));

        if (draft.TargetLevel <= draft.CurrentLevel &&
            issues.All(issue => issue.Severity != ProgressionPlanIssueSeverity.Blocker))
        {
            return new ReachJobLevelPlan(
                true,
                true,
                true,
                false,
                "already-satisfied",
                $"Level {draft.TargetLevel} is already reached on the current job.",
                issues,
                []);
        }

        bool questLaneReady = questLaneRequested && questing.IsReady;
        bool dutyLaneReady = dutyLaneRequested && duties.IsReady;
        if (questLaneRequested && !questing.IsReady)
            issues.Add(new(ProgressionPlanIssueSeverity.Warning, "quest-provider-unavailable", questing.Detail));
        if (dutyLaneRequested && !duties.IsReady)
            issues.Add(new(ProgressionPlanIssueSeverity.Warning, "duty-provider-unavailable", duties.Detail));
        if ((questLaneRequested || dutyLaneRequested) && !questLaneReady && !dutyLaneReady)
            issues.Add(new(ProgressionPlanIssueSeverity.Blocker, "no-provider-ready",
                "None of the enabled leveling methods currently has a compatible provider."));

        bool valid = issues.All(issue => issue.Severity != ProgressionPlanIssueSeverity.Blocker);
        if (!valid)
        {
            return new ReachJobLevelPlan(
                false,
                false,
                false,
                false,
                "blocked",
                "The progression draft needs attention before it can become a goal.",
                issues,
                []);
        }

        steps.Add(new ProgressionPlanStep(
            "ensure-gear-readiness",
            "Prepare equipment",
            $"Protect at least {draft.MinimumGilReserve:N0} gil while checking upgrades for the current job.",
            GearCapability,
            null,
            new HashSet<ResourceKind> { ResourceKind.InventoryMutation, ResourceKind.UiInteraction }));

        if (questLaneReady)
        {
            string[] methods =
            [
                .. (draft.AllowJobQuests ? new[] { "job quests" } : Array.Empty<string>()),
                .. (draft.AllowHuntingLog ? new[] { "Hunting Log" } : Array.Empty<string>()),
                .. (draft.AllowSideQuests ? new[] { "side quests" } : Array.Empty<string>()),
            ];
            steps.Add(new ProgressionPlanStep(
                "run-supported-quest-work",
                "Complete available quest work",
                $"Use {string.Join(", ", methods)} when Nexus policy finds eligible supported work.",
                QuestCapability,
                questing.Selected!.Id,
                new HashSet<ResourceKind>
                {
                    ResourceKind.Movement,
                    ResourceKind.Navigation,
                    ResourceKind.Targeting,
                    ResourceKind.Combat,
                    ResourceKind.Rotation,
                    ResourceKind.UiInteraction,
                }));
        }

        if (dutyLaneReady)
        {
            steps.Add(new ProgressionPlanStep(
                "run-one-supported-duty",
                "Run one eligible duty",
                "Delegate one bounded duty, verify completion, then return control to Nexus for replanning.",
                DutyCapability,
                duties.Selected!.Id,
                new HashSet<ResourceKind>
                {
                    ResourceKind.DutyQueue,
                    ResourceKind.Movement,
                    ResourceKind.Navigation,
                    ResourceKind.Targeting,
                    ResourceKind.Combat,
                    ResourceKind.Rotation,
                }));
        }

        steps.Add(new ProgressionPlanStep(
            "verify-level-and-replan",
            "Verify progress",
            "Observe the permanent job level after each bounded activity; stop at the target or build a fresh plan.",
            VerifyCapability,
            null,
            new HashSet<ResourceKind>()));

        issues.Add(new(ProgressionPlanIssueSeverity.Information, "execution-not-connected",
            "This release saves and reviews the Nexus-owned goal plan. It does not start quest or duty automation yet."));
        return new ReachJobLevelPlan(
            true,
            false,
            true,
            false,
            "draft-ready",
            $"Plan ready for the current job: level {draft.CurrentLevel} to {draft.TargetLevel}.",
            issues,
            steps);
    }
}
