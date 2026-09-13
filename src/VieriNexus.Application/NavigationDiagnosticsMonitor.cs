namespace VieriNexus.Application;

public enum NavigationDiagnosticState
{
    Healthy,
    Attention,
    Blocked,
}

public sealed record NavigationProviderDiagnostic(
    string Id,
    string DisplayName,
    NavigationDiagnosticState State,
    string? Version,
    string Code,
    string Detail);

public sealed record NavigationAuditEntry(
    DateTimeOffset ObservedAtUtc,
    string ProviderId,
    string DisplayName,
    NavigationDiagnosticState State,
    string Code,
    string Detail);

public sealed record NavigationDiagnosticsSnapshot(
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<NavigationProviderDiagnostic> Providers,
    IReadOnlyList<NavigationAuditEntry> RecentTransitions);

/// <summary>
/// Retains a bounded, session-only transition history. It records state/code changes,
/// not every frame, and owns no credentials or gameplay commands.
/// </summary>
public sealed class NavigationDiagnosticsMonitor
{
    private readonly object sync = new();
    private readonly int capacity;
    private readonly Dictionary<string, NavigationProviderDiagnostic> previous = new(StringComparer.Ordinal);
    private readonly List<NavigationAuditEntry> audit = [];
    private NavigationDiagnosticsSnapshot current = new(
        DateTimeOffset.MinValue,
        [],
        []);

    public NavigationDiagnosticsMonitor(int capacity = 32)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
    }

    public NavigationDiagnosticsSnapshot Current
    {
        get
        {
            lock (sync)
                return current;
        }
    }

    public NavigationDiagnosticsSnapshot Observe(
        IEnumerable<NavigationProviderDiagnostic> providers,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(providers);
        NavigationProviderDiagnostic[] next = providers.ToArray();
        if (next.Any(provider => string.IsNullOrWhiteSpace(provider.Id)))
            throw new ArgumentException("Every provider diagnostic requires an ID.", nameof(providers));
        if (next.Select(provider => provider.Id).Distinct(StringComparer.Ordinal).Count() != next.Length)
            throw new ArgumentException("Provider diagnostic IDs must be unique.", nameof(providers));

        lock (sync)
        {
            foreach (NavigationProviderDiagnostic provider in next)
            {
                if (previous.TryGetValue(provider.Id, out NavigationProviderDiagnostic? prior) &&
                    prior.State == provider.State &&
                    string.Equals(prior.Code, provider.Code, StringComparison.Ordinal))
                {
                    previous[provider.Id] = provider;
                    continue;
                }

                audit.Add(new NavigationAuditEntry(
                    now,
                    provider.Id,
                    provider.DisplayName,
                    provider.State,
                    provider.Code,
                    provider.Detail));
                previous[provider.Id] = provider;
            }

            if (audit.Count > capacity)
                audit.RemoveRange(0, audit.Count - capacity);

            current = new NavigationDiagnosticsSnapshot(
                now,
                next,
                audit.AsEnumerable().Reverse().ToArray());
            return current;
        }
    }
}
