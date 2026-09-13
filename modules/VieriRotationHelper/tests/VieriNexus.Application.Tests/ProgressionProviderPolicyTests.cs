using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Application.Tests;

public sealed class ProgressionProviderPolicyTests
{
    [Fact]
    public void SelectsOnlyReadyProviderForRole()
    {
        ProgressionProviderSelection result = ProgressionProviderPolicy.Select(
            ProgressionProviderRole.Questing,
            [
                Candidate("codex", ProgressionProviderFlavor.VieriCompatibility, ProgressionProviderReadiness.Ready),
                Candidate("questionable", ProgressionProviderFlavor.Stock, ProgressionProviderReadiness.Missing),
            ]);

        Assert.True(result.IsReady);
        Assert.Equal("codex", result.Selected!.Id.Value);
    }

    [Fact]
    public void MultipleReadyImplementationsFailClosed()
    {
        ProgressionProviderSelection result = ProgressionProviderPolicy.Select(
            ProgressionProviderRole.Questing,
            [
                Candidate("codex", ProgressionProviderFlavor.VieriCompatibility, ProgressionProviderReadiness.Ready),
                Candidate("questionable", ProgressionProviderFlavor.Stock, ProgressionProviderReadiness.Ready),
            ]);

        Assert.False(result.IsReady);
        Assert.Equal(ProgressionProviderReadiness.Conflict, result.Readiness);
        Assert.Null(result.Selected);
    }

    [Fact]
    public void IncompatibleProviderTakesPriorityOverDisabledDiagnostic()
    {
        ProgressionProviderSelection result = ProgressionProviderPolicy.Select(
            ProgressionProviderRole.Questing,
            [
                Candidate("codex", ProgressionProviderFlavor.VieriCompatibility, ProgressionProviderReadiness.Disabled),
                Candidate("questionable", ProgressionProviderFlavor.Stock, ProgressionProviderReadiness.Incompatible),
            ]);

        Assert.Equal(ProgressionProviderReadiness.Incompatible, result.Readiness);
        Assert.Contains("does not satisfy", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplicitDuplicateIdentityConflictWinsEvenWhenIpcRegistrationIsIncomplete()
    {
        ProgressionProviderCandidate conflict = Candidate(
            "autoduty-duplicate",
            ProgressionProviderFlavor.Stock,
            ProgressionProviderReadiness.Conflict) with
        {
            Role = ProgressionProviderRole.Duties,
            Detail = "More than one loaded plugin claims the AutoDuty identity.",
        };
        ProgressionProviderCandidate incompatible = Candidate(
            "autoduty",
            ProgressionProviderFlavor.VieriCompatibility,
            ProgressionProviderReadiness.Incompatible) with
        {
            Role = ProgressionProviderRole.Duties,
        };

        ProgressionProviderSelection result = ProgressionProviderPolicy.Select(
            ProgressionProviderRole.Duties,
            [conflict, incompatible]);

        Assert.Equal(ProgressionProviderReadiness.Conflict, result.Readiness);
        Assert.Null(result.Selected);
        Assert.Contains("more than one", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IgnoresCandidatesForOtherRole()
    {
        ProgressionProviderCandidate duty = Candidate(
            "autoduty",
            ProgressionProviderFlavor.Stock,
            ProgressionProviderReadiness.Ready) with
        {
            Role = ProgressionProviderRole.Duties,
        };

        ProgressionProviderSelection result = ProgressionProviderPolicy.Select(
            ProgressionProviderRole.Questing,
            [duty]);

        Assert.Equal(ProgressionProviderReadiness.Missing, result.Readiness);
        Assert.Empty(result.Candidates);
    }

    private static ProgressionProviderCandidate Candidate(
        string id,
        ProgressionProviderFlavor flavor,
        ProgressionProviderReadiness readiness) => new(
            new ProviderId(id),
            id,
            ProgressionProviderRole.Questing,
            flavor,
            readiness,
            "1.0.0",
            "test");
}
