using System;

namespace DelvUI.Helpers
{
    /// <summary>
    /// The legacy native mouse proxy replaces FFXIV's window procedure. That is
    /// appropriate only for the standalone plugin which owns its whole lifetime.
    /// An embedded Nexus module must use Dalamud/ImGui input so an unload or
    /// partial reload can never leave the rest of the game UI without clicks.
    /// </summary>
    public static class EmbeddedInputSafetyPolicy
    {
        public static bool AllowsNativeMouseProxy(string? configurationDirectory)
        {
            if (string.IsNullOrWhiteSpace(configurationDirectory))
            {
                return true;
            }

            string normalized = configurationDirectory.Replace('\\', '/');
            return !normalized.Contains("/NexusData/EmbeddedModules/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
