using System.Net;
using Xunit;

namespace VieriLink.Tests;

public sealed class StatusMessagePolicyTests
{
    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout, null)]
    [InlineData(HttpStatusCode.TooManyRequests, null)]
    [InlineData(HttpStatusCode.InternalServerError, null)]
    [InlineData(HttpStatusCode.ServiceUnavailable, null)]
    [InlineData(HttpStatusCode.Forbidden, 50001)]
    [InlineData(HttpStatusCode.NotFound, 10003)]
    public void TransientOrAccessFailuresNeverCreateReplacementMessages(HttpStatusCode status, int? discordCode) =>
        Assert.False(StatusMessagePolicy.MayReplace(status, discordCode));

    [Fact]
    public void OnlyDiscordUnknownMessageAllowsReplacement() =>
        Assert.True(StatusMessagePolicy.MayReplace(HttpStatusCode.NotFound, 10008));
}
