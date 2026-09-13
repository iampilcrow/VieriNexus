using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class AutoDutyProviderIdentityPolicyTests
{
    [Fact]
    public void SelectsStockWhenItIsTheOnlyLoadedProvider() =>
        Assert.Equal(
            AutoDutyProviderIdentity.Stock,
            AutoDutyProviderIdentityPolicy.Assess(
                [new AutoDutyProviderInstance(true, "AutoDuty", "0.0.0.335")],
                false).Identity);

    [Fact]
    public void CompatibilityMarkerIdentifiesRenamedVieriProvider() =>
        Assert.Equal(
            AutoDutyProviderIdentity.VieriCompatibility,
            AutoDutyProviderIdentityPolicy.Assess(
                [new AutoDutyProviderInstance(true, "AutoDuty", "1.0.0.443")],
                true).Identity);

    [Fact]
    public void InstalledButDisabledProvidersDoNotOwnDutyIpc()
    {
        AutoDutyProviderIdentityAssessment result = AutoDutyProviderIdentityPolicy.Assess(
            [
                new AutoDutyProviderInstance(false, "VieriAutoDuty", "1.0.0.443"),
                new AutoDutyProviderInstance(false, "AutoDuty", "0.0.0.335"),
            ],
            false);

        Assert.Equal(AutoDutyProviderIdentity.None, result.Identity);
        Assert.Equal(2, result.InstalledCount);
        Assert.Equal(0, result.LoadedCount);
    }

    [Fact]
    public void DuplicateLoadedIdentityAlwaysFailsClosedEvenIfIpcLooksReady()
    {
        AutoDutyProviderIdentityAssessment result = AutoDutyProviderIdentityPolicy.Assess(
            [
                new AutoDutyProviderInstance(true, "VieriAutoDuty", "1.0.0.443"),
                new AutoDutyProviderInstance(true, "AutoDuty", "0.0.0.335"),
            ],
            true);

        Assert.Equal(AutoDutyProviderIdentity.Conflict, result.Identity);
        Assert.Equal(2, result.LoadedCount);
        Assert.Contains("more than one", result.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
