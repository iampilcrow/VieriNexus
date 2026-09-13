using System.Text.Json;

namespace VieriNexus.Application.Tests;

public sealed class ProgressAtlasActionCatalogTests
{
    [Fact]
    public void FieldAetherCurrentCatalogHasUniqueExactTargetsAndFiniteCoordinates()
    {
        string path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "VieriNexus.Plugin", "Data", "field_aether_currents.json"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement[] entries = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(152, entries.Length);
        Assert.Equal(entries.Length, entries.Select(entry => entry.GetProperty("AetherCurrentId").GetUInt32()).Distinct().Count());
        Assert.All(entries, entry =>
        {
            Assert.NotEqual(0u, entry.GetProperty("AetherCurrentId").GetUInt32());
            Assert.NotEqual(0u, entry.GetProperty("DataId").GetUInt32());
            Assert.NotEqual(0u, entry.GetProperty("TerritoryId").GetUInt32());
            Assert.True(float.IsFinite(entry.GetProperty("X").GetSingle()));
            Assert.True(float.IsFinite(entry.GetProperty("Y").GetSingle()));
            Assert.True(float.IsFinite(entry.GetProperty("Z").GetSingle()));
        });
    }
}
