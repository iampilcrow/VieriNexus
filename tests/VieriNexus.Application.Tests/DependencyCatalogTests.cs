using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class DependencyCatalogTests
{
    [Fact]
    public void RequiredCatalogMatchesCoreExternalProviders()
    {
        string[] required = NexusDependencyCatalog.All
            .Where(x => x.Required)
            .Select(x => x.Id)
            .Order()
            .ToArray();

        Assert.Equal(
        [
            "allagan-market",
            "autoduty",
            "bossmod",
            "fast-job-switcher",
            "lifestream",
            "marketbuddy",
            "questionable",
            "textadvance",
            "vnavmesh",
            "wrath",
        ], required);
    }

    [Fact]
    public void RecommendedCatalogCoversCurrentVieriIntegrations()
    {
        HashSet<string> recommended = NexusDependencyCatalog.All
            .Where(x => !x.Required)
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);

        string[] expected =
        [
            "anti-afk", "artisan", "auto-retainer", "autohook", "cbt",
            "gearsetter", "glamour-log", "mogmail", "notification-master", "pandora", "quest-map",
            "select-string", "skippy", "stylist", "yes-already",
        ];
        Assert.All(expected, id => Assert.Contains(id, recommended));
    }

    [Fact]
    public void StockRuntimeProvidersAreDependenciesButVieriMigrationSourcesAreNot()
    {
        string[] internalNames = NexusDependencyCatalog.All.SelectMany(x => x.InternalNames).ToArray();

        Assert.Contains("Questionable", internalNames);
        Assert.Contains("AutoDuty", internalNames);
        Assert.Contains("WrathCombo", internalNames);
        Assert.DoesNotContain(internalNames, x => x.StartsWith("Vieri", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryCatalogEntryIsCompleteAndUnique()
    {
        Assert.Equal(NexusDependencyCatalog.All.Count,
            NexusDependencyCatalog.All.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(NexusDependencyCatalog.All, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(entry.Capability));
            Assert.False(string.IsNullOrWhiteSpace(entry.Description));
            Assert.NotEmpty(entry.InternalNames);
            Assert.All(entry.InternalNames, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        });
    }
}
