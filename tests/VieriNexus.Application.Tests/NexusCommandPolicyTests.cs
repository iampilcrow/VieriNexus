using VieriNexus.Application;
using VieriNexus.Contracts;

namespace VieriNexus.Application.Tests;

public sealed class NexusCommandPolicyTests
{
    [Fact]
    public void PublicAndApplicationContractVersionsStayAligned() =>
        Assert.Equal(NexusIpc.CurrentVersion, NexusCommandPolicy.ContractVersion);

    [Fact]
    public void NormalizesSafeAliases()
    {
        NexusCommandValidation result = NexusCommandPolicy.Validate(
            new NexusCommandRequest(1, Guid.NewGuid(), "  LAST ", "{}", 42), 42);

        Assert.True(result.IsValid);
        Assert.Equal("progression.last", result.Command);
    }

    [Fact]
    public void RejectsMutationForAnotherCharacter()
    {
        NexusCommandValidation result = NexusCommandPolicy.Validate(
            new NexusCommandRequest(1, Guid.NewGuid(), "maintenance.repair", "{}", 41), 42);

        Assert.False(result.IsValid);
        Assert.Equal("character-mismatch", result.Code);
    }

    [Fact]
    public void AllowsReadOnlyStatusWithoutCharacter()
    {
        NexusCommandValidation result = NexusCommandPolicy.Validate(
            new NexusCommandRequest(1, Guid.NewGuid(), "status", "{}", null), 0);

        Assert.True(result.IsValid);
        Assert.Equal("status", result.Command);
    }

    [Theory]
    [InlineData(2, "status", "unsupported-contract")]
    [InlineData(1, "leave", "unknown-command")]
    [InlineData(1, "pause", "unknown-command")]
    public void RejectsUnsupportedOrUnsafeSurfaces(int version, string command, string code)
    {
        NexusCommandValidation result = NexusCommandPolicy.Validate(
            new NexusCommandRequest(version, Guid.NewGuid(), command, "{}", 42), 42);

        Assert.False(result.IsValid);
        Assert.Equal(code, result.Code);
    }

    [Fact]
    public void BoundsPayload()
    {
        NexusCommandValidation result = NexusCommandPolicy.Validate(
            new NexusCommandRequest(1, Guid.NewGuid(), "route.play",
                new string('x', NexusCommandPolicy.MaximumPayloadLength + 1), 42), 42);

        Assert.False(result.IsValid);
        Assert.Equal("payload-too-large", result.Code);
    }

    [Fact]
    public void AllowsCharacterScopedFastJobSwitchRequest()
    {
        NexusCommandValidation result = NexusCommandPolicy.Validate(
            new NexusCommandRequest(1, Guid.NewGuid(), "job.switch", "{\"job\":\"MCH\"}", 42), 42);

        Assert.True(result.IsValid);
        Assert.Equal("job.switch", result.Command);
    }

    [Theory]
    [InlineData("stop", "character-unavailable")]
    [InlineData("maintenance.run", "character-unavailable")]
    public void RejectsMutationsWithoutCurrentCharacter(string command, string code)
    {
        NexusCommandValidation result = NexusCommandPolicy.Validate(
            new NexusCommandRequest(1, Guid.NewGuid(), command, "{}", null), 0);

        Assert.False(result.IsValid);
        Assert.Equal(code, result.Code);
    }
}
