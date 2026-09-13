using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class CodexMigrationImporterTests
{
    [Fact]
    public void ImportsCompleteCodexActivityAndStopPreferences()
    {
        const string json = """
        {
          "Version": 2,
          "Codex": {
            "MainScenarioQuests": true,
            "CombatClassJobQuests": true,
            "RoleQuests": false,
            "AetherCurrentQuests": true,
            "FieldAetherCurrents": true,
            "AetheryteAttunements": true,
            "MapExploration": false,
            "HuntingLogs": true,
            "SideQuests": false,
            "Achievements": true,
            "RequiredMsqDungeons": true
          },
          "Stop": { "LevelToStopAfter": true, "TargetLevel": 87 },
          "ProgressionQueue": {
            "Steps": [
              { "Id": "58f548eb-afd2-44dd-9fff-b15227d5880d", "Enabled": true, "ClassJob": 35, "TargetLevel": 100, "Method": 3, "FallbackPolicy": 0 },
              { "Enabled": false, "ClassJob": 31, "TargetLevel": 90, "Method": 0, "FallbackPolicy": 2 }
            ],
            "Settings": { "ResumeAfterRestart": false, "AutomaticUsesSideQuests": false }
          }
        }
        """;

        CodexMigrationPreview preview = new CodexMigrationImporter().Preview(json);

        Assert.True(preview.CanImport);
        CodexMigrationSnapshot snapshot = Assert.IsType<CodexMigrationSnapshot>(preview.Snapshot);
        Assert.Equal(2, snapshot.SourceConfigurationVersion);
        Assert.True(snapshot.MainScenarioQuests);
        Assert.True(snapshot.CombatClassJobQuests);
        Assert.False(snapshot.RoleQuests);
        Assert.True(snapshot.FieldAetherCurrents);
        Assert.False(snapshot.MapExploration);
        Assert.True(snapshot.LevelStopEnabled);
        Assert.Equal(87, snapshot.TargetLevel);
        Assert.Equal(2, snapshot.SavedQueueSteps);
        Assert.Equal(35u, snapshot.QueueSteps[0].ClassJobId);
        Assert.Equal(100, snapshot.QueueSteps[0].TargetLevel);
        Assert.Equal(3, snapshot.QueueSteps[0].Method);
        Assert.False(snapshot.QueueSettings.ResumeAfterRestart);
        Assert.False(snapshot.QueueSettings.AutomaticUsesSideQuests);
        Assert.Contains(preview.Issues, issue => issue.Message.Contains("Nexus-owned queue", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingOptionalSectionsUseSafeDefaults()
    {
        CodexMigrationPreview preview = new CodexMigrationImporter().Preview("""{ "Codex": {} }""");

        Assert.True(preview.CanImport);
        Assert.Equal(50, preview.Snapshot!.TargetLevel);
        Assert.True(preview.Snapshot.RequiredMsqDungeons);
        Assert.Equal(0, preview.Snapshot.SavedQueueSteps);
    }

    [Fact]
    public void NullAndWrongTypesDoNotThrow()
    {
        const string json = """
        {
          "Version": null,
          "Codex": { "MainScenarioQuests": null, "RequiredMsqDungeons": "yes" },
          "Stop": { "LevelToStopAfter": null, "TargetLevel": null },
          "ProgressionQueue": { "Steps": null }
        }
        """;

        CodexMigrationPreview preview = new CodexMigrationImporter().Preview(json);

        Assert.True(preview.CanImport);
        Assert.False(preview.Snapshot!.MainScenarioQuests);
        Assert.True(preview.Snapshot.RequiredMsqDungeons);
        Assert.Equal(50, preview.Snapshot.TargetLevel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{ nope }")]
    [InlineData("{ \"Codex\": null }")]
    public void InvalidOrMissingCodexRootFailsClosed(string json)
    {
        CodexMigrationPreview preview = new CodexMigrationImporter().Preview(json);

        Assert.False(preview.CanImport);
        Assert.Contains(preview.Issues, issue => issue.Severity == MigrationIssueSeverity.Error);
    }
}
