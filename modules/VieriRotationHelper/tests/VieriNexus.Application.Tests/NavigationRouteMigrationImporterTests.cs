using System.Text.Json;
using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class NavigationRouteMigrationImporterTests
{
    [Fact]
    public void PreviewPreservesEveryRouteAndDisplaySettingWithoutEnablingOverrides()
    {
        Guid id = Guid.Parse("5c1793cb-2590-4b40-8b86-f6dfa9aca755");
        string json = $$"""
        {
          "Version": 1,
          "RecordingIntervalSeconds": 1.25,
          "MinimumPointDistance": 0.85,
          "ShowWorldPreview": false,
          "ShowPointNumbers": false,
          "ShowLiveNavigationPath": true,
          "LibraryPaneWidth": 412.5,
          "SelectedRouteId": "{{id}}",
          "Routes": [{
            "Id": "{{id}}",
            "Name": "Safe vendor hall",
            "TerritoryId": 133,
            "Points": [{ "X": 1.1, "Y": 2.2, "Z": 3.3 }, { "X": 4.4, "Y": 5.5, "Z": 6.6 }],
            "Notes": "Keep this note",
            "Tags": "vendor,manual",
            "UseMesh": true,
            "UseFlight": false,
            "Tolerance": 0.75,
            "LastPointTolerance": 1.25,
            "BindingKind": 1,
            "TargetDataId": 1000215,
            "TargetLabel": "Domitien",
            "OverrideEnabled": false,
            "UpdatedAtUtc": "2026-09-07T21:00:00Z"
          }]
        }
        """;

        NavigationMigrationPreview preview = new NavigationRouteMigrationImporter().Preview(json);

        Assert.True(preview.CanImport);
        NavigationLibrarySnapshot snapshot = Assert.IsType<NavigationLibrarySnapshot>(preview.Snapshot);
        Assert.Equal(1.25f, snapshot.RecordingIntervalSeconds);
        Assert.Equal(.85f, snapshot.MinimumPointDistance);
        Assert.False(snapshot.ShowWorldPreview);
        Assert.False(snapshot.ShowPointNumbers);
        Assert.True(snapshot.ShowLiveNavigationPath);
        Assert.Equal(412.5f, snapshot.LibraryPaneWidth);
        Assert.Equal(id, snapshot.SelectedRouteId);
        NavigationRouteSnapshot route = Assert.Single(snapshot.Routes);
        Assert.Equal("Safe vendor hall", route.Name);
        Assert.Equal(133u, route.TerritoryId);
        Assert.Equal([new NavigationRoutePoint(1.1f, 2.2f, 3.3f), new NavigationRoutePoint(4.4f, 5.5f, 6.6f)], route.Points);
        Assert.Equal("Keep this note", route.Notes);
        Assert.Equal("vendor,manual", route.Tags);
        Assert.True(route.UseMesh);
        Assert.False(route.UseFlight);
        Assert.Equal(.75f, route.Tolerance);
        Assert.Equal(1.25f, route.LastPointTolerance);
        Assert.Equal(1, route.BindingKind);
        Assert.Equal(1000215u, route.TargetDataId);
        Assert.Equal("Domitien", route.TargetLabel);
        Assert.False(route.OverrideEnabled);
    }

    [Fact]
    public void PreviewRejectsDuplicateStableIds()
    {
        Guid id = Guid.NewGuid();
        string json = JsonSerializer.Serialize(new
        {
            Version = 1,
            Routes = new[]
            {
                new { Id = id, Name = "One", TerritoryId = 1, Points = Array.Empty<object>() },
                new { Id = id, Name = "Two", TerritoryId = 2, Points = Array.Empty<object>() },
            },
        });

        NavigationMigrationPreview preview = new NavigationRouteMigrationImporter().Preview(json);

        Assert.False(preview.CanImport);
        Assert.Contains(preview.Issues, issue => issue.Severity == MigrationIssueSeverity.Error && issue.Message.Contains("duplicated"));
    }

    [Fact]
    public void PreviewRejectsRouteBeyondPointSafetyLimit()
    {
        string json = JsonSerializer.Serialize(new
        {
            Version = 1,
            Routes = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Too large",
                    TerritoryId = 1,
                    Points = Enumerable.Range(0, 10_001).Select(index => new { X = index, Y = 0, Z = 0 }),
                },
            },
        });

        NavigationMigrationPreview preview = new NavigationRouteMigrationImporter().Preview(json);

        Assert.False(preview.CanImport);
        Assert.Contains(preview.Issues,
            issue => issue.Severity == MigrationIssueSeverity.Error && issue.Message.Contains("10,000-point"));
    }
}
