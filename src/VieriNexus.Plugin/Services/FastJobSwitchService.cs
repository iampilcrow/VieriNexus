using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace VieriNexus.Services;

internal sealed record NexusClassJob(uint Id, string Name, string Abbreviation, int Level);

/// <summary>
/// Uses Fast Job Switcher's documented lower-case class/job commands, then lets the queue verify
/// the actual equipped job. Nexus never treats command dispatch as proof that the switch happened.
/// </summary>
internal sealed class FastJobSwitchService(
    IDalamudPluginInterface pluginInterface,
    ICommandManager commandManager,
    IDataManager dataManager)
{
    internal const string InternalName = "FastJobSwitcher";
    internal const string DisplayName = "Fast Job Switcher";

    internal bool IsInstalled => pluginInterface.InstalledPlugins.Any(plugin =>
        string.Equals(plugin.InternalName, InternalName, StringComparison.OrdinalIgnoreCase));

    internal bool IsLoaded => pluginInterface.InstalledPlugins.Any(plugin =>
        string.Equals(plugin.InternalName, InternalName, StringComparison.OrdinalIgnoreCase) && plugin.IsLoaded);

    internal unsafe uint CurrentClassJobId
    {
        get
        {
            PlayerState* state = PlayerState.Instance();
            return state is null ? 0u : checked((uint)state->CurrentClassJobId);
        }
    }

    internal unsafe int Level(uint classJobId)
    {
        PlayerState* state = PlayerState.Instance();
        ClassJob? row = dataManager.GetExcelSheet<ClassJob>().GetRowOrDefault(classJobId);
        return state is null || row is null || row.Value.ExpArrayIndex < 0
            ? 0
            : state->ClassJobLevels[row.Value.ExpArrayIndex];
    }

    internal IReadOnlyList<NexusClassJob> CombatJobs() => dataManager.GetExcelSheet<ClassJob>()
        .Where(row => row.RowId is >= 1 and <= 7 or >= 19 and <= 43)
        .OrderBy(row => row.UIPriority)
        .Select(row => new NexusClassJob(
            row.RowId,
            row.Name.ExtractText(),
            row.Abbreviation.ExtractText().ToUpperInvariant(),
            Level(row.RowId)))
        .Where(job => job.Abbreviation.Length > 0)
        .ToArray();

    internal string Label(uint classJobId)
    {
        ClassJob? row = dataManager.GetExcelSheet<ClassJob>().GetRowOrDefault(classJobId);
        if (row is null)
            return $"Unknown job {classJobId}";
        string name = row.Value.Name.ExtractText();
        string abbreviation = row.Value.Abbreviation.ExtractText().ToUpperInvariant();
        return name.Length > 0 && abbreviation.Length > 0 ? $"{name} ({abbreviation})" : abbreviation;
    }

    internal bool TryRequestSwitch(uint classJobId, out string message)
    {
        if (CurrentClassJobId == classJobId)
        {
            message = $"{Label(classJobId)} is already equipped.";
            return true;
        }
        if (!IsLoaded)
        {
            message = IsInstalled
                ? "Fast Job Switcher is installed but disabled. Enable it before resuming the queue."
                : "Fast Job Switcher is required for Nexus progression queues.";
            return false;
        }
        if (Level(classJobId) <= 0)
        {
            message = $"{Label(classJobId)} is not unlocked on this character.";
            return false;
        }

        ClassJob? row = dataManager.GetExcelSheet<ClassJob>().GetRowOrDefault(classJobId);
        string abbreviation = row?.Abbreviation.ExtractText().Trim().ToLowerInvariant() ?? string.Empty;
        if (abbreviation.Length == 0)
        {
            message = $"Nexus could not resolve the Fast Job Switcher command for job {classJobId}.";
            return false;
        }

        commandManager.ProcessCommand("/" + abbreviation);
        message = $"Asked Fast Job Switcher to equip {Label(classJobId)}; waiting for game confirmation.";
        return true;
    }
}
