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

public sealed record GrandCompanyOverlayDestination(
    string CompanyName,
    uint HeadquartersTerritoryId,
    uint InnTerritoryId,
    int InnShortcutIndex,
    uint BarracksTerritoryId,
    uint BarracksDoorDataId,
    float BarracksX,
    float BarracksY,
    float BarracksZ,
    float SupplyX,
    float SupplyY,
    float SupplyZ);

/// <summary>
/// Exact Grand Company destination mapping used by VieriAutoDuty's overlay helpers.
/// Lifestream's nullable inn shortcut is intentionally not used: a null shortcut may
/// resolve a configured suite instead of the active character's Grand Company inn.
/// </summary>
public static class VieriAutoDutyGrandCompanyContract
{
    public static GrandCompanyOverlayDestination Resolve(byte grandCompany) => grandCompany switch
    {
        1 => new(
            "Maelstrom", 128, 177, 0, 536, 2007527,
            98.00867f, 41.275635f, 62.790894f,
            94.02183f, 40.27537f, 74.475525f),
        2 => new(
            "Twin Adder", 132, 179, 2, 534, 2006962,
            -80.216736f, 0.47296143f, -7.0039062f,
            -68.678566f, -0.5015295f, -8.470145f),
        _ => new(
            "Immortal Flames", 130, 178, 1, 535, 2007529,
            -153.30743f, 5.2338257f, -98.039246f,
            -142.82619f, 4.0999994f, -106.31349f),
    };
}

public sealed record VieriAutoDutyOverlayButtonState(
    bool Goto,
    bool Equip,
    bool Repair,
    bool Extract,
    bool Desynth,
    bool Sell,
    bool TurnIn,
    bool Coffers,
    bool TripleTriad);

/// <summary>
/// Preserves VieriAutoDuty's exact Override Overlay Buttons behavior. When override is disabled,
/// automatic maintenance choices govern most buttons; Goto and Sell retain their predecessor
/// behavior, and the individual button switches still remain authoritative where applicable.
/// </summary>
public static class VieriAutoDutyOverlayButtonPolicy
{
    public static VieriAutoDutyOverlayButtonState Evaluate(
        AutoDutyOverlayPreferences? overlay,
        AutoDutyMaintenancePolicy? maintenance,
        bool hasWorkingProfile)
    {
        bool overrideButtons = overlay?.OverrideButtons ?? true;
        bool gotoEnabled = overlay is null || !overrideButtons || overlay.ShowGoto;
        bool equipEnabled = overlay is null ||
            overlay.ShowGear && (overrideButtons || maintenance?.AutoEquipRecommendedGear == true);
        bool repairEnabled = hasWorkingProfile && (overlay is null ||
            overlay.ShowRepair && (overrideButtons || maintenance?.AutoRepair == true));
        bool extractEnabled = hasWorkingProfile && (overlay is null ||
            overlay.ShowExtract && (overrideButtons || maintenance?.AutoExtract == true));
        bool desynthEnabled = hasWorkingProfile && (overlay is null ||
            overlay.ShowDesynth && (overrideButtons || maintenance?.AutoDesynth == true));
        bool sellEnabled = hasWorkingProfile &&
            (overlay is null || !overrideButtons || overlay.ShowSell);
        bool turnInEnabled = hasWorkingProfile && (overlay is null ||
            overlay.ShowTurnIn && (overrideButtons || maintenance?.AutoGrandCompanyTurnIn == true));
        bool coffersEnabled = hasWorkingProfile && (overlay is null ||
            overlay.ShowCoffers && (overrideButtons || maintenance?.AutoOpenCoffers == true));
        bool tripleTriadConfigured = maintenance is
            { RegisterTripleTriadCards: true } or { SellTripleTriadCards: true };
        bool tripleTriadEnabled = hasWorkingProfile && (overlay is null ||
            tripleTriadConfigured || overrideButtons && overlay.ShowTripleTriad);
        return new(gotoEnabled, equipEnabled, repairEnabled, extractEnabled, desynthEnabled,
            sellEnabled, turnInEnabled, coffersEnabled, tripleTriadEnabled);
    }
}
