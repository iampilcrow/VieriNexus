namespace VieriNexus.Domain;

public sealed record ModuleDescriptor(
    string Id,
    string DisplayName,
    string Description,
    string Category,
    bool IsFoundation,
    IReadOnlyList<CapabilityId> Capabilities);

public interface INexusModule
{
    ModuleDescriptor Descriptor { get; }
}
