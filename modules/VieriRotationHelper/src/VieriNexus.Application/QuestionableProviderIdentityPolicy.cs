namespace VieriNexus.Application;

public enum QuestionableProviderIdentity
{
    None,
    VieriCompatibility,
    Stock,
    Conflict,
}

public sealed record QuestionableProviderInstance(
    bool IsInstalled,
    bool IsLoaded,
    string DisplayName,
    string? Version);

public sealed record QuestionableProviderIdentityAssessment(
    QuestionableProviderIdentity Identity,
    int LoadedCount,
    string Detail);

/// <summary>
/// Keeps the temporary VieriCodex provider and stock Questionable from running beside one another.
/// Their IPC names do not collide, so the handoff must fail closed before either contract is called.
/// </summary>
public static class QuestionableProviderIdentityPolicy
{
    public static QuestionableProviderIdentityAssessment Assess(
        QuestionableProviderInstance vieri,
        QuestionableProviderInstance stock)
    {
        int loadedCount = (vieri.IsLoaded ? 1 : 0) + (stock.IsLoaded ? 1 : 0);
        if (loadedCount > 1)
        {
            return new(
                QuestionableProviderIdentity.Conflict,
                loadedCount,
                "VieriCodex and stock Questionable are both loaded. Disable one and reload before Nexus can delegate quest work.");
        }

        if (vieri.IsLoaded)
            return new(QuestionableProviderIdentity.VieriCompatibility, 1, "The loaded quest provider is the VieriCodex migration source.");
        if (stock.IsLoaded)
            return new(QuestionableProviderIdentity.Stock, 1, "The loaded quest provider is stock Questionable.");

        return new(
            QuestionableProviderIdentity.None,
            0,
            vieri.IsInstalled || stock.IsInstalled
                ? "A Questionable-compatible quest provider is installed but disabled."
                : "No Questionable-compatible quest provider is installed.");
    }
}
