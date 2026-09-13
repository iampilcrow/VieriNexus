using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class QuestionableProviderIdentityPolicyTests
{
    [Fact]
    public void SelectsVieriCompatibilityWhenItIsTheOnlyLoadedProvider()
    {
        QuestionableProviderIdentityAssessment result = QuestionableProviderIdentityPolicy.Assess(
            Provider(true, true, "VieriCodex"),
            Provider(true, false, "Questionable"));

        Assert.Equal(QuestionableProviderIdentity.VieriCompatibility, result.Identity);
        Assert.Equal(1, result.LoadedCount);
    }

    [Fact]
    public void SelectsStockWhenItIsTheOnlyLoadedProvider()
    {
        QuestionableProviderIdentityAssessment result = QuestionableProviderIdentityPolicy.Assess(
            Provider(true, false, "VieriCodex"),
            Provider(true, true, "Questionable"));

        Assert.Equal(QuestionableProviderIdentity.Stock, result.Identity);
        Assert.Equal(1, result.LoadedCount);
    }

    [Fact]
    public void BothLoadedProvidersFailClosedBeforeContractSelection()
    {
        QuestionableProviderIdentityAssessment result = QuestionableProviderIdentityPolicy.Assess(
            Provider(true, true, "VieriCodex"),
            Provider(true, true, "Questionable"));

        Assert.Equal(QuestionableProviderIdentity.Conflict, result.Identity);
        Assert.Equal(2, result.LoadedCount);
        Assert.Contains("both loaded", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstalledButDisabledProvidersReportNoActiveIdentity()
    {
        QuestionableProviderIdentityAssessment result = QuestionableProviderIdentityPolicy.Assess(
            Provider(true, false, "VieriCodex"),
            Provider(true, false, "Questionable"));

        Assert.Equal(QuestionableProviderIdentity.None, result.Identity);
        Assert.Equal(0, result.LoadedCount);
        Assert.Contains("disabled", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static QuestionableProviderInstance Provider(bool installed, bool loaded, string name) =>
        new(installed, loaded, name, "1.0.0");
}
