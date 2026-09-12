using System.Text.Json;

namespace VieriNexus.Application;

public sealed class CodexMigrationImporter
{
    public CodexMigrationPreview Preview(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new(null, [new(MigrationIssueSeverity.Error, "The VieriCodex configuration is empty.")]);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new(null, [new(MigrationIssueSeverity.Error, "The VieriCodex configuration root is invalid.")]);

            JsonElement codex = Object(root, "Codex");
            if (codex.ValueKind != JsonValueKind.Object)
                return new(null, [new(MigrationIssueSeverity.Error, "The VieriCodex progression preferences were not found.")]);

            JsonElement stop = Object(root, "Stop");
            JsonElement queue = Object(root, "ProgressionQueue");
            int targetLevel = Math.Clamp(Number(stop, "TargetLevel", 50), 1, 100);
            CodexQueueStepSnapshot[] queueSteps = QueueSteps(queue);
            CodexQueueSettingsSnapshot queueSettings = QueueSettings(Object(queue, "Settings"));
            CodexMigrationSnapshot snapshot = new(
                1,
                Number(root, "Version", 1),
                Boolean(codex, "MainScenarioQuests"),
                Boolean(codex, "CombatClassJobQuests"),
                Boolean(codex, "RoleQuests"),
                Boolean(codex, "AetherCurrentQuests"),
                Boolean(codex, "FieldAetherCurrents"),
                Boolean(codex, "AetheryteAttunements"),
                Boolean(codex, "MapExploration"),
                Boolean(codex, "HuntingLogs"),
                Boolean(codex, "SideQuests"),
                Boolean(codex, "Achievements"),
                Boolean(codex, "RequiredMsqDungeons", true),
                Boolean(stop, "LevelToStopAfter"),
                targetLevel,
                queueSteps,
                queueSettings);

            List<MigrationIssue> issues =
            [
                new(MigrationIssueSeverity.Information,
                    "Main Scenario, Class/Job/Role, logs, currents, travel-node, exploration, side-quest, achievement, duty, and level-stop preferences were mapped."),
            ];
            if (queueSteps.Length > 0)
                issues.Add(new(MigrationIssueSeverity.Warning,
                    "Saved VieriCodex queue steps were preserved in staging for reference; Nexus will not resume a predecessor instruction pointer."));
            return new(snapshot, issues);
        }
        catch (JsonException)
        {
            return new(null, [new(MigrationIssueSeverity.Error, "The VieriCodex configuration is not valid JSON.")]);
        }
    }

    private static JsonElement Object(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static bool Boolean(JsonElement parent, string name, bool fallback = false) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    private static int Number(JsonElement parent, string name, int fallback) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int number)
            ? number
            : fallback;

    private static CodexQueueStepSnapshot[] QueueSteps(JsonElement queue)
    {
        if (queue.ValueKind != JsonValueKind.Object ||
            !queue.TryGetProperty("Steps", out JsonElement value) ||
            value.ValueKind != JsonValueKind.Array)
            return [];
        return value.EnumerateArray()
            .Where(step => step.ValueKind == JsonValueKind.Object)
            .Take(100)
            .Select(step => new CodexQueueStepSnapshot(
                GuidValue(step, "Id"),
                Boolean(step, "Enabled", true),
                (uint)Math.Clamp(Number(step, "ClassJob", 0), 0, 43),
                Math.Clamp(Number(step, "TargetLevel", 1), 1, 100),
                Math.Clamp(Number(step, "Method", 0), 0, 3),
                Math.Clamp(Number(step, "FallbackPolicy", 0), 0, 6)))
            .ToArray();
    }

    private static CodexQueueSettingsSnapshot QueueSettings(JsonElement settings) => new(
        Boolean(settings, "SkipTargetsAlreadyReached", true),
        Boolean(settings, "AutomaticallySwitchJobs", true),
        Boolean(settings, "AutomaticallyAdvance", true),
        Boolean(settings, "ResumeAfterRestart", true),
        Boolean(settings, "AutomaticUsesHuntingLog", true),
        Boolean(settings, "AutomaticUsesSideQuests", true),
        Boolean(settings, "AutomaticUsesDungeonGrind", true),
        Math.Clamp(Number(settings, "OnStepFailure", 0), 0, 6));

    private static Guid GuidValue(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String &&
        Guid.TryParse(value.GetString(), out Guid id)
            ? id
            : Guid.Empty;
}
