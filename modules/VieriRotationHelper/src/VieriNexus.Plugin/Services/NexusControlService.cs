using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using VieriNexus.Application;
using VieriNexus.Contracts;
using VieriNexus.Domain;

namespace VieriNexus.Services;

/// <summary>
/// One character-scoped control and telemetry boundary for the custom Nexus overlay, chat commands,
/// and trusted companion IPC. It never exposes provider-private pause/leave/loop operations.
/// </summary>
internal sealed unsafe class NexusControlService
{
    private static readonly InventoryType[] Bags =
    [
        InventoryType.Inventory1, InventoryType.Inventory2,
        InventoryType.Inventory3, InventoryType.Inventory4,
    ];

    private readonly Configuration configuration;
    private readonly WorldStateStore world;
    private readonly IDataManager dataManager;
    private readonly ICondition condition;
    private readonly ProgressionRuntimeService progression;
    private readonly ProgressAtlasActionService atlas;
    private readonly NavigationLibraryService routes;
    private readonly NavigationRouteRuntimeService navigation;
    private readonly GearShoppingRuntimeService gear;
    private readonly NexusMaintenanceRuntimeService maintenance;
    private readonly StrikingDummyTravelService strikingDummies;
    private readonly Action<string> openPage;
    private readonly System.Action save;
    private readonly Dictionary<Guid, CachedCommand> completed = [];
    private readonly Queue<Guid> completionOrder = [];

    internal NexusControlService(
        Configuration configuration,
        WorldStateStore world,
        IDataManager dataManager,
        ICondition condition,
        ProgressionRuntimeService progression,
        ProgressAtlasActionService atlas,
        NavigationLibraryService routes,
        NavigationRouteRuntimeService navigation,
        GearShoppingRuntimeService gear,
        NexusMaintenanceRuntimeService maintenance,
        StrikingDummyTravelService strikingDummies,
        Action<string> openPage,
        System.Action save)
    {
        this.configuration = configuration;
        this.world = world;
        this.dataManager = dataManager;
        this.condition = condition;
        this.progression = progression;
        this.atlas = atlas;
        this.routes = routes;
        this.navigation = navigation;
        this.gear = gear;
        this.maintenance = maintenance;
        this.strikingDummies = strikingDummies;
        this.openPage = openPage;
        this.save = save;
    }

    internal NexusOperationsStatusDto Status()
    {
        WorldSnapshot snapshot = world.Current;
        CharacterSnapshot? character = snapshot.Character.Value;
        ProgressionGoalState? goal = progression.State;
        NexusTask? task = goal?.ActiveTask;
        ProgressionCharacterMetrics metrics = progression.CurrentMetrics;
        (int used, int total) = InventoryUsage();
        string location = dataManager.GetExcelSheet<TerritoryType>()
            .GetRowOrDefault(snapshot.Session.TerritoryId)?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;

        bool navigationActive = navigation.Status.IsActive;
        bool gearActive = gear.Status.IsActive;
        bool maintenanceActive = maintenance.Status.IsActive;
        bool atlasActive = atlas.Status.IsActive;
        bool dummyActive = strikingDummies.Status.IsActive;
        bool progressionActive = goal?.Goal.Status == GoalStatus.Active;
        string? module = gearActive ? "Gear & Inventory" :
            maintenanceActive ? "Maintenance" :
            atlasActive ? "Progress Atlas" :
            dummyActive || navigationActive ? "Routes & Navigation" :
            progressionActive ? "Progression" : null;
        string? activity = gearActive ? "Gear transaction" :
            maintenanceActive ? maintenance.Status.Operation?.ToString() :
            atlasActive ? atlas.Status.Title :
            dummyActive ? "Striking-dummy travel" :
            navigationActive ? navigation.Status.RouteName :
            task?.Title;
        string? detail = gearActive ? gear.Status.Message :
            maintenanceActive ? maintenance.Status.Message :
            atlasActive ? atlas.Status.Message :
            dummyActive ? strikingDummies.Status.Message :
            navigationActive ? navigation.Status.Message :
            task?.StatusDetail ?? goal?.Goal.StatusDetail;
        NexusTask? last = goal?.Tasks.LastOrDefault(candidate => candidate.Status == NexusTaskStatus.Succeeded);
        return new NexusOperationsStatusDto(
            NexusIpc.CurrentVersion,
            character is { Key.IsKnown: true },
            character?.Name ?? string.Empty,
            character?.Key.ContentId ?? 0,
            character is null ? string.Empty : ClassJobDisplay.Label(character),
            character?.Level ?? 0,
            metrics.ItemLevel,
            metrics.Gil,
            snapshot.Session.TerritoryId,
            location,
            character?.IsInCombat == true,
            IsInDuty(),
            IsInDutyQueue(),
            used,
            total,
            LowestEquippedDurability(),
            module is not null ? "Running" : goal?.Goal.Status.ToString() ?? "Idle",
            module,
            activity,
            detail,
            task?.Provider?.Value,
            goal?.StopAfterCurrentDuty == true,
            Completed(goal, "vieri.quest.run-one/v1"),
            Completed(goal, "vieri.hunting-log.complete-target/v1"),
            Completed(goal, "vieri.duties.run-one/v1"),
            last?.Title,
            DateTimeOffset.UtcNow);
    }

