using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using VieriNexus.Application;

namespace VieriNexus.Application.Tests;

public sealed class QuestionableRouteCompatibilityPackTests
{
    private readonly QuestionableRouteCompatibilityPack pack = new();

    [Theory]
    [MemberData(nameof(BrokenRoutes))]
    public void CorrectsEachExactBrokenRouteAndIsIdempotent(int questId, string json)
    {
        QuestionableRouteCorrectionTarget target = QuestionableRouteCompatibilityPack.Targets.Single(x => x.QuestId == questId);

        QuestionableRouteCorrectionResult corrected = pack.Correct(target, json);

        Assert.Equal(QuestionableRouteCorrectionState.Applied, corrected.State);
        Assert.NotNull(corrected.CorrectedJson);
        QuestionableRouteCorrectionResult repeated = pack.Correct(target, corrected.CorrectedJson!);
        Assert.Equal(QuestionableRouteCorrectionState.AlreadyCorrect, repeated.State);
    }

    [Fact]
    public void UnknownUpstreamValueFailsClosed()
    {
        QuestionableRouteCorrectionTarget target = QuestionableRouteCompatibilityPack.Targets.Single(x => x.QuestId == 1063);
        string json = Route((255, Step(1006749, 131, "CompleteQuest", ("NextQuestId", 9999))));

        QuestionableRouteCorrectionResult result = pack.Correct(target, json);

        Assert.Equal(QuestionableRouteCorrectionState.Conflict, result.State);
        Assert.Null(result.CorrectedJson);
    }

    [Fact]
    public void MissingIndoorMountValueFailsClosed()
    {
        QuestionableRouteCorrectionTarget target = QuestionableRouteCompatibilityPack.Targets.Single(x => x.QuestId == 4645);
        string json = Route(
            (0, Step(1044156, 1055, "AcceptQuest")),
            (255, Step(2013035, 1055, "CompleteQuest", ("Mount", false))));

        QuestionableRouteCorrectionResult result = pack.Correct(target, json);

        Assert.Equal(QuestionableRouteCorrectionState.Conflict, result.State);
        Assert.Null(result.CorrectedJson);
    }

    [Fact]
    public void AtomicBundleInstallPreservesEveryUnrelatedEntryAndSupportsExactRollback()
    {
        string root = Path.Combine(Path.GetTempPath(), $"nexus-questionable-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string bundle = Path.Combine(root, "bundle.zip");
            string receipt = Path.Combine(root, "state", "receipt.json");
            string backups = Path.Combine(root, "backups");
            CreateBundle(bundle);
            byte[] original = File.ReadAllBytes(bundle);

            QuestionableCompatibilityInstallResult installed = pack.Install(bundle, backups, receipt);

            Assert.Equal(QuestionableCompatibilityInstallState.Installed, installed.State);
            Assert.True(installed.IsSatisfied);
            Assert.True(File.Exists(receipt));
            using (ZipArchive archive = ZipFile.OpenRead(bundle))
            {
                ZipArchiveEntry unrelated = Assert.Single(archive.Entries, x => x.FullName == "Other/keep.txt");
                using StreamReader reader = new(unrelated.Open());
                Assert.Equal("keep exactly", reader.ReadToEnd());
            }

            QuestionableCompatibilityInstallResult repeated = pack.Install(bundle, backups, receipt);
            Assert.Equal(QuestionableCompatibilityInstallState.ManagedActive, repeated.State);

            QuestionableCompatibilityInstallResult restored = pack.RestoreOfficialBundle(bundle, receipt);
            Assert.Equal(QuestionableCompatibilityInstallState.AlreadyCorrect, restored.State);
            Assert.Equal(original, File.ReadAllBytes(bundle));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BundleConflictDoesNotModifyAnything()
    {
        string root = Path.Combine(Path.GetTempPath(), $"nexus-questionable-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string bundle = Path.Combine(root, "bundle.zip");
            CreateBundle(bundle, monkNextQuest: 9999);
            byte[] original = File.ReadAllBytes(bundle);

            QuestionableCompatibilityInstallResult result = pack.Install(
                bundle, Path.Combine(root, "backups"), Path.Combine(root, "receipt.json"));

            Assert.Equal(QuestionableCompatibilityInstallState.Conflict, result.State);
            Assert.Equal(original, File.ReadAllBytes(bundle));
            Assert.False(Directory.Exists(Path.Combine(root, "backups")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    public static IEnumerable<object[]> BrokenRoutes() =>
        BrokenRouteCases().Select(value => new object[] { value.QuestId, value.Json });

    private static (int QuestId, string Json)[] BrokenRouteCases() =>
    [
        (1063, Route((255, Step(1006749, 131, "CompleteQuest", ("NextQuestId", 1604))))),
        (2893, Route((2, Step(2008682, 612, "Combat")))),
        (2565, Route((3, Step(1021894, 397, "SinglePlayerDuty")))),
        (4068, Route(
            (6, Step(1039248, 399, "SinglePlayerDuty")),
            (8, Step(1039248, 399, "Interact", ("StopDistance", 5))))),
        (4645, Route(
            (0, Step(1044156, 1055, "AcceptQuest", ("Mount", true))),
            (255, Step(2013035, 1055, "CompleteQuest", ("Mount", true))))),
    ];

    private static void CreateBundle(string path, int monkNextQuest = 1604)
    {
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(archive, "manifest.json", "{\"DataVersion\":1234}");
        Write(archive, "Other/keep.txt", "keep exactly");
        foreach ((int questId, string json) in BrokenRouteCases())
        {
            string value = questId == 1063
                ? Route((255, Step(1006749, 131, "CompleteQuest", ("NextQuestId", monkNextQuest))))
                : json;
            Write(archive, $"QuestPaths/Test/{questId}_Test.json", value);
        }
    }

    private static void Write(ZipArchive archive, string name, string value)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name);
        using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
        writer.Write(value);
    }

    private static JsonObject Step(int dataId, int territoryId, string interaction, params (string Name, object Value)[] extra)
    {
        var step = new JsonObject
        {
            ["DataId"] = dataId,
            ["TerritoryId"] = territoryId,
            ["InteractionType"] = interaction,
        };
        foreach ((string name, object value) in extra)
            step[name] = JsonValue.Create(value);
        return step;
    }

    private static string Route(params (int Sequence, JsonObject Step)[] values)
    {
        var sequences = new JsonArray();
        foreach ((int sequence, JsonObject step) in values)
            sequences.Add(new JsonObject
            {
                ["Sequence"] = sequence,
                ["Steps"] = new JsonArray(step),
            });
        return new JsonObject { ["QuestSequence"] = sequences }.ToJsonString();
    }
}
