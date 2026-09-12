using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class CommandCenterMigrationImporterTests
{
    [Fact]
    public void ImportsCompleteVieriDeckSettingsWithoutTypeMetadata()
    {
        const string json = """
        {
          "$type": "VieriDeck.Configuration, VieriDeck",
          "Version": 3,
          "ShowUnloadedPlugins": false,
          "HidePluginsWithoutActions": true,
          "CloseDeckAfterOpeningPlugin": true,
          "CommandPanelOpen": true,
          "SelectedCommandPluginId": "AutoDuty",
          "OnlyShowFavorites": true,
          "ListWindowWidth": 250.5,
          "CommandPanelWidth": 583.3,
          "WindowHeight": 451.3,
          "UiScale": 1.2,
          "HasDeckPosition": true,
          "DeckPositionX": 504.0,
          "DeckPositionY": 48.0,
          "Favorites": ["AutoDuty", "DelvUI", "autoduty"],
          "HiddenPlugins": ["Example"],
          "PreferredCommands": {
            "$type": "dictionary metadata",
            "AutoDuty": "/ad"
          },
          "CustomCommands": {
            "$type": "dictionary metadata",
            "AutoDuty": [
              { "$type": "VieriDeck.CustomCommand", "Command": "ad stop", "Description": "Stop it" }
            ]
          },
          "HotkeyEnabled": true,
          "Hotkey": 123,
          "HotkeyControl": false,
          "HotkeyShift": true,
          "HotkeyAlt": false,
          "ExactModifiers": true
        }
        """;

        CommandCenterMigrationPreview preview = new CommandCenterMigrationImporter().Preview(json);

        Assert.True(preview.CanImport);
        CommandCenterSnapshot snapshot = Assert.IsType<CommandCenterSnapshot>(preview.Snapshot);
        Assert.Equal(3, snapshot.SourceConfigurationVersion);
        Assert.False(snapshot.ShowUnloadedPlugins);
        Assert.True(snapshot.HidePluginsWithoutActions);
        Assert.True(snapshot.CloseAfterOpeningPlugin);
        Assert.True(snapshot.CommandPanelOpen);
        Assert.True(snapshot.HasSourceWindowPosition);
        Assert.Equal(504f, snapshot.SourceWindowPositionX);
        Assert.Equal(48f, snapshot.SourceWindowPositionY);
        Assert.Equal(["AutoDuty", "DelvUI"], snapshot.Favorites);
        Assert.Equal("/ad", snapshot.PreferredCommands["AutoDuty"]);
        Assert.Equal("/ad stop", Assert.Single(snapshot.CustomCommands["AutoDuty"]).Command);
        Assert.Equal(123, snapshot.Hotkey);
        Assert.True(snapshot.HotkeyShift);
    }

    [Fact]
    public void NullAndWrongTypeCollectionsAreSafeDefaults()
    {
        const string json = """
        { "Version": null, "Favorites": null, "HiddenPlugins": {}, "PreferredCommands": [], "CustomCommands": null }
        """;

        CommandCenterMigrationPreview preview = new CommandCenterMigrationImporter().Preview(json);

        Assert.True(preview.CanImport);
        Assert.Empty(preview.Snapshot!.Favorites);
        Assert.Empty(preview.Snapshot.HiddenPlugins);
        Assert.Empty(preview.Snapshot.PreferredCommands);
        Assert.Empty(preview.Snapshot.CustomCommands);
        Assert.Contains(preview.Issues, issue => issue.Severity == MigrationIssueSeverity.Warning);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{ nope }")]
    public void InvalidRootsFailClosed(string json)
    {
        CommandCenterMigrationPreview preview = new CommandCenterMigrationImporter().Preview(json);

        Assert.False(preview.CanImport);
        Assert.Null(preview.Snapshot);
        Assert.Contains(preview.Issues, issue => issue.Severity == MigrationIssueSeverity.Error);
    }
}
