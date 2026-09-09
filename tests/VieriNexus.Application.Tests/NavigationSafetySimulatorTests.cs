using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationSafetySimulatorTests
{
    [Fact]
    public void IsolatedSimulationPassesEveryNonMovingSafetyScenario()
    {
        NavigationSimulationReport report = new NavigationSafetySimulator().Run(DateTimeOffset.UtcNow);

        Assert.True(report.Passed);
        Assert.Equal(5, report.PassedCount);
        Assert.Equal(5, report.Scenarios.Count);
        Assert.All(report.Scenarios, scenario => Assert.True(scenario.Passed, scenario.Detail));
        Assert.Contains(report.Scenarios, scenario => scenario.Name.Contains("Manual movement"));
        Assert.Contains(report.Scenarios, scenario => scenario.Name.Contains("reload"));
        Assert.Contains(report.Scenarios, scenario => scenario.Name.Contains("Source-owner"));
        Assert.Contains(report.Scenarios, scenario => scenario.Name.Contains("Lease-expiry"));
    }
}
