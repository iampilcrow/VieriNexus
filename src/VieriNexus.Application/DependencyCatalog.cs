namespace VieriNexus.Application;

public sealed record DependencyDescriptor(
    string Id,
    string DisplayName,
    string Capability,
    string Description,
    bool Required,
    string[] InternalNames,
    string? RepositoryUrl = null,
    string? InstallerSearch = null);

public static class NexusDependencyCatalog
{
    public static IReadOnlyList<DependencyDescriptor> All { get; } =
    [
        new("bossmod", "Boss Mod", "Duties and Combat", "Handles encounter mechanics, supported solo duties, and duty movement intelligence.", true, ["BossMod"], "https://puni.sh/api/repository/veyn"),
        new("vnavmesh", "vnavmesh", "Navigation", "Builds navigation meshes and moves safely within the loaded zone.", true, ["vnavmesh"], "https://puni.sh/api/repository/veyn"),
        new("lifestream", "Lifestream", "Travel", "Handles Aetheryte, Aethernet, world, and local travel services.", true, ["Lifestream"], "https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json"),
        new("textadvance", "TextAdvance", "Questing", "Accepts and turns in quests and advances supported dialogue and cutscenes.", true, ["TextAdvance"], "https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json"),
        new("marketbuddy", "Marketbuddy", "Market", "Applies configured retainer listing price changes for Nexus market automation.", true, ["Marketbuddy"], "https://love.puni.sh/ment.json"),
        new("allagan-market", "Allagan Market", "Market", "Provides market ownership, pricing, scan state, and undercut intelligence.", true, ["AllaganMarket"], null, "Allagan Market"),

        new("auto-retainer", "AutoRetainer", "Retainers and Inventory", "Adds retainer cycles, Grand Company turn-ins, protected selling and discarding, and Multi Mode support.", false, ["AutoRetainer"], "https://love.puni.sh/ment.json"),
        new("glamour-log", "Glamour Log", "Collections", "Adds Glamour Dresser and Armoire ownership checks and automated entrusting between runs.", false, ["GlamourLog"], "https://puni.sh/api/repository/croizat", "Glamour Log"),
        new("anti-afk", "Anti-AFK", "Long Automation", "Prevents long unattended progression or duty sessions from being marked AFK.", false, ["AntiAfkKick-Dalamud"], "https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json", "AntiAfkKick"),
        new("pandora", "Pandora's Box", "Duties and Questing", "Adds automatic Active Time Maneuvers, chest handling, tank stance, and supported instance interactions.", false, ["PandorasBox"], "https://love.puni.sh/ment.json", "Pandora's Box"),
        new("gearsetter", "Gearsetter", "Equipment", "Finds and equips recommended upgrades across the Armoury Chest and inventory.", false, ["Gearsetter"], "https://puni.sh/api/repository/vera"),
        new("stylist", "Stylist", "Equipment", "Keeps job gearsets organized and equips recommended items across inventory sources.", false, ["Stylist"], "https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json"),
        new("fast-job-switcher", "Fast Job Switcher", "Progression Queue", "Enables unattended switching between configured classes and jobs.", false, ["FastJobSwitcher"], null, "Fast Job Switcher"),
        new("cbt", "CBT", "Questing", "Its Sniper No Sniping tweak completes supported aiming sequences that otherwise need manual input.", false, ["Automaton"], "https://puni.sh/api/repository/croizat", "CBT"),
        new("artisan", "Artisan", "Crafting", "Completes crafting steps required by supported quests and future crafting goals.", false, ["Artisan"], "https://love.puni.sh/ment.json"),
        new("autohook", "AutoHook", "Fishing", "Completes fishing steps required by supported quests and future fishing goals.", false, ["AutoHook"], "https://love.puni.sh/ment.json"),
        new("mogmail", "Mogmail", "Progression", "Claims mailed quest items so deliveries do not interrupt progression routes.", false, ["Mogmail"], "https://puni.sh/api/plugins/nexai"),
        new("notification-master", "NotificationMaster", "Notifications", "Sends an out-of-game alert when progression reaches a step requiring manual attention.", false, ["NotificationMaster"]),
        new("select-string", "SelectString", "Questing", "Provides keyboard selection for numbered dialogue and menu choices used by supported routes.", false, ["SelectString"], "https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json"),
        new("quest-map", "QuestMap", "Progression", "Adds quest and reward discovery when reviewing progression options.", false, ["QuestMap"]),
        new("yes-already", "YesAlready", "Interactions", "Provides compatible confirmation automation; Nexus pauses it during flows that require exclusive control.", false, ["YesAlready"], "https://love.puni.sh/ment.json"),
        new("skippy", "Skippy", "Duty Playback", "Adds optional supported MSQ duty playback skipping detected by the duty module.", false, ["Skippy"]),
    ];
}
