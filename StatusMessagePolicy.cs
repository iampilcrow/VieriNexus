using System.Net;

namespace VieriLink;

internal static class StatusMessagePolicy
{
    internal static bool MayReplace(HttpStatusCode statusCode, int? discordErrorCode) =>
        statusCode == HttpStatusCode.NotFound && discordErrorCode == 10008;
}
