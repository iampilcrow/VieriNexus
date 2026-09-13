namespace VieriNexus.Application;

public enum NexusMaintenanceOperation
{
    Repair,
    ExtractMateria,
    RegisterTripleTriadCards,
    RegisterMinions,
    RegisterOrchestrionRolls,
    OpenCoffers,
    Sell,
    Desynthesize,
    GrandCompanyTurnIn,
    EntrustArmoire,
    EntrustGlamourChest,
}

public static class OperationsExecutionPolicy
{
    public static IReadOnlyList<NexusMaintenanceOperation> ConfiguredOperations(AutoDutyMaintenancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        List<NexusMaintenanceOperation> result = [];
        if (policy.AutoExtract)
            result.Add(NexusMaintenanceOperation.ExtractMateria);
        if (policy.AutoDesynth)
            result.Add(NexusMaintenanceOperation.Desynthesize);
        if (policy.EntrustArmoire)
            result.Add(NexusMaintenanceOperation.EntrustArmoire);
        if (policy.EntrustGlamourChest)
            result.Add(NexusMaintenanceOperation.EntrustGlamourChest);
        if (policy.RegisterTripleTriadCards)
            result.Add(NexusMaintenanceOperation.RegisterTripleTriadCards);
        if (policy.RegisterMinions)
            result.Add(NexusMaintenanceOperation.RegisterMinions);
        if (policy.RegisterOrchestrionRolls)
            result.Add(NexusMaintenanceOperation.RegisterOrchestrionRolls);
        if (policy.AutoOpenCoffers)
            result.Add(NexusMaintenanceOperation.OpenCoffers);
        if (policy.AutoGrandCompanyTurnIn)
            result.Add(NexusMaintenanceOperation.GrandCompanyTurnIn);
        if (policy.AutoRepair && policy.RepairWithCrafter)
            result.Add(NexusMaintenanceOperation.Repair);
        return result;
    }
}

public sealed record StrikingDummyDestination(
    string Expansion,
    string Zone,
    string Levels,
    float MapX,
    float MapY,
    bool RequiresInstance = false,
    string LocalArea = "")
{
    public string DisplayLocation => string.IsNullOrWhiteSpace(LocalArea) ? Zone : $"{Zone} — {LocalArea}";
}

public static class StrikingDummyCatalog
{
    public static IReadOnlyList<StrikingDummyDestination> Destinations { get; } =
    [
        new("A Realm Reborn", "Central Shroud", "1", 24, 19.5f, LocalArea: "The Bannock"),
        new("A Realm Reborn", "Middle La Noscea", "1", 26.4f, 17.2f, LocalArea: "Summerford Farms"),
        new("A Realm Reborn", "Western Thanalan", "1", 26.4f, 24.7f, LocalArea: "Scorpion Crossing"),
        new("A Realm Reborn", "Coerthas Central Highlands", "1 / 50", 13.2f, 16.9f, LocalArea: "Whitebrim Front"),
        new("A Realm Reborn", "Wolves' Den Pier", "PvP", 0, 0),
        new("Heavensward", "Coerthas Western Highlands", "50", 31.5f, 38.6f),
        new("Heavensward", "The Dravanian Forelands", "50", 32.7f, 24.8f),
        new("Heavensward", "The Dravanian Hinterlands", "50", 22.7f, 16.6f),
        new("Stormblood", "The Fringes", "60", 10, 11.8f),
        new("Stormblood", "Yanxia", "60", 31.2f, 16.7f),
        new("Stormblood", "The Lochs", "70", 10.5f, 20.6f),
        new("Shadowbringers", "Kholusia", "70", 35.7f, 27),
        new("Shadowbringers", "Amh Araeng", "70", 27.7f, 14.4f),
        new("Shadowbringers", "The Tempest", "80", 32, 19.3f),
        new("Endwalker", "Thavnair", "80", 23.2f, 34.1f),
        new("Endwalker", "Labyrinthos", "80", 31.4f, 13.6f),
        new("Endwalker", "Ultima Thule", "90", 30.6f, 26.8f),
        new("Dawntrail", "Urqopacha", "90", 29.9f, 12.9f, LocalArea: "Wachunpelo"),
        new("Dawntrail", "Kozama'uka", "90", 21.2f, 11.1f, LocalArea: "Ok'hanu"),
        new("Dawntrail", "Living Memory", "100", 21.4f, 38.5f, LocalArea: "South of Leynode Mnemo"),
        new("Shadowbringers", "The Bozjan Southern Front", "80", 14.1f, 30, true),
        new("Shadowbringers", "Zadnor", "80", 0, 0, true),
        new("Dawntrail", "The Occult Crescent: South Horn", "100 / Knowledge 1", 37.6f, 6.7f, true),
        new("Dawntrail", "The Occult Crescent: North Horn", "100 / Knowledge 1", 38.1f, 39.3f, true),
    ];

    public static (float X, float Z) MapToWorld(float mapX, float mapY, uint sizeFactor, int offsetX, int offsetY) =>
        ((mapX - 1 - 2048f / sizeFactor) / 0.02f - offsetX,
         (mapY - 1 - 2048f / sizeFactor) / 0.02f - offsetY);
}
