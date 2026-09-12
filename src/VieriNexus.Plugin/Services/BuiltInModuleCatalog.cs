using VieriNexus.Application;
using VieriNexus.Domain;

namespace VieriNexus.Services;

internal static class BuiltInModuleCatalog
{
    internal static ModuleRegistry Create()
    {
        var registry = new ModuleRegistry();
        Register(registry, "progression", "Progression", "Goals, quests, hunting logs, achievements, and exploration.", "Automation", "vieri.capability.progression/v1");
        Register(registry, "duties", "Duties", "Duty selection, execution, loops, and completion policy.", "Automation", "vieri.capability.duty.run/v1");
        Register(registry, "combat", "Combat", "Rotation engine, controls, suggestions, and positional guidance.", "Automation", "vieri.capability.combat.control/v1");
        Register(registry, "gear", "Gear & Inventory", "Shopping, equipping, repair, extraction, and inventory maintenance.", "Automation", "vieri.capability.gear.readiness/v1");
        Register(registry, "market", "Market", "Retainer scans, pricing decisions, and verified adjustments.", "Economy", "vieri.capability.market.reprice/v1");
        Register(registry, "custom-ui", "Custom UI", "HUD, nameplates, layouts, and overlay presentation.", "Interface", "vieri.capability.ui.custom/v1");
        Register(registry, "communications", "Communications", "Discord status, notifications, and authorized remote commands.", "Services", "vieri.capability.notify.discord/v1");
        Register(registry, "command-center", "Plugins", "Plugin favorites, settings, commands, hotkeys, and quick actions.", "Services", "vieri.capability.command.invoke/v1");
        Register(registry, "navigation", "Routes & Navigation", "Named route recording, manual plotting, world previews, playback, and reusable automation assignments.", "Automation", "vieri.capability.navigation.route/v1");
        return registry;
    }

    private static void Register(ModuleRegistry registry, string id, string name, string description, string category, string capability) =>
        registry.Register(new BuiltInModule(new ModuleDescriptor(id, name, description, category, false, [new CapabilityId(capability)])));

    private sealed class BuiltInModule(ModuleDescriptor descriptor) : INexusModule
    {
        public ModuleDescriptor Descriptor { get; } = descriptor;
    }
}
