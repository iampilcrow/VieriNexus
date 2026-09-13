using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VieriNexus.Application;

public enum QuestionableRouteCorrectionState
{
    Applied,
    AlreadyCorrect,
    Missing,
    Conflict,
}

public sealed record QuestionableRouteCorrectionTarget(
    int QuestId,
    string QuestName,
    string FilePrefix);

public sealed record QuestionableRouteCorrectionResult(
    QuestionableRouteCorrectionTarget Target,
    QuestionableRouteCorrectionState State,
    string Message,
    string? CorrectedJson = null);

public enum QuestionableCompatibilityInstallState
{
    Installed,
    ManagedActive,
    AlreadyCorrect,
    Conflict,
    Failed,
}

public sealed record QuestionableCompatibilityInstallResult(
    QuestionableCompatibilityInstallState State,
    string Message,
    string? BundleSha256 = null,
    IReadOnlyList<QuestionableRouteCorrectionResult>? Corrections = null)
{
    public bool IsSatisfied => State is QuestionableCompatibilityInstallState.Installed or
        QuestionableCompatibilityInstallState.ManagedActive or
        QuestionableCompatibilityInstallState.AlreadyCorrect;
}

public sealed record QuestionableCompatibilityReceipt(
    int SchemaVersion,
    long DataVersion,
    string OriginalSha256,
    string PatchedSha256,
    string BackupPath,
    DateTimeOffset AppliedAtUtc,
    IReadOnlyList<int> QuestIds);

