using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VieriLink;

internal sealed class DiscordClient : IDisposable
{
    private readonly HttpClient http = new() { BaseAddress = new Uri("https://discord.com/api/v10/"), Timeout = TimeSpan.FromSeconds(15) };

    public void SetToken(string token)
    {
        http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token) ? null : new AuthenticationHeaderValue("Bot", token);
    }

    public async Task<string> SendAsync(string channelId, object payload, CancellationToken ct)
    {
        using HttpResponseMessage response = await SendWithRetry(() => http.PostAsJsonAsync($"channels/{channelId}/messages", payload, ct), ct);
        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
    }

    public async Task EditAsync(string channelId, string messageId, object payload, CancellationToken ct)
    {
        using HttpResponseMessage response = await SendWithRetry(() => http.PatchAsJsonAsync($"channels/{channelId}/messages/{messageId}", payload, ct), ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<string?> FindStatusMessageAsync(string channelId, string title, CancellationToken ct)
    {
        using HttpResponseMessage meResponse = await SendWithRetry(() => http.GetAsync("users/@me", ct), ct);
        await EnsureSuccessAsync(meResponse, ct);
        DiscordAuthor? me = await meResponse.Content.ReadFromJsonAsync<DiscordAuthor>(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(me?.Id)) return null;

        using HttpResponseMessage response = await SendWithRetry(() => http.GetAsync($"channels/{channelId}/messages?limit=100", ct), ct);
        await EnsureSuccessAsync(response, ct);
        List<DiscordMessage> messages = await response.Content.ReadFromJsonAsync<List<DiscordMessage>>(cancellationToken: ct) ?? [];
        return messages.FirstOrDefault(message =>
            message.Author.Id == me.Id &&
            message.Embeds.Any(embed => string.Equals(embed.Title, title, StringComparison.Ordinal)))?.Id;
    }

    public async Task<List<DiscordMessage>> ReadAfterAsync(string channelId, string after, CancellationToken ct)
    {
        string query = string.IsNullOrWhiteSpace(after) ? "?limit=25" : $"?limit=25&after={after}";
        using HttpResponseMessage response = await SendWithRetry(() => http.GetAsync($"channels/{channelId}/messages{query}", ct), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DiscordMessage>>(cancellationToken: ct) ?? [];
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string body = await response.Content.ReadAsStringAsync(ct);
        int? code = null;
        string? message = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("code", out JsonElement codeElement) && codeElement.TryGetInt32(out int parsed)) code = parsed;
            if (doc.RootElement.TryGetProperty("message", out JsonElement messageElement)) message = messageElement.GetString();
        }
        catch { }
        throw new DiscordApiException(response.StatusCode, code, message ?? response.ReasonPhrase ?? "Discord request failed");
    }

    private static async Task<HttpResponseMessage> SendWithRetry(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        HttpResponseMessage response = await send();
        if (response.StatusCode != HttpStatusCode.TooManyRequests) return response;
        double retry = 1;
        try
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            retry = body.RootElement.GetProperty("retry_after").GetDouble();
        }
        catch { }
        response.Dispose();
        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(retry, .25, 10)), ct);
        return await send();
    }

    public void Dispose() => http.Dispose();
}

internal sealed class DiscordMessage
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DiscordAuthor Author { get; set; } = new();
    public List<DiscordEmbed> Embeds { get; set; } = [];
}

internal sealed class DiscordEmbed
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class DiscordAuthor
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool Bot { get; set; }
}

internal sealed class DiscordApiException : HttpRequestException
{
    public DiscordApiException(HttpStatusCode statusCode, int? discordErrorCode, string message)
        : base(message, null, statusCode)
    {
        DiscordErrorCode = discordErrorCode;
    }

    public int? DiscordErrorCode { get; }
    public bool IsUnknownMessage => StatusMessagePolicy.MayReplace(StatusCode!.Value, DiscordErrorCode);
}
