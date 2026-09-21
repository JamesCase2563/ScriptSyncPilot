using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ScriptSyncPilot.HandySync;

public sealed class HandyApiException(string message) : Exception(message);

/// <summary>
/// Minimal Handy REST API v3 client covering exactly what HSSP-driven video
/// sync needs: server time sampling for clock offset, script hosting upload,
/// and the HSSP setup/play/synctime/stop calls.
///
/// See docs/handy-api-v3.md and docs/EXTRACTED/HANDY_API_EXTRACT.md for the
/// endpoints this wraps.
/// </summary>
public sealed class HandyApiClient : IDisposable
{
    private const string BaseUrl = "https://www.handyfeeling.com/api/handy-rest/v3/";
    private const string HostingUploadUrl = "https://www.handyfeeling.com/api/hosting/v2/upload";

    private readonly HttpClient _http;
    private readonly HttpClient _hostingHttp = new();

    public HandyApiClient(string apiKey, string connectionKey)
    {
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        _http.DefaultRequestHeaders.Add("X-Connection-Key", connectionKey);
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct)
    {
        using var resp = await GetAsync("connected", ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<DeviceResponse<ConnectedResult>>(body);
        return parsed?.Result?.Connected ?? false;
    }

    public async Task<long> GetServerTimeMsAsync(CancellationToken ct)
    {
        using var resp = await GetAsync("servertime", ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<ServerTimeResponse>(body);
        return parsed?.ServerTime ?? throw new HandyApiException($"servertime: empty response ({body})");
    }

    /// <summary>
    /// Estimates the client-to-server clock offset per the algorithm in
    /// docs/handy-api-v3.md (round-trip halving, averaged over N samples).
    /// Add this to a local Unix-ms timestamp to get an estimated server time.
    /// </summary>
    public async Task<long> EstimateClockOffsetMsAsync(int samples, CancellationToken ct)
    {
        long total = 0;
        for (var i = 0; i < samples; i++)
        {
            var tSend = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var serverTime = await GetServerTimeMsAsync(ct);
            var tReceive = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var rtd = tReceive - tSend;
            var tsEst = serverTime + rtd / 2;
            total += tsEst - tReceive;
        }
        return total / samples;
    }

    /// <summary>Uploads a script as CSV to Handy's temporary hosting endpoint and returns its download URL.</summary>
    public async Task<string> UploadScriptCsvAsync(string csv, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "script.csv");

        using var resp = await _hostingHttp.PostAsync(HostingUploadUrl, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HandyApiException($"script upload failed: {(int)resp.StatusCode} {body}");

        var parsed = System.Text.Json.JsonSerializer.Deserialize<UploadResponse>(body);
        return parsed?.Url ?? throw new HandyApiException($"script upload: no url in response ({body})");
    }

    /// <summary>Device mode: 0=HAMP, 1=HSSP, 2=HDSP, 4=HSP, ...</summary>
    public Task SetModeAsync(int mode, CancellationToken ct) =>
        PutAsync("mode2", new { mode }, ct);

    public Task HsspSetupAsync(string scriptUrl, CancellationToken ct) =>
        PutAsync("hssp/setup", new { url = scriptUrl }, ct);

    public Task HsspPlayAsync(long startTimeMs, long serverTimeMs, double playbackRate = 1.0, bool loop = false, CancellationToken ct = default) =>
        PutAsync("hssp/play", new { start_time = startTimeMs, server_time = serverTimeMs, playback_rate = playbackRate, loop }, ct);

    public Task HsspStopAsync(CancellationToken ct) =>
        PutAsync("hssp/stop", null, ct);

    /// <summary>Nudges the device's running playback clock towards currentTimeMs without restarting playback.</summary>
    public Task HsspSyncTimeAsync(long currentTimeMs, long serverTimeMs, double filter = 0.5, CancellationToken ct = default) =>
        PutAsync("hssp/synctime", new { current_time = currentTimeMs, server_time = serverTimeMs, filter }, ct);

    private async Task PutAsync(string path, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, path);
        if (body != null) req.Content = JsonContent.Create(body);
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccessAsync(path, resp, ct);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct)
    {
        var resp = await _http.GetAsync(path, ct);
        await EnsureSuccessAsync(path, resp, ct);
        return resp;
    }

    private static async Task EnsureSuccessAsync(string path, HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var statusCode = (int)response.StatusCode;
        var reason = response.ReasonPhrase;
        var body = await response.Content.ReadAsStringAsync(ct);
        response.Dispose();
        throw new HandyApiException($"{path} failed: {statusCode} {reason}. Handy response: {body}");
    }

    public void Dispose()
    {
        _http.Dispose();
        _hostingHttp.Dispose();
    }

    private sealed class ServerTimeResponse
    {
        [JsonPropertyName("server_time")] public long ServerTime { get; set; }
    }

    private sealed class UploadResponse
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    private sealed class DeviceResponse<T>
    {
        [JsonPropertyName("result")] public T? Result { get; set; }
    }

    private sealed class ConnectedResult
    {
        [JsonPropertyName("connected")] public bool Connected { get; set; }
    }
}