/// <summary>
/// Applies the finite Nexus-owned route corrections to a complete stock Questionable path bundle.
/// Every edit is guarded by exact quest/sequence/step identity. Unknown upstream changes fail closed.
/// </summary>
public sealed class QuestionableRouteCompatibilityPack
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static IReadOnlyList<QuestionableRouteCorrectionTarget> Targets { get; } =
    [
        new(1063, "A Slave to the Aether", "1063_"),
        new(2893, "Sleeping Truths Lie", "2893_"),
        new(2565, "The Face of True Evil", "2565_"),
        new(4068, "Sage's Focus", "4068_"),
        new(4645, "The Land, Wind, and Sea", "4645_"),
    ];

    public static bool IsManagedQuest(string questId) =>
        int.TryParse(questId, out int parsed) && Targets.Any(target => target.QuestId == parsed);

    public QuestionableRouteCorrectionResult Correct(
        QuestionableRouteCorrectionTarget target,
        string json)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException exception)
        {
            return Conflict(target, $"The stock route is not valid JSON: {exception.Message}");
        }

        if (root?["QuestSequence"] is not JsonArray)
            return Conflict(target, "The stock route has no readable QuestSequence array.");

        return target.QuestId switch
        {
            1063 => CorrectNextQuest(target, root),
            2893 => CorrectFly(target, root, 2, 2008682, 612, "Combat"),
            2565 => CorrectSoloDuty(target, root),
            4068 => CorrectSageApproaches(target, root),
            4645 => CorrectIslandMounts(target, root),
            _ => Conflict(target, "The route is not part of the managed compatibility catalog."),
        };
    }

    public QuestionableCompatibilityInstallResult Install(
        string bundlePath,
        string backupRoot,
        string receiptPath)
    {
        if (!File.Exists(bundlePath))
            return Failed("Questionable's downloaded path bundle is not available yet.");

        try
        {
            string originalSha = HashFile(bundlePath);
            QuestionableCompatibilityReceipt? priorReceipt = ReadReceipt(receiptPath);
            var corrections = new List<QuestionableRouteCorrectionResult>(Targets.Count);
            var entries = new Dictionary<int, string>();
            long dataVersion;

            using (ZipArchive archive = ZipFile.OpenRead(bundlePath))
            {
                dataVersion = ReadDataVersion(archive);
                foreach (QuestionableRouteCorrectionTarget target in Targets)
                {
                    ZipArchiveEntry[] matching = archive.Entries
                        .Where(entry => entry.Name.StartsWith(target.FilePrefix, StringComparison.Ordinal) &&
                                        entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    if (matching.Length != 1)
                    {
                        corrections.Add(new(target, QuestionableRouteCorrectionState.Missing,
                            matching.Length == 0
                                ? "The exact quest route was not present in the stock bundle."
                                : "More than one stock route matched the exact quest ID."));
                        continue;
                    }

                    entries[target.QuestId] = matching[0].FullName;
                    using StreamReader reader = new(matching[0].Open(), Encoding.UTF8, true);
                    corrections.Add(Correct(target, reader.ReadToEnd()));
                }
            }

            QuestionableRouteCorrectionResult? conflict = corrections.FirstOrDefault(result =>
                result.State is QuestionableRouteCorrectionState.Missing or QuestionableRouteCorrectionState.Conflict);
            if (conflict is not null)
                return new(QuestionableCompatibilityInstallState.Conflict,
                    $"Nexus left Questionable untouched because {conflict.Target.QuestName} changed unexpectedly: {conflict.Message}",
                    originalSha, corrections);

            if (corrections.All(result => result.State == QuestionableRouteCorrectionState.AlreadyCorrect))
            {
                bool managed = priorReceipt is not null &&
                               string.Equals(priorReceipt.PatchedSha256, originalSha, StringComparison.OrdinalIgnoreCase);
                return new(
                    managed ? QuestionableCompatibilityInstallState.ManagedActive : QuestionableCompatibilityInstallState.AlreadyCorrect,
                    managed
                        ? "All five Nexus compatibility corrections are installed in Questionable's route bundle."
                        : "All five route corrections are already present; Nexus does not need to modify Questionable.",
                    originalSha, corrections);
            }

            Directory.CreateDirectory(backupRoot);
            string backupPath = Path.Combine(backupRoot, $"questionable-paths-{dataVersion}-{originalSha}.zip");
            if (!File.Exists(backupPath))
                File.Copy(bundlePath, backupPath);

            string tempPath = bundlePath + $".nexus-{Guid.NewGuid():N}.tmp";
            try
            {
                File.Copy(bundlePath, tempPath, true);
                using (ZipArchive archive = ZipFile.Open(tempPath, ZipArchiveMode.Update))
                {
                    foreach (QuestionableRouteCorrectionResult correction in corrections.Where(result =>
                                 result.State == QuestionableRouteCorrectionState.Applied))
                    {
                        string entryName = entries[correction.Target.QuestId];
                        ZipArchiveEntry entry = archive.GetEntry(entryName)
                                                ?? throw new InvalidDataException($"Route entry disappeared: {entryName}");
                        entry.Delete();
                        ZipArchiveEntry replacement = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                        using StreamWriter writer = new(replacement.Open(), Utf8NoBom);
                        writer.Write(correction.CorrectedJson);
                    }
                }

                VerifyCorrectedBundle(tempPath);
                string patchedSha = HashFile(tempPath);
                if (!string.Equals(HashFile(bundlePath), originalSha, StringComparison.OrdinalIgnoreCase))
                {
                    return new(QuestionableCompatibilityInstallState.Conflict,
                        "Questionable installed newer route data while Nexus was preparing the compatibility pack; Nexus kept the newer bundle and will recheck it.",
                        originalSha, corrections);
                }
                File.Move(tempPath, bundlePath, true);
                var receipt = new QuestionableCompatibilityReceipt(
                    1,
                    dataVersion,
                    originalSha,
                    patchedSha,
                    backupPath,
                    DateTimeOffset.UtcNow,
                    Targets.Select(target => target.QuestId).ToArray());
                WriteReceipt(receiptPath, receipt);
                return new(QuestionableCompatibilityInstallState.Installed,
                    $"Installed {corrections.Count(result => result.State == QuestionableRouteCorrectionState.Applied)} Nexus route correction(s) while preserving the complete stock bundle.",
                    patchedSha, corrections);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or
                                          InvalidOperationException or JsonException)
        {
            return Failed($"Nexus could not prepare the Questionable compatibility pack: {exception.Message}");
        }
    }

    public QuestionableCompatibilityInstallResult RestoreOfficialBundle(string bundlePath, string receiptPath)
    {
        try
        {
            QuestionableCompatibilityReceipt? receipt = ReadReceipt(receiptPath);
            if (receipt is null)
                return Failed("No Nexus-managed Questionable bundle receipt is available.");
            if (!File.Exists(bundlePath) || !File.Exists(receipt.BackupPath))
                return Failed("The active bundle or its exact official backup is missing.");
            if (!string.Equals(HashFile(bundlePath), receipt.PatchedSha256, StringComparison.OrdinalIgnoreCase))
                return new(QuestionableCompatibilityInstallState.Conflict,
                    "Questionable's route bundle changed after Nexus applied the pack; Nexus will not overwrite the newer data.");
            if (!string.Equals(HashFile(receipt.BackupPath), receipt.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                return Failed("The official rollback bundle no longer matches its recorded hash.");

            string tempPath = bundlePath + $".restore-{Guid.NewGuid():N}.tmp";
            try
            {
                File.Copy(receipt.BackupPath, tempPath, true);
                if (!string.Equals(HashFile(bundlePath), receipt.PatchedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return new(QuestionableCompatibilityInstallState.Conflict,
                        "Questionable installed newer route data while Nexus was preparing the restore; Nexus kept the newer bundle.");
                }
                File.Move(tempPath, bundlePath, true);
                return new(QuestionableCompatibilityInstallState.AlreadyCorrect,
                    "Restored the exact official Questionable route bundle. Nexus compatibility management is disabled.",
                    receipt.OriginalSha256);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Failed($"Nexus could not restore the official Questionable bundle: {exception.Message}");
        }
    }

    private QuestionableRouteCorrectionResult CorrectNextQuest(
        QuestionableRouteCorrectionTarget target,
        JsonObject root)
    {
        JsonObject? step = FindStep(root, 255, 1006749, 131, "CompleteQuest");
        if (step is null)
            return Conflict(target, "The expected completion step was not found exactly once.");
        int? value = IntValue(step["NextQuestId"]);
        if (value == 1064)
            return Correct(target);
        if (value != 1604)
            return Conflict(target, $"Expected NextQuestId 1604 or 1064, found {value?.ToString() ?? "no value"}.");
        step["NextQuestId"] = 1064;
        return Applied(target, root, "Corrected the guarded next quest from 1604 to 1064.");
    }

    private QuestionableRouteCorrectionResult CorrectFly(
        QuestionableRouteCorrectionTarget target,
        JsonObject root,
        int sequence,
        int dataId,
        int territoryId,
        string interactionType)
    {
        JsonObject? step = FindStep(root, sequence, dataId, territoryId, interactionType);
        if (step is null)
            return Conflict(target, "The expected long combat approach was not found exactly once.");
        return SetTrueWhenMissing(target, root, step, "Fly", "Retained flight for the long combat approach.");
    }

    private QuestionableRouteCorrectionResult CorrectSoloDuty(
        QuestionableRouteCorrectionTarget target,
        JsonObject root)
    {
        JsonObject? step = FindStep(root, 3, 1021894, 397, "SinglePlayerDuty");
        if (step is null)
            return Conflict(target, "The expected solo-duty step was not found exactly once.");
        if (step["SinglePlayerDutyOptions"] is JsonObject options)
        {
            bool? enabled = BoolValue(options["Enabled"]);
            if (enabled == true)
                return Correct(target);
            if (enabled == false)
                return Conflict(target, "The stock route explicitly disables the solo duty.");
            options["Enabled"] = true;
        }
        else if (step["SinglePlayerDutyOptions"] is null)
        {
            step["SinglePlayerDutyOptions"] = new JsonObject { ["Enabled"] = true };
        }
        else
        {
            return Conflict(target, "SinglePlayerDutyOptions has an unexpected shape.");
        }
        return Applied(target, root, "Enabled the supported solo duty explicitly.");
    }

    private QuestionableRouteCorrectionResult CorrectSageApproaches(
        QuestionableRouteCorrectionTarget target,
        JsonObject root)
    {
        JsonObject? duty = FindStep(root, 6, 1039248, 399, "SinglePlayerDuty");
        JsonObject? resume = FindStep(root, 8, 1039248, 399, "Interact");
        if (duty is null || resume is null)
            return Conflict(target, "The two expected resumable duty approaches were not found exactly once.");
        bool? dutyFly = BoolValue(duty["Fly"]);
        bool? resumeFly = BoolValue(resume["Fly"]);
        if (dutyFly == true && resumeFly == true)
            return Correct(target);
        if (dutyFly == false || resumeFly == false)
            return Conflict(target, "The stock route explicitly disables flight on a protected approach.");
        duty["Fly"] = true;
        resume["Fly"] = true;
        return Applied(target, root, "Retained flight on both resumable approaches to the solo duty.");
    }

    private QuestionableRouteCorrectionResult CorrectIslandMounts(
        QuestionableRouteCorrectionTarget target,
        JsonObject root)
    {
        JsonObject? accept = FindStep(root, 0, 1044156, 1055, "AcceptQuest");
        JsonObject? complete = FindStep(root, 255, 2013035, 1055, "CompleteQuest");
        if (accept is null || complete is null)
            return Conflict(target, "The two expected indoor quest steps were not found exactly once.");
        bool? acceptMount = BoolValue(accept["Mount"]);
        bool? completeMount = BoolValue(complete["Mount"]);
        if (acceptMount == false && completeMount == false)
            return Correct(target);
        if (acceptMount != true || completeMount != true)
            return Conflict(target, "The protected indoor mount values changed to an unexpected shape upstream.");
        accept["Mount"] = false;
        complete["Mount"] = false;
        return Applied(target, root, "Disabled mounting at the indoor acceptance and completion points.");
    }

    private QuestionableRouteCorrectionResult SetTrueWhenMissing(
        QuestionableRouteCorrectionTarget target,
        JsonObject root,
        JsonObject step,
        string property,
        string message)
    {
        bool? value = BoolValue(step[property]);
        if (value == true)
            return Correct(target);
        if (value == false)
            return Conflict(target, $"The stock route explicitly sets {property} to false.");
        step[property] = true;
        return Applied(target, root, message);
    }

    private static JsonObject? FindStep(
        JsonObject root,
        int sequence,
        int dataId,
        int territoryId,
        string interactionType)
    {
        if (root["QuestSequence"] is not JsonArray sequences)
            return null;
        JsonObject[] matches = sequences.OfType<JsonObject>()
            .Where(item => IntValue(item["Sequence"]) == sequence)
            .SelectMany(item => (item["Steps"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .Where(step => IntValue(step["DataId"]) == dataId &&
                           IntValue(step["TerritoryId"]) == territoryId &&
                           string.Equals(StringValue(step["InteractionType"]), interactionType,
                               StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static int? IntValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out int number) ? number : null;

    private static bool? BoolValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out bool result) ? result : null;

    private static string? StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? result) ? result : null;

    private static QuestionableRouteCorrectionResult Applied(
        QuestionableRouteCorrectionTarget target,
        JsonObject root,
        string message) =>
        new(target, QuestionableRouteCorrectionState.Applied, message, root.ToJsonString(JsonOptions));

    private static QuestionableRouteCorrectionResult Correct(QuestionableRouteCorrectionTarget target) =>
        new(target, QuestionableRouteCorrectionState.AlreadyCorrect, "The correction is already present.");

    private static QuestionableRouteCorrectionResult Conflict(
        QuestionableRouteCorrectionTarget target,
        string message) =>
        new(target, QuestionableRouteCorrectionState.Conflict, message);

    private static QuestionableCompatibilityInstallResult Failed(string message) =>
        new(QuestionableCompatibilityInstallState.Failed, message);

    private void VerifyCorrectedBundle(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        foreach (QuestionableRouteCorrectionTarget target in Targets)
        {
            ZipArchiveEntry entry = archive.Entries.Single(item =>
                item.Name.StartsWith(target.FilePrefix, StringComparison.Ordinal) &&
                item.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            using StreamReader reader = new(entry.Open(), Encoding.UTF8, true);
            QuestionableRouteCorrectionResult verification = Correct(target, reader.ReadToEnd());
            if (verification.State != QuestionableRouteCorrectionState.AlreadyCorrect)
                throw new InvalidDataException($"Corrected route verification failed for {target.QuestName}.");
        }
    }

    private static long ReadDataVersion(ZipArchive archive)
    {
        ZipArchiveEntry? manifest = archive.GetEntry("manifest.json");
        if (manifest is null)
            return 0;
        using Stream stream = manifest.Open();
        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement.TryGetProperty("DataVersion", out JsonElement value) &&
               value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long result)
            ? result
            : 0;
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static QuestionableCompatibilityReceipt? ReadReceipt(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<QuestionableCompatibilityReceipt>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteReceipt(string path, QuestionableCompatibilityReceipt receipt)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tempPath = path + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(receipt, JsonOptions), Utf8NoBom);
        File.Move(tempPath, path, true);
    }
}
