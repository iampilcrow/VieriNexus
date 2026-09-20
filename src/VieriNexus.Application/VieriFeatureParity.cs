namespace VieriNexus.Application;

public enum VieriFeatureOwnership
{
    Nexus,
    EmbeddedNexusRuntime,
    StockProvider,
}

public sealed record VieriFeatureContract(
    string SourceProduct,
    string Feature,
    VieriFeatureOwnership Ownership,
    string Destination);

/// <summary>
/// The predecessor products are the behavioral baseline for Nexus. This catalog
/// makes the ownership boundary explicit without treating any predecessor feature
/// as optional: custom behavior belongs to Nexus, while stock mechanics remain in
/// their updateable stock providers.
/// </summary>
public static class VieriFeatureParity
{
    public static IReadOnlyList<VieriFeatureContract> All { get; } =
    [
        new("VieriCodex", "Main Scenario Quests (MSQ)", VieriFeatureOwnership.Nexus, "Automation"),
        new("VieriCodex", "Class/Job/Role Quests", VieriFeatureOwnership.Nexus, "Automation"),
        new("VieriCodex", "Aether Current quests", VieriFeatureOwnership.Nexus, "Automation and Progress Atlas"),
        new("VieriCodex", "Field Aether Currents", VieriFeatureOwnership.Nexus, "Automation and Progress Atlas"),
        new("VieriCodex", "Aetherytes & Aethernet", VieriFeatureOwnership.Nexus, "Automation and Progress Atlas"),
        new("VieriCodex", "World Exploration", VieriFeatureOwnership.Nexus, "Automation and Progress Atlas"),
        new("VieriCodex", "Hunting & Grand Company Logs", VieriFeatureOwnership.Nexus, "Automation and Progress Atlas"),
        new("VieriCodex", "Side Quests", VieriFeatureOwnership.Nexus, "Automation"),
        new("VieriCodex", "Achievements", VieriFeatureOwnership.Nexus, "Automation and Progress Atlas"),
        new("VieriCodex", "Expansion-organized quest and duty Progress Atlas", VieriFeatureOwnership.Nexus, "Progress Atlas"),
        new("VieriCodex", "Duty exploration", VieriFeatureOwnership.Nexus, "Progress Atlas"),
        new("VieriCodex", "One Click Navigation", VieriFeatureOwnership.Nexus, "Custom Route Editor"),
        new("VieriCodex", "Multi-job automation queue", VieriFeatureOwnership.Nexus, "Automation"),
        new("VieriCodex", "Quest execution and native quest tools", VieriFeatureOwnership.StockProvider, "Questionable"),

        new("VieriAutoDuty", "Operations overlay", VieriFeatureOwnership.Nexus, "Nexus Operations"),
        new("VieriAutoDuty", "Gear, maintenance, inventory, and Last Run policy", VieriFeatureOwnership.Nexus, "Automation and Gear & Inventory"),
        new("VieriAutoDuty", "Duty execution and per-duty configuration", VieriFeatureOwnership.StockProvider, "AutoDuty"),

        new("VieriNavPlotter", "Custom route editor, bindings, recording, and playback", VieriFeatureOwnership.Nexus, "Custom Route Editor"),
        new("VieriNavPlotter", "Teleport and local mesh movement", VieriFeatureOwnership.StockProvider, "Lifestream and vnavmesh"),

        new("VieriRotationHelper", "On-screen ability suggestions and manual switch", VieriFeatureOwnership.EmbeddedNexusRuntime, "Combat/Rotation"),
        new("VieriRotationHelper", "Rotation engine", VieriFeatureOwnership.StockProvider, "Wrath Combo"),
        new("VieriAvarice", "Positional guidance, profiles, statistics, and feedback", VieriFeatureOwnership.EmbeddedNexusRuntime, "Combat/Rotation"),
        new("VieriDelvUI", "HUD, profiles, highlighting, markers, party roles, and ready checks", VieriFeatureOwnership.EmbeddedNexusRuntime, "Custom UI"),
        new("VieriAutoMarket", "Retainer matching, repricing, pacing, and reports", VieriFeatureOwnership.EmbeddedNexusRuntime, "Market Helper"),
        new("VieriDeck", "Plugin launcher, favorites, commands, and hotkey", VieriFeatureOwnership.Nexus, "Plugins"),
        new("VieriLink", "Discord status, notifications, recovery, and authorized commands", VieriFeatureOwnership.EmbeddedNexusRuntime, "Communications"),
    ];
}

public static class VieriAutoDutyOverlayContract
{
    public const string Barracks = "Barracks";
    public const string Inn = "Inn";
    public const string GrandCompanySupply = "GCSupply";
    public const string FlagMarker = "Flag Marker";
    public const string SummoningBell = "Summoning Bell";
    public const string Apartment = "Apartment";
    public const string PersonalHome = "Personal Home";
    public const string FreeCompanyEstate = "FC Estate";
    public const string TripleTriadTrader = "Triple Triad Trader";
    public const string StrikingDummies = "Striking Dummies";

    public const string ShopForUpgrades = "Shop for Upgrades";
    public const string Equip = "Equip";
    public const string Repair = "Repair";
    public const string ExtractMateria = "Extract Materia";
    public const string Desynth = "Desynth";

    public const string SellInventory = "Sell Inventory";
    public const string TurnIn = "TurnIn";
    public const string Coffers = "Coffers";
    public const string Armoire = "Armoire";

    public const string TripleTriad = "Triple Triad";
    public const string RegisterTripleTriadCards = "Register TT Cards";
    public const string SellTripleTriadCards = "Sell TT Cards";

    public static IReadOnlyList<string> GotoActions { get; } =
    [
        Barracks, Inn, GrandCompanySupply, FlagMarker, SummoningBell, Apartment,
        PersonalHome, FreeCompanyEstate, TripleTriadTrader, StrikingDummies,
    ];

    public static IReadOnlyList<string> GearActions { get; } =
        [ShopForUpgrades, Equip, Repair, ExtractMateria, Desynth];

    public static IReadOnlyList<string> InventoryActions { get; } =
        [SellInventory, TurnIn, Coffers, Armoire];

    public static IReadOnlyList<string> ExtraActions { get; } =
        [TripleTriad, RegisterTripleTriadCards, SellTripleTriadCards];
}
