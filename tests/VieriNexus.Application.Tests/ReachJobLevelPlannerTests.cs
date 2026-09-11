using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class ReachJobLevelPlannerTests
{
    [Fact]
    public void BuildsPlanWithBoundedQuestAndDutyExecutionConnected()
    {
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            Draft(),
            Ready(ProgressionProviderRole.Questing, "codex"),
            Ready(ProgressionProviderRole.Duties, "autoduty"));

        Assert.True(plan.IsValid);
        Assert.True(plan.HasUsableProvider);
        Assert.True(plan.IsExecutionConnected);
        Assert.Equal(
        [
            "ensure-gear-readiness",
            "run-supported-quest-work",
            "run-one-supported-duty",
            "verify-level-and-replan",
        ], plan.Steps.Select(step => step.Code));
        Assert.Equal(new ProviderId("codex"), plan.Steps[1].Provider);
        Assert.Equal(new ProviderId("autoduty"), plan.Steps[2].Provider);
    }

    [Fact]
    public void AlreadyReachedTargetIsSatisfiedWithoutTasks()
    {
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            Draft(currentLevel: 90, targetLevel: 90),
            Ready(ProgressionProviderRole.Questing, "codex"),
            Ready(ProgressionProviderRole.Duties, "autoduty"));

        Assert.True(plan.IsValid);
        Assert.True(plan.IsSatisfied);
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void DutyOnlyPlanDoesNotRequireQuestProvider()
    {
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            Draft(jobQuests: false, huntingLog: false, sideQuests: false, duties: true),
            Missing(ProgressionProviderRole.Questing),
            Ready(ProgressionProviderRole.Duties, "autoduty"));

        Assert.True(plan.IsValid);
        Assert.True(plan.IsExecutionConnected);
        Assert.DoesNotContain(plan.Steps, step => step.Code == "run-supported-quest-work");
        Assert.Contains(plan.Steps, step => step.Code == "run-one-supported-duty");
    }

    [Fact]
    public void ClassJobRoleQuestOnlyPlanIsExecutableThroughQuestProvider()
    {
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            Draft(jobQuests: true, huntingLog: false, sideQuests: false, duties: false),
            Ready(ProgressionProviderRole.Questing, "questionable"),
            Missing(ProgressionProviderRole.Duties));

        Assert.True(plan.IsValid);
        Assert.True(plan.IsExecutionConnected);
        Assert.Contains(plan.Steps, step => step.Code == "run-supported-quest-work");
        Assert.DoesNotContain(plan.Steps, step => step.Code == "run-one-supported-duty");
    }

    [Fact]
    public void GeneralSideQuestOnlyPlanIsExecutableThroughQuestProvider()
    {
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            Draft(jobQuests: false, huntingLog: false, sideQuests: true, duties: false),
            Ready(ProgressionProviderRole.Questing, "questionable"),
            Missing(ProgressionProviderRole.Duties));

        Assert.True(plan.IsValid);
        Assert.True(plan.IsExecutionConnected);
        ProgressionPlanStep step = Assert.Single(plan.Steps, step => step.Code == "run-supported-quest-work");
        Assert.Contains("general side quests", step.Reason);
        Assert.DoesNotContain(plan.Steps, step => step.Code == "run-one-supported-duty");
    }

    [Fact]
    public void HuntingLogAloneRemainsBlockedUntilNativeSelectionIsConnected()
    {
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            Draft(jobQuests: false, huntingLog: true, sideQuests: false, duties: false),
            Ready(ProgressionProviderRole.Questing, "questionable"),
            Missing(ProgressionProviderRole.Duties));

        Assert.False(plan.IsValid);
        Assert.False(plan.IsExecutionConnected);
        Assert.Contains(plan.Issues, issue => issue.Code == "quest-methods-not-connected");
        Assert.Contains(plan.Issues, issue => issue.Code == "no-provider-ready");
    }

    [Fact]
    public void RequestedUnavailableLaneWarnsButOtherReadyLaneCanProceed()
    {
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            Draft(),
            Missing(ProgressionProviderRole.Questing),
            Ready(ProgressionProviderRole.Duties, "autoduty"));

        Assert.True(plan.IsValid);
        Assert.True(plan.IsExecutionConnected);
        Assert.Contains(plan.Issues, issue =>
            issue.Code == "quest-provider-unavailable" &&
            issue.Severity == ProgressionPlanIssueSeverity.Warning);
    }

    [Fact]
    public void NoReadyEnabledProviderBlocksPlan()
    {
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            Draft(),
            Missing(ProgressionProviderRole.Questing),
            Missing(ProgressionProviderRole.Duties));

        Assert.False(plan.IsValid);
        Assert.False(plan.HasUsableProvider);
        Assert.Contains(plan.Issues, issue => issue.Code == "no-provider-ready");
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void NoLevelingMethodBlocksPlan()
    {
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            Draft(jobQuests: false, huntingLog: false, sideQuests: false, duties: false),
            Ready(ProgressionProviderRole.Questing, "codex"),
            Ready(ProgressionProviderRole.Duties, "autoduty"));

        Assert.False(plan.IsValid);
        Assert.Contains(plan.Issues, issue => issue.Code == "no-leveling-method");
    }

    [Fact]
    public void InvalidTargetAndGilReserveFailValidation()
    {
        ReachJobLevelPlan plan = ReachJobLevelPlanner.Build(
            Draft(targetLevel: 101, gilReserve: -1),
            Ready(ProgressionProviderRole.Questing, "codex"),
            Ready(ProgressionProviderRole.Duties, "autoduty"));

        Assert.False(plan.IsValid);
        Assert.Contains(plan.Issues, issue => issue.Code == "target-level-invalid");
        Assert.Contains(plan.Issues, issue => issue.Code == "gil-reserve-invalid");
    }

    private static ReachJobLevelGoalDraft Draft(
        int currentLevel = 90,
        int targetLevel = 92,
        bool jobQuests = true,
        bool huntingLog = true,
        bool sideQuests = true,
        bool duties = true,
        int gilReserve = 1_000_000) => new(
            new CharacterKey(123, 456),
            41,
            currentLevel,
            targetLevel,
            jobQuests,
            huntingLog,
            sideQuests,
            duties,
            gilReserve);

    private static ProgressionProviderSelection Ready(ProgressionProviderRole role, string id)
    {
        ProgressionProviderCandidate candidate = new(
            new ProviderId(id),
            id,
            role,
            ProgressionProviderFlavor.VieriCompatibility,
            ProgressionProviderReadiness.Ready,
            "1.0.0",
            "ready");
        return new ProgressionProviderSelection(
            role,
            ProgressionProviderReadiness.Ready,
            candidate,
            [candidate],
            "ready");
    }

    private static ProgressionProviderSelection Missing(ProgressionProviderRole role) => new(
        role,
        ProgressionProviderReadiness.Missing,
        null,
        [],
        "missing");
}
