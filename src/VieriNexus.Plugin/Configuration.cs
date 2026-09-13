using Dalamud.Configuration;
using Dalamud.Plugin;
using VieriNexus.Application;

namespace VieriNexus;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 9;
    public bool FirstRunComplete { get; set; }
    public bool OpenOnLogin { get; set; }
    public bool CompactNavigation { get; set; }
    public float UiScale { get; set; } = 1f;
    public string SelectedPage { get; set; } = "Home";
    public bool ShowOperationsOverlay { get; set; }
    public bool LockOperationsOverlay { get; set; }
    public bool OperationsOverlayTransparent { get; set; }
    public bool ShowOperationsStatus { get; set; } = true;
    public bool ManageQuestionableRouteCorrections { get; set; } = true;
    public bool PendingCodexQueuePromotion { get; set; }
    public Guid? AppliedOperationsReceiptId { get; set; }
    public ulong AppliedOperationsCharacterId { get; set; }
    public Dictionary<string, CharacterConfiguration> Characters { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, LegacyImportState> LegacyImports { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> EmbeddedModules { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface value)
    {
        int storedVersion = Version;
        pluginInterface = value;
        UiScale = Math.Clamp(UiScale, .8f, 1.5f);
        Characters = new Dictionary<string, CharacterConfiguration>(Characters ?? [], StringComparer.Ordinal);
        LegacyImports = new Dictionary<string, LegacyImportState>(LegacyImports ?? [], StringComparer.OrdinalIgnoreCase);
        EmbeddedModules = new Dictionary<string, bool>(EmbeddedModules ?? [], StringComparer.OrdinalIgnoreCase);
        if (storedVersion < 8 && LegacyImports.TryGetValue("codex", out LegacyImportState? codex) && codex.Activated)
            PendingCodexQueuePromotion = true;
        foreach (CharacterConfiguration character in Characters.Values)
        {
            character.Progression ??= new ProgressionDraftConfiguration();
            character.ProgressionQueue ??= new ProgressionQueueConfiguration();
            ProgressionQueuePolicy.Normalize(character.ProgressionQueue);
            character.Atlas ??= new AtlasAutomationConfiguration();
        }
        Version = 9;
    }

    public CharacterConfiguration ForCharacter(string key)
    {
        if (!Characters.TryGetValue(key, out var value))
        {
            value = new CharacterConfiguration();
            Characters[key] = value;
        }
        return value;
    }

    public LegacyImportState ForLegacyImport(string sourceId)
    {
        if (!LegacyImports.TryGetValue(sourceId, out LegacyImportState? value))
        {
            value = new LegacyImportState();
            LegacyImports[sourceId] = value;
        }
        return value;
    }

    public bool IsEmbeddedModuleEnabled(string moduleId) =>
        !EmbeddedModules.TryGetValue(moduleId, out bool enabled) || enabled;

    public void SetEmbeddedModuleEnabled(string moduleId, bool enabled) => EmbeddedModules[moduleId] = enabled;

    public void Save() => pluginInterface?.SavePluginConfig(this);
}

[Serializable]
public sealed class CharacterConfiguration
{
    public string ProfileName { get; set; } = "Default";
    public bool AllowAutomation { get; set; } = true;
    public bool PauseOnManualMovement { get; set; } = true;
    public bool PauseOnManualTarget { get; set; } = true;
    public int ManualControlQuietPeriodMs { get; set; } = 1500;
    public ProgressionDraftConfiguration Progression { get; set; } = new();
    public ProgressionQueueConfiguration ProgressionQueue { get; set; } = new();
    public AtlasAutomationConfiguration Atlas { get; set; } = new();
}

[Serializable]
public sealed class ProgressionDraftConfiguration
{
    public int TargetLevel { get; set; }
    public bool AllowMainScenario { get; set; } = true;
    public bool AllowJobQuests { get; set; } = true;
    public bool AllowHuntingLog { get; set; } = true;
    public bool AllowSideQuests { get; set; } = true;
    public bool AllowDuties { get; set; } = true;
    public int MinimumGilReserve { get; set; } = 1_000_000;
}

[Serializable]
public sealed class AtlasAutomationConfiguration
{
    public bool AllowAetherCurrentQuests { get; set; } = true;
    public bool AllowFieldAetherCurrents { get; set; } = true;
    public bool AllowAetheryteAttunements { get; set; } = true;
    public bool AllowMapExploration { get; set; } = true;
    public bool AllowAchievements { get; set; } = true;
}

[Serializable]
public sealed class LegacyImportState
{
    public bool Reviewed { get; set; }
    public bool Imported { get; set; }
    public string SourceVersion { get; set; } = string.Empty;
    public DateTimeOffset? ImportedAt { get; set; }
    public Guid? ReceiptId { get; set; }
    public int ImportedItemCount { get; set; }
    public bool ReadyForActivation { get; set; }
    public bool Activated { get; set; }
    public string AppliedCharacterKey { get; set; } = string.Empty;
    public ProgressionDraftConfiguration? PreviousProgression { get; set; }
    public ProgressionQueueConfiguration? PreviousProgressionQueue { get; set; }
    public AtlasAutomationConfiguration? PreviousAtlas { get; set; }
}
