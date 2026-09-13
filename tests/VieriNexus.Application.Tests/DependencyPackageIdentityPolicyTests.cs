using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class DependencyPackageIdentityPolicyTests
{
    private static readonly DependencyDescriptor AutoDuty = NexusDependencyCatalog.All.Single(
        dependency => dependency.Id == "autoduty");

    [Fact]
    public void StockAutoDutyMatchesItsPackageName() =>
        Assert.True(DependencyPackageIdentityPolicy.MatchesInstalled(AutoDuty, "AutoDuty", "AutoDuty"));

    [Fact]
    public void VieriAutoDutyCannotSatisfyOrInstallTheStockDependency()
    {
        Assert.False(DependencyPackageIdentityPolicy.MatchesInstalled(
            AutoDuty,
            "AutoDuty",
            "VieriAutoDuty"));
        Assert.False(DependencyPackageIdentityPolicy.MatchesAvailable(
            AutoDuty,
            "AutoDuty",
            "VieriAutoDuty"));
        Assert.True(DependencyPackageIdentityPolicy.IsBlockingLegacyCollision(
            AutoDuty,
            "VieriAutoDuty"));
    }

    [Fact]
    public void OrdinaryDependenciesContinueToMatchTheirInternalName()
    {
        DependencyDescriptor lifestream = NexusDependencyCatalog.All.Single(
            dependency => dependency.Id == "lifestream");

        Assert.True(DependencyPackageIdentityPolicy.MatchesInstalled(
            lifestream,
            "Lifestream",
            "Any display name"));
    }
}
