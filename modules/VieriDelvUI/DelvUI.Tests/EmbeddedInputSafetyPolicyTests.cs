using DelvUI.Helpers;
using Xunit;

namespace DelvUI.Tests;

public sealed class EmbeddedInputSafetyPolicyTests
{
    [Theory]
    [InlineData(@"C:\Users\player\AppData\Roaming\XIVLauncher\pluginConfigs\VieriDelvUI")]
    [InlineData(@"D:\Portable\Dalamud\VieriDelvUI")]
    public void StandaloneConfigurationAllowsNativeProxy(string path)
    {
        Assert.True(EmbeddedInputSafetyPolicy.AllowsNativeMouseProxy(path));
    }

    [Theory]
    [InlineData(@"C:\Users\player\AppData\Roaming\XIVLauncher\pluginConfigs\VieriNexus\NexusData\EmbeddedModules\Config\delvui")]
    [InlineData(@"D:/Dalamud/VieriNexus/NexusData/EmbeddedModules/Config/delvui")]
    public void NexusEmbeddedConfigurationRejectsNativeProxy(string path)
    {
        Assert.False(EmbeddedInputSafetyPolicy.AllowsNativeMouseProxy(path));
    }
}