    internal NexusCommandResultDto ExecuteLocal(string command, string payloadJson = "{}") =>
        Execute(new NexusCommandDto(
            NexusIpc.CurrentVersion,
            Guid.NewGuid(),
            command,
            payloadJson,
            world.Current.Character.Value?.Key.ContentId));

    internal NexusCommandResultDto Execute(NexusCommandDto request)
    {
        string fingerprint = $"{request.ContractVersion}\n{request.Command}\n{request.PayloadJson}\n{request.CharacterContentId}";
        if (completed.TryGetValue(request.RequestId, out CachedCommand? cached) && cached is not null)
            return string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal)
                ? cached.Result
                : new NexusCommandResultDto(request.RequestId, false, "request-id-collision",
                    "That request ID was already used for a different Nexus command.");

        NexusCommandValidation validation = NexusCommandPolicy.Validate(
            new NexusCommandRequest(request.ContractVersion, request.RequestId, request.Command,
                request.PayloadJson, request.CharacterContentId),
            world.Current.Character.Value?.Key.ContentId ?? 0);
        NexusCommandResultDto result = validation.IsValid
            ? ExecuteValidated(request.RequestId, validation.Command, request.PayloadJson ?? string.Empty)
            : new NexusCommandResultDto(request.RequestId, false, validation.Code, validation.Message);
        Cache(request.RequestId, fingerprint, result);
        return result;
    }

    internal NexusCommandResultDto StopAll(Guid? requestId = null)
    {
        bool stopped = false;
        if (atlas.Status.IsActive)
            stopped |= atlas.Stop(out _);
        if (gear.Status.IsActive)
            stopped |= gear.Stop().Success;
        if (maintenance.Status.IsActive)
            stopped |= maintenance.Stop(out _);
        if (strikingDummies.Status.IsActive)
            stopped |= strikingDummies.Stop(out _);
        if (progression.State?.Goal.Status is not null and not GoalStatus.Cancelled and not GoalStatus.Satisfied)
            stopped |= progression.StopNow().Success;
        if (navigation.Status.IsActive)
        {
            navigation.Stop();
            stopped = true;
        }
        return new NexusCommandResultDto(requestId ?? Guid.NewGuid(), stopped,
            stopped ? "stop-requested" : "nothing-active",
            stopped ? "Stop requested for every active Nexus operation." : "No Nexus operation is active.");
    }

    private NexusCommandResultDto ExecuteValidated(Guid id, string command, string payload)
    {
        bool success;
        string message;
        switch (command)
        {
            case "status":
                NexusOperationsStatusDto status = Status();
                message = status.ActiveModule is null
                    ? $"Nexus is {status.State.ToLowerInvariant()} for {status.Character}."
                    : $"{status.ActiveModule}: {status.Detail ?? status.Activity ?? status.State}";
                return Result(id, true, "status", message);
            case "stop":
                return StopAll(id);
            case "progression.start":
                CharacterSnapshot? character = world.Current.Character.Value;
                CharacterConfiguration? characterConfiguration = character is null
                    ? null
                    : configuration.ForCharacter(character.Key.ToString());
                if (characterConfiguration is null)
                    return Result(id, false, "character-unavailable", "A logged-in character is required to start Progression.");
                ProgressionActionResult start = progression.StartConfigured(
                    character, characterConfiguration.Progression, characterConfiguration.AllowAutomation);
                if (start.Success)
                    save();
                return Result(id, start.Success, start.Success ? "progression-started" : "progression-rejected", start.Message);
            case "progression.resume":
                return FromProgression(id, "progression-resume", progression.Resume());
            case "progression.last":
                return FromProgression(id, "stop-after", progression.StopAfterCurrentDuty());
            case "progression.stop":
                return FromProgression(id, "progression-stop", progression.StopNow());
            case "maintenance.run":
                success = maintenance.StartConfigured(out message);
                return Result(id, success, "maintenance-run", message);
            case "maintenance.repair":
                success = maintenance.Start(NexusMaintenanceOperation.Repair, out message);
                return Result(id, success, "maintenance-repair", message);
            case "maintenance.extract":
                success = maintenance.Start(NexusMaintenanceOperation.ExtractMateria, out message);
                return Result(id, success, "maintenance-extract", message);
            case "maintenance.register":
                success = maintenance.StartRegistrations(out message);
                return Result(id, success, "maintenance-register", message);
            case "maintenance.coffers":
                success = maintenance.Start(NexusMaintenanceOperation.OpenCoffers, out message);
                return Result(id, success, "maintenance-coffers", message);
            case "maintenance.desynth":
                success = maintenance.Start(NexusMaintenanceOperation.Desynthesize, out message);
                return Result(id, success, "maintenance-desynth", message);
            case "maintenance.gc":
                success = maintenance.Start(NexusMaintenanceOperation.GrandCompanyTurnIn, out message);
                return Result(id, success, "maintenance-gc", message);
            case "maintenance.storage":
                success = maintenance.StartStorage(out message);
                return Result(id, success, "maintenance-storage", message);
            case "maintenance.sell-review":
                openPage("Gear & Inventory");
                return Result(id, false, "review-required",
                    "Protected selling requires an exact in-game item review. The Gear & Inventory page is open.");
            case "route.play":
            case "route.preview":
                return RunRoute(id, command == "route.preview", PayloadValue(payload, "name"));
            case "ui.open":
                string page = PayloadValue(payload, "page");
                if (!AllowedPage(page))
                    return Result(id, false, "invalid-page", "That Nexus page is not available through the command gateway.");
                openPage(page);
                return Result(id, true, "page-opened", $"Opened {page}.");
            default:
                return Result(id, false, "unknown-command", "The Nexus command is not implemented.");
        }
    }

    private NexusCommandResultDto RunRoute(Guid id, bool previewOnly, string name)
    {
        NavigationLibrarySnapshot? library = routes.Current;
        NavigationRouteSnapshot? route = library is null ? null : NavigationLibraryQuery.Find(library, name);
        if (route is null)
            return Result(id, false, "route-not-found", $"Route '{name}' was not found.");
        if (previewOnly)
        {
            NavigationRoutePlan preview = navigation.TogglePreview(route, library!.ShowPointNumbers);
            return Result(id, preview.IsValid, preview.Code, preview.Message);
        }
        NavigationRouteExecutionStatus started = navigation.Start(route, NavigationRoutePlanKind.Playback);
        return Result(id, started.IsActive, started.Code, started.Message);
    }

    private void Cache(Guid id, string fingerprint, NexusCommandResultDto result)
    {
        completed[id] = new CachedCommand(fingerprint, result);
        completionOrder.Enqueue(id);
        while (completionOrder.Count > 128)
            completed.Remove(completionOrder.Dequeue());
    }

    private bool IsInDuty() => condition[ConditionFlag.BoundByDuty] ||
        condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95];

    private static bool IsInDutyQueue()
    {
        Conditions* conditions = Conditions.Instance();
        return conditions is not null && conditions->InDutyQueue;
    }

    private static int Completed(ProgressionGoalState? state, string kind) =>
        state?.Tasks.Count(task => task.Status == NexusTaskStatus.Succeeded && task.Kind.Value == kind) ?? 0;

    private static NexusCommandResultDto FromProgression(Guid id, string code, ProgressionActionResult result) =>
        Result(id, result.Success, code, result.Message);

    private static NexusCommandResultDto Result(Guid id, bool accepted, string code, string message) =>
        new(id, accepted, code, message);

    private static string PayloadValue(string payload, string property)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return string.Empty;
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(property, out JsonElement value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static bool AllowedPage(string page) => page is
        "Home" or "Overview" or "Progression" or "Progress Atlas" or "Routes & Navigation" or
        "Gear & Inventory" or "Dependencies" or "Migration" or "Settings";

    private static (int Used, int Total) InventoryUsage()
    {
        InventoryManager* manager = InventoryManager.Instance();
        if (manager is null)
            return default;
        int used = 0;
        int total = 0;
        foreach (InventoryType type in Bags)
        {
            InventoryContainer* bag = manager->GetInventoryContainer(type);
            if (bag is null || !bag->IsLoaded)
                continue;
            total += bag->Size;
            for (int index = 0; index < bag->Size; index++)
                if (bag->Items[index].ItemId != 0)
                    used++;
        }
        return (used, total);
    }

    private static float LowestEquippedDurability()
    {
        InventoryManager* manager = InventoryManager.Instance();
        InventoryContainer* equipped = manager is null
            ? null
            : manager->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped is null || !equipped->IsLoaded)
            return 100f;
        float lowest = 100f;
        bool found = false;
        for (int index = 0; index < Math.Min(13, equipped->Size); index++)
        {
            InventoryItem item = equipped->Items[index];
            if (item.ItemId == 0)
                continue;
            found = true;
            lowest = Math.Min(lowest, item.Condition / 300f);
        }
        return found ? Math.Clamp(lowest, 0f, 100f) : 100f;
    }

    private sealed record CachedCommand(string Fingerprint, NexusCommandResultDto Result);
}
