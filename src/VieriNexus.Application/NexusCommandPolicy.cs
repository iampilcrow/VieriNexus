namespace VieriNexus.Application;

public sealed record NexusCommandRequest(
    int ContractVersion,
    Guid RequestId,
    string Command,
    string PayloadJson,
    ulong? CharacterContentId);

public sealed record NexusCommandValidation(
    bool IsValid,
    string Command,
    string Code,
    string Message);

/// <summary>
/// Pure validation for local and remote Nexus control. Mutation requests are character-scoped,
/// bounded, and normalized before a runtime is allowed to interpret their payload.
/// </summary>
public static class NexusCommandPolicy
{
    public const int ContractVersion = 1;
    public const int MaximumPayloadLength = 8_192;

    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["start"] = "progression.start",
            ["resume"] = "progression.resume",
            ["last"] = "progression.last",
            ["repair"] = "maintenance.repair",
            ["extract"] = "maintenance.extract",
            ["register"] = "maintenance.register",
            ["coffers"] = "maintenance.coffers",
            ["desynth"] = "maintenance.desynth",
            ["gcturnin"] = "maintenance.gc",
            ["storage"] = "maintenance.storage",
            ["maintenance"] = "maintenance.run",
            ["sell"] = "maintenance.sell-review",
        };

    private static readonly HashSet<string> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        "status",
        "stop",
        "progression.start",
        "progression.resume",
        "progression.last",
        "progression.stop",
        "maintenance.run",
        "maintenance.repair",
        "maintenance.extract",
        "maintenance.register",
        "maintenance.coffers",
        "maintenance.desynth",
        "maintenance.gc",
        "maintenance.storage",
        "maintenance.sell-review",
        "job.switch",
        "route.play",
        "route.preview",
        "ui.open",
    };

    public static NexusCommandValidation Validate(NexusCommandRequest request, ulong currentContentId)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContractVersion != ContractVersion)
            return Invalid("unsupported-contract", $"Nexus command contract {request.ContractVersion} is not supported.");
        if (request.RequestId == Guid.Empty)
            return Invalid("missing-request-id", "Every Nexus command needs a non-empty request ID.");
        string command = (request.Command ?? string.Empty).Trim();
        if (command.Length == 0 || command.Length > 64)
            return Invalid("invalid-command", "The Nexus command name is missing or too long.");
        if (Aliases.TryGetValue(command, out string? canonical))
            command = canonical;
        if (!Commands.Contains(command))
            return Invalid("unknown-command", $"Nexus does not expose the '{command}' command.");
        string payload = request.PayloadJson ?? string.Empty;
        if (payload.Length > MaximumPayloadLength)
            return Invalid("payload-too-large", "The Nexus command payload exceeds the 8 KiB safety limit.");

        bool readOnlyOrUi = command is "status" or "ui.open";
        if (!readOnlyOrUi && currentContentId == 0)
            return Invalid("character-unavailable", "A logged-in character is required for this Nexus command.");
        if (request.CharacterContentId is { } expected && expected != currentContentId)
            return Invalid("character-mismatch", "The command was scoped to a different character and was not run.");

        return new NexusCommandValidation(true, command.ToLowerInvariant(), "accepted", "The command request is valid.");
    }

    private static NexusCommandValidation Invalid(string code, string message) =>
        new(false, string.Empty, code, message);
}
