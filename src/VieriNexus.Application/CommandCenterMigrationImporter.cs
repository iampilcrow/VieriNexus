using System.Text.Json;

namespace VieriNexus.Application;

public sealed class CommandCenterMigrationImporter
{
    private const int MaximumEntries = 2_000;

    public CommandCenterMigrationPreview Preview(string json)
    {
        List<MigrationIssue> issues = [];
        if (string.IsNullOrWhiteSpace(json))
            return new(null, [new(MigrationIssueSeverity.Error, "The VieriDeck configuration is empty.")]);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new(null, [new(MigrationIssueSeverity.Error, "The VieriDeck configuration root is invalid.")]);

            int version = Number(root, "Version", 1);
            string[] favorites = StringSet(root, "Favorites", issues);
            string[] hidden = StringSet(root, "HiddenPlugins", issues);
            Dictionary<string, string> preferred = StringDictionary(root, "PreferredCommands", issues);
            Dictionary<string, IReadOnlyList<CommandCenterCustomCommand>> custom = CustomCommands(root, issues);
            if (favorites.Length + hidden.Length + preferred.Count + custom.Sum(pair => pair.Value.Count) > MaximumEntries)
                issues.Add(new(MigrationIssueSeverity.Error, "The VieriDeck configuration exceeds the 2,000-entry safety limit."));

            CommandCenterSnapshot snapshot = new(
                1,
                version,
                Boolean(root, "ShowUnloadedPlugins", true),
                Boolean(root, "HidePluginsWithoutActions", false),
                Boolean(root, "CloseDeckAfterOpeningPlugin", false),
                Boolean(root, "CommandPanelOpen", true),
                Boolean(root, "OnlyShowFavorites", false),
                Text(root, "SelectedCommandPluginId"),
                Float(root, "ListWindowWidth", 455f, 200f, 1_200f),
                Float(root, "CommandPanelWidth", 650f, 210f, 1_800f),
                Float(root, "WindowHeight", 680f, 180f, 1_200f),
                Float(root, "UiScale", 1f, .75f, 1.6f),
                Boolean(root, "HasDeckPosition", false),
                Float(root, "DeckPositionX", 0f, -50_000f, 50_000f),
                Float(root, "DeckPositionY", 0f, -50_000f, 50_000f),
                Boolean(root, "HotkeyEnabled", true),
                Number(root, "Hotkey", 0),
                Boolean(root, "HotkeyControl", false),
                Boolean(root, "HotkeyShift", false),
                Boolean(root, "HotkeyAlt", false),
                Boolean(root, "ExactModifiers", true),
                favorites,
                hidden,
                preferred,
                custom);
            if (!CommandCenterWorkingStore.IsValid(snapshot))
                issues.Add(new(MigrationIssueSeverity.Error, "The VieriDeck settings could not be normalized safely."));
            else
                issues.Add(new(MigrationIssueSeverity.Information,
                    "Favorites, hidden plugins, preferred commands, custom commands, filters, window layout reference, and hotkey were mapped."));
            return new(issues.Any(issue => issue.Severity == MigrationIssueSeverity.Error) ? null : snapshot, issues);
        }
        catch (JsonException)
        {
            return new(null, [new(MigrationIssueSeverity.Error, "The VieriDeck configuration is not valid JSON.")]);
        }
    }

    private static bool Boolean(JsonElement root, string name, bool fallback) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    private static int Number(JsonElement root, string name, int fallback) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
            ? number
            : fallback;

    private static float Float(JsonElement root, string name, float fallback, float minimum, float maximum) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float number) && float.IsFinite(number)
            ? Math.Clamp(number, minimum, maximum)
            : fallback;

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? (value.GetString() ?? string.Empty).Trim()
            : string.Empty;

    private static string[] StringSet(JsonElement root, string name, List<MigrationIssue> issues)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new(MigrationIssueSeverity.Warning, $"Ignored malformed {name}."));
            return [];
        }
        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => (item.GetString() ?? string.Empty).Trim())
            .Where(item => item.Length is > 0 and <= 200)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumEntries)
            .ToArray();
    }

    private static Dictionary<string, string> StringDictionary(JsonElement root, string name, List<MigrationIssue> issues)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return result;
        if (value.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new(MigrationIssueSeverity.Warning, $"Ignored malformed {name}."));
            return result;
        }
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.NameEquals("$type") || property.Value.ValueKind != JsonValueKind.String)
                continue;
            string command = (property.Value.GetString() ?? string.Empty).Trim();
            if (property.Name.Length is > 0 and <= 200 && command.Length is > 0 and <= 1_024)
                result[property.Name] = NormalizeCommand(command);
        }
        return result;
    }

    private static Dictionary<string, IReadOnlyList<CommandCenterCustomCommand>> CustomCommands(
        JsonElement root,
        List<MigrationIssue> issues)
    {
        Dictionary<string, IReadOnlyList<CommandCenterCustomCommand>> result = new(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("CustomCommands", out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return result;
        if (value.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new(MigrationIssueSeverity.Warning, "Ignored malformed CustomCommands."));
            return result;
        }
        foreach (JsonProperty group in value.EnumerateObject())
        {
            if (group.NameEquals("$type") || group.Value.ValueKind != JsonValueKind.Array || group.Name.Length is 0 or > 200)
                continue;
            CommandCenterCustomCommand[] commands = group.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => new CommandCenterCustomCommand(
                    NormalizeCommand(Text(item, "Command")),
                    Text(item, "Description")))
                .Where(item => item.Command.Length is > 1 and <= 1_024 && item.Description.Length <= 2_048)
                .DistinctBy(item => item.Command, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumEntries)
                .ToArray();
            if (commands.Length > 0)
                result[group.Name] = commands;
        }
        return result;
    }

    private static string NormalizeCommand(string command)
    {
        command = command.Trim();
        return command.Length > 0 && command[0] != '/' ? "/" + command : command;
    }
}
