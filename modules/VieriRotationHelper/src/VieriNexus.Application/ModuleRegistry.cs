using VieriNexus.Domain;

namespace VieriNexus.Application;

public sealed class ModuleRegistry
{
    private readonly Dictionary<string, INexusModule> modules = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<INexusModule> Modules => modules.Values;

    public void Register(INexusModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (!modules.TryAdd(module.Descriptor.Id, module))
            throw new InvalidOperationException($"A Nexus module with ID '{module.Descriptor.Id}' is already registered.");
    }

    public bool TryGet(string id, out INexusModule? module) => modules.TryGetValue(id, out module);
}
