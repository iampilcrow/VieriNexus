using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class PluginPagePolicyTests
{
    [Fact]
    public void FavoritesAreListedFirstAndExcludedFromAllOtherPlugins()
    {
        PluginPageGroups groups = PluginPagePolicy.Group(
            ["AutoDuty", "DelvUI", "Lifestream", "Questionable"],
            ["Lifestream", "autoduty"]);

        Assert.Equal(["AutoDuty", "Lifestream"], groups.Favorites);
        Assert.Equal(["DelvUI", "Questionable"], groups.AllOtherPlugins);
    }

    [Fact]
    public void CommandButtonOpensClickedPluginAndClosesItOnSecondClick()
    {
        PluginCommandPanelState opened = PluginPagePolicy.ToggleCommands(false, string.Empty, "Lifestream");
        PluginCommandPanelState switched = PluginPagePolicy.ToggleCommands(true, "Lifestream", "Questionable");
        PluginCommandPanelState closed = PluginPagePolicy.ToggleCommands(true, "Lifestream", "lifestream");

        Assert.Equal(new(true, "Lifestream"), opened);
        Assert.Equal(new(true, "Questionable"), switched);
        Assert.Equal(new(false, "lifestream"), closed);
    }
}
