using System.Globalization;
using System.Text.Json;

namespace VieriNexus.Application;

public sealed class AutoDutyMigrationImporter
{
    public AutoDutyMigrationPreview Preview(string json)
    {
        var issues = new List<MigrationIssue>();
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Error("The VieriAutoDuty configuration root is invalid.");

            string defaultProfile = Text(root, "DefaultConfigName", "Bare");
            if (!root.TryGetProperty("profileData", out JsonElement profilesElement) ||
                profilesElement.ValueKind != JsonValueKind.Array)
                return Error("No VieriAutoDuty profiles were found in the configuration.");

            var profiles = new List<AutoDutyProfileSnapshot>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement profileElement in profilesElement.EnumerateArray())
            {
                if (profileElement.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new(MigrationIssueSeverity.Error,
                        "A VieriAutoDuty profile entry is not a JSON object."));
                    continue;
                }

                string name = Text(profileElement, "Name", string.Empty).Trim();
                if (name.Length == 0 || !names.Add(name))
                {
                    issues.Add(new(MigrationIssueSeverity.Error,
                        name.Length == 0 ? "A VieriAutoDuty profile has no name." : $"The VieriAutoDuty profile name '{name}' is duplicated."));
                    continue;
                }
                if (!profileElement.TryGetProperty("Config", out JsonElement config) || config.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new(MigrationIssueSeverity.Error, $"Profile '{name}' has no readable configuration."));
                    continue;
                }

                profiles.Add(new AutoDutyProfileSnapshot(
                    name,
                    ULongArray(profileElement, "CIDs"),
                    ReadOverlay(config),
                    ReadMaintenance(config)));
            }

            if (profiles.Count == 0)
                issues.Add(new(MigrationIssueSeverity.Error, "No usable VieriAutoDuty profiles were found."));
            if (profiles.Count > 0 && !profiles.Any(profile => string.Equals(profile.Name, defaultProfile, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new(MigrationIssueSeverity.Warning,
                    $"The saved default profile '{defaultProfile}' was not present; Nexus will retain every profile without choosing a replacement."));
            }

            var retired = new List<string>();
            if (root.TryGetProperty("RetiredEquipmentTransfers", out JsonElement retiredElement) &&
                retiredElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement entry in retiredElement.EnumerateArray())
                    retired.Add(entry.GetRawText());
            }

            AutoDutyMigrationSnapshot? snapshot = issues.Any(issue => issue.Severity == MigrationIssueSeverity.Error)
                ? null
                : new AutoDutyMigrationSnapshot(1, defaultProfile, profiles, retired);
            if (snapshot is not null)
            {
                issues.Add(new(MigrationIssueSeverity.Information,
                    $"Mapped {profiles.Count} profile(s), {profiles.Sum(profile => profile.CharacterIds.Count)} character assignment(s), and {retired.Count} pending retired-item transfer(s)."));
            }
            return new(snapshot, issues);
        }
        catch (JsonException)
        {
            return Error("The VieriAutoDuty configuration is not valid JSON.");
        }
        catch (InvalidOperationException)
        {
            return Error("The VieriAutoDuty configuration contains a value with an unsupported JSON type.");
        }
    }

    private static AutoDutyOverlayPreferences ReadOverlay(JsonElement value) => new(
        Bool(value, "ShowOverlay", true),
        Bool(value, "HideOverlayWhenStopped", false),
        Bool(value, "LockOverlay", false),
        Bool(value, "OverlayNoBG", false),
        Bool(value, "OverlayAnchorBottom", false),
        Bool(value, "ShowDutyLoopText", true),
        Bool(value, "ShowActionText", true),
        Bool(value, "GotoButton", true),
        Bool(value, "EquipButton", true),
        Bool(value, "RepairButton", true),
        Bool(value, "ExtractButton", true),
        Bool(value, "DesynthButton", true),
        Bool(value, "SellButton", true),
        Bool(value, "TurninButton", true),
        Bool(value, "CofferButton", true),
        Bool(value, "TTButton", true));

    private static AutoDutyMaintenancePolicy ReadMaintenance(JsonElement value) => new(
        Bool(value, "AutoBuyGilVendorGear", false),
        UInt(value, "AutoBuyGilVendorKeepGil", 0),
        Bool(value, "AutoEquipRecommendedGear", false),
        Bool(value, "AutoRepair", false),
        UInt(value, "AutoRepairPct", 50),
        Bool(value, "AutoRepairSelf", false),
        RawObject(value, "PreferredRepairNPC"),
        Bool(value, "AutoExtract", false),
        Bool(value, "AutoExtractAll", false),
        Bool(value, "AutoOpenCoffers", false),
        NullableByte(value, "AutoOpenCoffersGearset"),
        Bool(value, "AutoOpenCoffersBlacklistUse", false),
        UIntStringDictionary(value, "AutoOpenCoffersBlacklist"),
        Bool(value, "AutoDesynth", false),
        Bool(value, "AutoDesynthSkillUp", false),
        Int(value, "AutoDesynthSkillUpLimit", 50),
        Bool(value, "AutoDesynthNQOnly", false),
        Bool(value, "AutoDesynthNoGearset", true),
        ULong(value, "AutoDesynthCategories", 1),
        Bool(value, "AutoGCTurnin", false),
        Bool(value, "AutoGCTurninSlotsLeftBool", false),
        Int(value, "AutoGCTurninSlotsLeft", 5),
        Bool(value, "AutoGCTurninUseTicket", false),
        Bool(value, "ArmoireEntrust", false),
        Bool(value, "GlamourChestEntrust", false),
        Bool(value, "TripleTriadRegister", false),
        Bool(value, "MinionRegister", false),
        Bool(value, "OrchestrionRegister", false),
        Bool(value, "TripleTriadSell", false),
        Int(value, "TripleTriadSellMinItemCount", 1),
        Int(value, "TripleTriadSellMinSlotCount", 1),
        Bool(value, "AutoSell", false),
        ScalarText(value, "AutoSellMode", "UnneededEquipment"),
        Bool(value, "AutoSellUseOccupiedSlots", true),
        Int(value, "AutoSellOccupiedSlots", 120),
        Bool(value, "AutoSellUseBagPercent", true),
        Int(value, "AutoSellBagPercent", 85),
        Bool(value, "AutoSellProtectGearsets", true),
        RawObject(value, "PreferredSellNPC"),
        Bool(value, "InDutyMaintenanceEnabled", false),
        Bool(value, "InDutyDurabilityEnabled", true),
        Int(value, "InDutyDurabilityPercent", 20),
        Bool(value, "InDutyInventoryEnabled", true),
        Bool(value, "InDutyExtractBeforeSelling", false),
        Bool(value, "InDutyDesynthBeforeSelling", false),
        Bool(value, "InDutyReturnToInnAfterMaintenance", false));

    private static AutoDutyMigrationPreview Error(string message) =>
        new(null, [new MigrationIssue(MigrationIssueSeverity.Error, message)]);

    private static string Text(JsonElement parent, string name, string fallback) =>
        parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static string ScalarText(JsonElement parent, string name, string fallback)
    {
        if (!parent.TryGetProperty(name, out JsonElement value))
            return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Null or JsonValueKind.Undefined => fallback,
            _ => value.GetRawText(),
        };
    }

    private static bool Bool(JsonElement parent, string name, bool fallback) =>
        parent.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    private static int Int(JsonElement parent, string name, int fallback) =>
        parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int result) ? result : fallback;

    private static uint UInt(JsonElement parent, string name, uint fallback) =>
        parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetUInt32(out uint result) ? result : fallback;

    private static ulong ULong(JsonElement parent, string name, ulong fallback) =>
        parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetUInt64(out ulong result) ? result : fallback;

    private static byte? NullableByte(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetByte(out byte result) ? result : null;

    private static IReadOnlyList<ulong> ULongArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
            return [];
        return value.EnumerateArray().Select(item =>
                item.ValueKind == JsonValueKind.Number && item.TryGetUInt64(out ulong id) ? id : 0)
            .Where(id => id != 0).Distinct().Order().ToArray();
    }

    private static IReadOnlyDictionary<uint, string> UIntStringDictionary(JsonElement parent, string name)
    {
        var result = new Dictionary<uint, string>();
        if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
            return result;
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (uint.TryParse(property.Name, NumberStyles.None, CultureInfo.InvariantCulture, out uint id))
                result[id] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.GetRawText();
        }
        return result;
    }

    private static string? RawObject(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            return null;
        return value.GetRawText();
    }
}
