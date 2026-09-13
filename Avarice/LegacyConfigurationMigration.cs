using System.IO;

namespace Avarice;

internal static class LegacyConfigurationMigration
{
    public static void CopyFromAvariceIfNeeded(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            var configDirectory = pluginInterface.GetPluginConfigDirectory();
            var pluginConfigsRoot = Directory.GetParent(configDirectory)?.FullName;
            if (pluginConfigsRoot == null)
                return;

            var legacyConfigFile = Path.Combine(pluginConfigsRoot, "Avarice.json");
            var destinationConfigFile = Path.Combine(pluginConfigsRoot, "VieriAvarice.json");
            if (File.Exists(legacyConfigFile) && !File.Exists(destinationConfigFile))
            {
                File.Copy(legacyConfigFile, destinationConfigFile, false);
            }

            var legacyDirectory = Path.Combine(pluginConfigsRoot, "Avarice");
            if (Directory.Exists(legacyDirectory) &&
                (!Directory.Exists(configDirectory) || !Directory.EnumerateFileSystemEntries(configDirectory).Any()))
            {
                CopyDirectory(legacyDirectory, configDirectory);
            }
        }
        catch (Exception exception)
        {
            PluginLog.Error($"Could not copy the existing Avarice configuration: {exception}");
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), false);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
        }
    }
}
