using System.Reflection;
using System.Text.RegularExpressions;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace VieriNexus.Services;

internal sealed class CommandCenterEntry
{
    internal required string Id { get; init; }
    internal required string Name { get; init; }
    internal string Description { get; init; } = string.Empty;
    internal string Version { get; init; } = string.Empty;
    internal string AssemblyName { get; init; } = string.Empty;
    internal Assembly? OwnerAssembly { get; init; }
    internal object? CommandTarget { get; init; }
    internal bool IsLoaded { get; init; }
    internal bool IsSystem { get; init; }
    internal IExposedPlugin? Plugin { get; init; }
    internal List<CommandCenterCommand> Commands { get; } = [];
}

internal sealed record CommandCenterCommand(string Command, string Help, bool IsDerived = false, bool IsCustom = false);

internal sealed partial class CommandCenterCatalogService
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly CommandCenterPluginUiBridge pluginUi;
    private long nextRefresh;
    private IReadOnlyList<CommandCenterEntry> entries = [];

    internal CommandCenterCatalogService(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;
        pluginUi = new(log);
    }

    internal IReadOnlyList<CommandCenterEntry> Entries
    {
        get
        {
            Refresh();
            return entries;
        }
    }

    internal void Refresh(bool force = false)
    {
        long now = Environment.TickCount64;
        if (!force && now < nextRefresh)
            return;
        nextRefresh = now + 2_000;
        try
        {
            RegisteredCommand[] registered = commandManager.Commands
                .Select(pair => Describe(pair.Key, pair.Value))
                .OrderBy(command => command.Command, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            HashSet<string> claimed = new(StringComparer.OrdinalIgnoreCase);
            List<CommandCenterEntry> catalog = [];
            // Multiple repository entries can share an InternalName while one is disabled and
            // another is loaded (for example VieriAutoDuty and stock AutoDuty). The launcher is
            // keyed by InternalName, so present one deterministic entry and prefer the runtime
            // Dalamud is actually using.
            IExposedPlugin[] installed = pluginInterface.InstalledPlugins
                .GroupBy(plugin => plugin.InternalName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(plugin => plugin.IsLoaded)
                    .ThenByDescending(plugin => plugin.HasMainUi || plugin.HasConfigUi)
                    .ThenByDescending(plugin => plugin.Version)
                    .First())
                .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (IExposedPlugin exposed in installed)
            {
                string[] aliases = [Normalize(exposed.InternalName), Normalize(exposed.Name)];
                RegisteredCommand[] matching = registered.Where(command =>
                    command.PluginId.Equals(exposed.InternalName, StringComparison.OrdinalIgnoreCase) ||
                    command.Assembly.Length > 0 && aliases.Any(alias => AssemblyMatches(alias, Normalize(command.Assembly)))).ToArray();
                CommandCenterEntry entry = new()
                {
                    Id = exposed.InternalName,
                    Name = exposed.Name,
                    Description = exposed.Manifest.Description ?? exposed.Manifest.Punchline ?? string.Empty,
                    Version = exposed.Version.ToString(),
                    AssemblyName = matching.FirstOrDefault()?.Assembly ?? exposed.InternalName,
                    OwnerAssembly = matching.FirstOrDefault()?.OwnerAssembly,
                    CommandTarget = matching.Select(command => command.CommandTarget).FirstOrDefault(target => target is not null),
                    IsLoaded = exposed.IsLoaded,
                    Plugin = exposed,
                };
                foreach (RegisteredCommand command in matching)
                {
                    entry.Commands.Add(new(command.Command, command.Help));
                    claimed.Add(command.Command);
                }
                Enrich(entry);
                catalog.Add(entry);
            }
            CommandCenterEntry dalamud = new()
            {
                Id = "__dalamud",
                Name = "Dalamud",
                Description = "Dalamud and XIVLauncher plugin and application controls.",
                IsLoaded = true,
                IsSystem = true,
            };
            foreach (RegisteredCommand command in registered.Where(command => !claimed.Contains(command.Command) &&
                         (command.Command.StartsWith("/xl", StringComparison.OrdinalIgnoreCase) || Normalize(command.Assembly) == "dalamud")))
            {
                dalamud.Commands.Add(new(command.Command, command.Help));
                claimed.Add(command.Command);
            }
            catalog.Insert(0, dalamud);
            foreach (IGrouping<string, RegisteredCommand> group in registered.Where(command => !claimed.Contains(command.Command))
                         .GroupBy(command => string.IsNullOrWhiteSpace(command.Assembly) ? "Other commands" : command.Assembly)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                CommandCenterEntry entry = new()
                {
                    Id = "__commands_" + Normalize(group.Key),
                    Name = group.Key.EndsWith("Plugin", StringComparison.OrdinalIgnoreCase) ? group.Key[..^6] : group.Key,
                    Description = "Registered commands not matched to an installed-plugin manifest.",
                    IsLoaded = true,
                    IsSystem = true,
                };
                foreach (RegisteredCommand command in group)
                    entry.Commands.Add(new(command.Command, command.Help));
                Enrich(entry);
                catalog.Add(entry);
            }
            entries = catalog;
        }
        catch (Exception exception)
        {
            log.Error(exception, "Nexus could not refresh the Command Center catalog.");
        }
    }

    internal bool Run(string command)
    {
        command = NormalizeCommand(command);
        return command.Length > 1 && commandManager.ProcessCommand(command);
    }

    internal bool Open(CommandCenterEntry entry, bool settings, out bool closed)
    {
        closed = false;
        try
        {
            if (entry.Id == "__dalamud")
                return Run(settings ? "/xlsettings" : "/xlplugins");
            if (!settings && pluginUi.TryClose(entry))
            {
                closed = true;
                return true;
            }
            if (entry.Plugin is not { IsLoaded: true } plugin)
                return false;
            if (settings && plugin.HasConfigUi)
                plugin.OpenConfigUi();
            else if (plugin.HasMainUi)
                plugin.OpenMainUi();
            else if (plugin.HasConfigUi)
                plugin.OpenConfigUi();
            else
                return false;
            return true;
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Nexus could not open {PluginName}.", entry.Name);
            return false;
        }
    }

    private RegisteredCommand Describe(string command, IReadOnlyCommandInfo info)
    {
        try
        {
            Assembly? assembly = info.Handler.Method.DeclaringType?.Assembly;
            return new(command, info.HelpMessage ?? string.Empty, assembly?.GetName().Name ?? string.Empty,
                assembly is null ? string.Empty : pluginInterface.GetPlugin(assembly)?.InternalName ?? string.Empty,
                assembly,
                info.Handler.Target);
        }
        catch
        {
            return new(command, info.HelpMessage ?? string.Empty, string.Empty, string.Empty, null, null);
        }
    }

    private static void Enrich(CommandCenterEntry entry)
    {
        foreach (CommandCenterCommand source in entry.Commands.ToArray())
        {
            AddCommandsFromHelp(entry, source.Help);
            Match match = InlineSubcommandsRegex().Match(source.Help);
            if (!match.Success)
                continue;
            foreach (string subcommand in match.Groups["commands"].Value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Select(value => value.Trim().TrimEnd('.')).Where(value => value.Length > 0 && !value.Contains('<') && !value.Contains('[')))
                Add(entry, $"{source.Command} {subcommand}", $"Run the {entry.Name} {subcommand} action.");
        }
        if (entry.Id.Contains("lifestream", StringComparison.OrdinalIgnoreCase) || entry.Name.Contains("lifestream", StringComparison.OrdinalIgnoreCase))
        {
            Add(entry, "/li", "Travel back to your home world.");
            Add(entry, "/li mb", "Travel to a market board.");
            Add(entry, "/li auto", "Travel to your preferred estate.");
            Add(entry, "/li shared", "Travel to your preferred shared estate.");
            Add(entry, "/li home", "Travel to your private estate.");
            Add(entry, "/li house", "Travel to your private estate.");
            Add(entry, "/li private", "Travel to your private estate.");
            Add(entry, "/li fc", "Travel to your Free Company estate.");
            Add(entry, "/li free", "Travel to your Free Company estate.");
            Add(entry, "/li company", "Travel to your Free Company estate.");
            Add(entry, "/li free company", "Travel to your Free Company estate.");
            Add(entry, "/li apt", "Travel to your apartment.");
            Add(entry, "/li apartment", "Travel to your apartment.");
            Add(entry, "/li ws", "Travel to your Free Company workshop.");
            Add(entry, "/li workshop", "Travel to your Free Company workshop.");
            Add(entry, "/li gc", "Travel to your Grand Company.");
            Add(entry, "/li hc", "Return home first, then travel to your Grand Company.");
            Add(entry, "/li hcc", "Return home first, then travel to the Grand Company city's FC chest.");
            Add(entry, "/li gcc", "Travel to the FC chest in your Grand Company city.");
            Add(entry, "/li cosmic", "Travel to the Cosmic Exploration area.");
            Add(entry, "/li moon", "Travel to the Cosmic Exploration area.");
            Add(entry, "/li ardorum", "Travel to the Cosmic Exploration area.");
            Add(entry, "/li island", "Travel to Island Sanctuary.");
            Add(entry, "/li firmament", "Travel to the Firmament.");
            Add(entry, "/li w", "Open World Visit selection.");
            Add(entry, "/li world", "Open World Visit selection.");
            Add(entry, "/li open", "Open World Visit selection.");
            Add(entry, "/li select", "Open World Visit selection.");
            Add(entry, "/lifestream", "Open Lifestream configuration.");
        }
    }

    private static void AddCommandsFromHelp(CommandCenterEntry entry, string help)
    {
        if (string.IsNullOrWhiteSpace(help) || !help.Contains('\n'))
            return;
        foreach (string raw in help.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = HelpLineRegex().Match(raw.Trim());
            if (!match.Success)
                continue;
            string left = match.Groups["left"].Value.Trim();
            string description = match.Groups["description"].Value.Trim();
            int orIndex = left.LastIndexOf(" or ", StringComparison.OrdinalIgnoreCase);
            if (orIndex >= 0)
                left = left[(orIndex + 4)..].Trim();
            if (!left.StartsWith('/') || left.Contains('<') || left.Contains('['))
                continue;
            int firstSpace = left.IndexOf(' ');
            if (firstSpace < 0)
            {
                Add(entry, left, description);
                continue;
            }
            string root = left[..firstSpace];
            foreach (string subcommand in left[(firstSpace + 1)..].Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Add(entry, $"{root} {subcommand}", description);
        }
    }

    private static void Add(CommandCenterEntry entry, string command, string help)
    {
        if (!entry.Commands.Any(existing => existing.Command.Equals(command, StringComparison.OrdinalIgnoreCase)))
            entry.Commands.Add(new(command, help, true));
    }

    private static bool AssemblyMatches(string plugin, string assembly) => plugin.Length > 0 && assembly.Length > 0 &&
        (plugin == assembly || plugin.StartsWith(assembly, StringComparison.Ordinal) || assembly.StartsWith(plugin, StringComparison.Ordinal));
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string NormalizeCommand(string command)
    {
        command = command.Trim();
        return command.Length > 0 && command[0] != '/' ? "/" + command : command;
    }

    private sealed record RegisteredCommand(
        string Command,
        string Help,
        string Assembly,
        string PluginId,
        Assembly? OwnerAssembly,
        object? CommandTarget);

    [GeneratedRegex(@"\bSubcommands?\s*:\s*(?<commands>[^\r\n]+)", RegexOptions.IgnoreCase)]
    private static partial Regex InlineSubcommandsRegex();

    [GeneratedRegex(@"^(?<left>/.*?)\s*(?:->|→)\s*(?<description>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex HelpLineRegex();
}
