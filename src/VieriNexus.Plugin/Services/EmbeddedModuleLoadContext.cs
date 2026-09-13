using System.Reflection;
using System.Runtime.Loader;

namespace VieriNexus.Services;

internal sealed class EmbeddedModuleLoadContext(string mainAssemblyPath)
    : AssemblyLoadContext($"VieriNexus:{Path.GetFileNameWithoutExtension(mainAssemblyPath)}", isCollectible: true)
{
    private static readonly string[] SharedPrefixes =
    [
        "Dalamud", "FFXIVClientStructs", "Lumina", "Newtonsoft.Json", "Serilog",
        "InteropGenerator.Runtime",
    ];

    private readonly AssemblyDependencyResolver resolver = new(mainAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (SharedPrefixes.Any(prefix => assemblyName.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
            return Default.Assemblies.FirstOrDefault(assembly =>
                AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));

        string? path = resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}
