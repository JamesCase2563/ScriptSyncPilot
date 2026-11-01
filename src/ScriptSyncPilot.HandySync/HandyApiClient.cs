using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace ScriptSyncPilot.HandySync;

public sealed class HandyApiException(string message, int? code = null, string? name = null) : Exception(message)
{
    public int? Code { get; } = code;
    public string? Name { get; } = name;
}

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
    private const string ApplicationId = "LGtuDB6Ikqu4s65pK7eoshlqkDV_ziiG";
    private const string BaseUrl = "https://www.handyfeeling.com/api/handy-rest/v3/";
    private const string HostingUploadUrl = "https://www.handyfeeling.com/api/hosting/v2/upload";
    private static readonly TimeSpan ControlRequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ServerTimeRequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ScriptUploadTimeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly HttpClient _hostingHttp;
    private readonly TimeSpan _controlRequestTimeout;
    private readonly TimeSpan _serverTimeRequestTimeout;
    private readonly TimeSpan _scriptUploadTimeout;
    private readonly CancellationTokenSource _pendingRequestsCts = new();
    private int _disposed;

    public HandyApiClient(string connectionKey)
        : this(connectionKey, new HttpClientHandler(), new Uri(BaseUrl))
    {
    }

    internal HandyApiClient(string connectionKey, HttpMessageHandler handler, Uri? baseUri = null,
        HttpMessageHandler? hostingHandler = null, TimeSpan? controlRequestTimeout = null,
        TimeSpan? serverTimeRequestTimeout = null, TimeSpan? scriptUploadTimeout = null)
    {
        _controlRequestTimeout = controlRequestTimeout ?? ControlRequestTimeout;
        _serverTimeRequestTimeout = serverTimeRequestTimeout ?? ServerTimeRequestTimeout;
        _scriptUploadTimeout = scriptUploadTimeout ?? ScriptUploadTimeout;
        _hostingHttp = hostingHandler is null ? new HttpClient() : new HttpClient(hostingHandler);
        _hostingHttp.Timeout = Timeout.InfiniteTimeSpan;
        _http = new HttpClient(handler) { BaseAddress = baseUri ?? new Uri(BaseUrl), Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.Add("X-Api-Key", ApplicationId);
        _http.DefaultRequestHeaders.Add("X-Connection-Key", connectionKey);
    }

    public Task<bool> IsConnectedAsync(CancellationToken ct) => WithRequestTimeoutAsync("connected", _controlRequestTimeout, ct, async token =>
    {
        using var resp = await GetAsync("connected", token);
        var body = await ReadBodyAsync("connected", resp, token);
        var parsed = ParseResponse<DeviceResponse<ConnectedResult>>("connected", body);
        ThrowIfDeviceError("connected", parsed?.Error);
        return parsed?.Result?.Connected ?? false;
    });

    public Task<long> GetServerTimeMsAsync(CancellationToken ct) => WithRequestTimeoutAsync("servertime", _serverTimeRequestTimeout, ct, async token =>
    {
        using var resp = await GetAsync("servertime", token);
        var body = await ReadBodyAsync("servertime", resp, token);
        var parsed = ParseResponse<ServerTimeResponse>("servertime", body);
        return parsed?.ServerTime ?? throw new HandyApiException("servertime: empty response.");
    });

    /// <summary>
    /// Estimates the client-to-server clock offset per the algorithm in
    /// docs/handy-api-v3.md (round-trip halving, averaged over N samples).
    /// Add this to a local Unix-ms timestamp to get an estimated server time.
    /// </summary>
    public async Task<long> EstimateClockOffsetMsAsync(int samples, CancellationToken ct)
    {
        if (samples <= 0) throw new ArgumentOutOfRangeException(nameof(samples), "Samples must be >= 1.");

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

    public Task<DeviceInfo> GetInfoAsync(CancellationToken ct) =>
        WithRequestTimeoutAsync("info", _controlRequestTimeout, ct, token => GetResultAsync<DeviceInfo>("info", token));

    public Task<int> GetOffsetAsync(CancellationToken ct) =>
        WithRequestTimeoutAsync("hstp/offset", _controlRequestTimeout, ct,
            async token => (await GetResultAsync<OffsetResult>("hstp/offset", token)).Offset);

    public Task SetOffsetAsync(int offsetMs, CancellationToken ct) =>
        WithRequestTimeoutAsync("hstp/offset", _controlRequestTimeout, ct,
            token => PutAsync("hstp/offset", new { offset = offsetMs }, token));

    public Task<SliderStroke> GetStrokeAsync(CancellationToken ct) =>
        WithRequestTimeoutAsync("slider/stroke", _controlRequestTimeout, ct,
            token => GetResultAsync<SliderStroke>("slider/stroke", token));

    public Task<SliderStroke> SetStrokeAsync(double min, double max, CancellationToken ct) =>
        WithRequestTimeoutAsync("slider/stroke", _controlRequestTimeout, ct,
            token => PutResultAsync<SliderStroke>("slider/stroke", new { min, max }, token));

    public Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct) =>
        WithRequestTimeoutAsync("capabilities", _controlRequestTimeout, ct,
            token => GetResultAsync<DeviceCapabilities>("capabilities", token));

    public Task<SliderSettings> GetSliderSettingsAsync(CancellationToken ct) =>
        WithRequestTimeoutAsync("settings/slider", _controlRequestTimeout, ct,
            token => GetResultAsync<SliderSettings>("settings/slider", token));

    public Task<SliderState> GetSliderStateAsync(CancellationToken ct) =>
        WithRequestTimeoutAsync("slider/state", _controlRequestTimeout, ct,
            token => GetResultAsync<SliderState>("slider/state", token));

    /// <summary>Uploads a script as CSV to Handy's temporary hosting endpoint and returns its download URL.</summary>
    public Task<string> UploadScriptCsvAsync(string csv, CancellationToken ct) =>
        WithRequestTimeoutAsync("script upload", _scriptUploadTimeout, ct, async token =>
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "script.csv");

        using var request = new HttpRequestMessage(HttpMethod.Post, HostingUploadUrl) { Content = content };
        using var resp = await SendAsync(_hostingHttp, "script upload", request, token);
        if (!resp.IsSuccessStatusCode)
            throw new HandyApiException($"script upload failed: {(int)resp.StatusCode}.");

        var body = await ReadBodyAsync("script upload", resp, token);
        var parsed = ParseResponse<UploadResponse>("script upload", body);
        if (string.IsNullOrWhiteSpace(parsed?.Url))
            throw new HandyApiException("script upload: no url in response.");
        return parsed.Url;
    });

    /// <summary>Device mode: 0=HAMP, 1=HSSP, 2=HDSP, 4=HSP, ...</summary>
    public Task SetModeAsync(int mode, CancellationToken ct) =>
        WithRequestTimeoutAsync("mode2", _controlRequestTimeout, ct,
            token => PutAsync("mode2", new { mode }, token));

    public Task HsspSetupAsync(string scriptUrl, CancellationToken ct) =>
        WithRequestTimeoutAsync("hssp/setup", _controlRequestTimeout, ct,
            token => PutAsync("hssp/setup", new { url = scriptUrl }, token));

    public Task HsspPlayAsync(long startTimeMs, long serverTimeMs, double playbackRate = 1.0, bool loop = false, CancellationToken ct = default) =>
        WithRequestTimeoutAsync("hssp/play", _controlRequestTimeout, ct,
            token => PutAsync("hssp/play", new { start_time = startTimeMs, server_time = serverTimeMs, playback_rate = playbackRate, loop }, token));

    public Task HsspStopAsync(CancellationToken ct) =>
        WithRequestTimeoutAsync("hssp/stop", _controlRequestTimeout, ct,
            token => PutAsync("hssp/stop", null, token));

    internal Task HsspStopForDisconnectAsync() =>
        WithRequestTimeoutAsync("hssp/stop", _controlRequestTimeout, CancellationToken.None,
            token => PutAsync("hssp/stop", null, token), ignorePendingRequestCancellation: true);

    internal void CancelPendingRequests()
    {
        try { _pendingRequestsCts.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    /// <summary>Nudges the device's running playback clock towards currentTimeMs without restarting playback.</summary>
    public Task HsspSyncTimeAsync(long currentTimeMs, long serverTimeMs, double filter = 0.5, CancellationToken ct = default) =>
        WithRequestTimeoutAsync("hssp/synctime", _controlRequestTimeout, ct,
            token => PutAsync("hssp/synctime", new { current_time = currentTimeMs, server_time = serverTimeMs, filter }, token));

    private async Task<T> WithRequestTimeoutAsync<T>(string operation, TimeSpan timeout,
        CancellationToken callerToken, Func<CancellationToken, Task<T>> sendAndRead,
        bool ignorePendingRequestCancellation = false)
    {
        using var requestCts = ignorePendingRequestCancellation
            ? CancellationTokenSource.CreateLinkedTokenSource(callerToken)
            : CancellationTokenSource.CreateLinkedTokenSource(callerToken, _pendingRequestsCts.Token);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(requestCts.Token);
        timeoutCts.CancelAfter(timeout);
        try
        {
            return await sendAndRead(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw new TaskCanceledException($"{operation}: request canceled.", null, callerToken);
        }
        catch (OperationCanceledException) when (!ignorePendingRequestCancellation && _pendingRequestsCts.IsCancellationRequested)
        {
            throw new TaskCanceledException($"{operation}: request canceled.", null, callerToken);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TaskCanceledException($"{operation}: request timed out.", null, callerToken);
        }
    }

    private async Task WithRequestTimeoutAsync(string operation, TimeSpan timeout,
        CancellationToken callerToken, Func<CancellationToken, Task> sendAndRead,
        bool ignorePendingRequestCancellation = false)
    {
        using var requestCts = ignorePendingRequestCancellation
            ? CancellationTokenSource.CreateLinkedTokenSource(callerToken)
            : CancellationTokenSource.CreateLinkedTokenSource(callerToken, _pendingRequestsCts.Token);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(requestCts.Token);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await sendAndRead(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw new TaskCanceledException($"{operation}: request canceled.", null, callerToken);
        }
        catch (OperationCanceledException) when (!ignorePendingRequestCancellation && _pendingRequestsCts.IsCancellationRequested)
        {
            throw new TaskCanceledException($"{operation}: request canceled.", null, callerToken);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TaskCanceledException($"{operation}: request timed out.", null, callerToken);
        }
    }

    private async Task PutAsync(string path, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, path);
        if (body != null) req.Content = JsonContent.Create(body);
        using var resp = await SendAsync(_http, path, req, ct);
        await EnsureSuccessAsync(path, resp, ct);
        await EnsureNoDeviceErrorAsync(path, resp, ct);
    }

    private async Task<T> PutResultAsync<T>(string path, object body, CancellationToken ct) where T : class
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        using var resp = await SendAsync(_http, path, req, ct);
        await EnsureSuccessAsync(path, resp, ct);
        return await ReadResultAsync<T>(path, resp, ct);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var resp = await SendAsync(_http, path, request, ct);
        await EnsureSuccessAsync(path, resp, ct);
        return resp;
    }

    private async Task<T> GetResultAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await GetAsync(path, ct);
        return await ReadResultAsync<T>(path, response, ct);
    }

    private static async Task<T> ReadResultAsync<T>(string path, HttpResponseMessage response, CancellationToken ct) where T : class
    {
        var body = await ReadBodyAsync(path, response, ct);
        DeviceResponse<T>? parsed;
        try { parsed = System.Text.Json.JsonSerializer.Deserialize<DeviceResponse<T>>(body); }
        catch (System.Text.Json.JsonException)
        {
            throw new HandyApiException($"{path}: invalid JSON response.");
        }
        ThrowIfDeviceError(path, parsed?.Error);

        return parsed?.Result ?? throw new HandyApiException($"{path}: response did not contain a result.");
    }

    private static async Task EnsureNoDeviceErrorAsync(string path, HttpResponseMessage response, CancellationToken ct)
    {
        var body = await ReadBodyAsync(path, response, ct);
        if (string.IsNullOrWhiteSpace(body)) return;

        System.Text.Json.JsonDocument document;
        try { document = System.Text.Json.JsonDocument.Parse(body); }
        catch (System.Text.Json.JsonException)
        {
            throw new HandyApiException($"{path}: invalid JSON response.");
        }
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new HandyApiException($"{path}: invalid response shape.");
            if (document.RootElement.TryGetProperty("error", out var error))
                ThrowIfDeviceError(path, error);
        }
    }

    private static async Task EnsureSuccessAsync(string path, HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var statusCode = (int)response.StatusCode;
        response.Dispose();
        throw new HandyApiException($"{path} failed: {statusCode}.");
    }

    private static void ThrowIfDeviceError(string path, JsonElement? error)
    {
        if (error is null || error.Value.ValueKind == JsonValueKind.Null) return;
        if (error.Value.ValueKind != JsonValueKind.Object)
            throw new HandyApiException($"{path}: invalid response shape.");
        throw DeviceError(path, error.Value);
    }

    private static HandyApiException DeviceError(string path, JsonElement error)
    {
        var code = error.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number) ? number : (int?)null;
        return new HandyApiException(code is null ? $"{path}: device error." : $"{path}: device error {code}.", code);
    }

    private static T? ParseResponse<T>(string operation, string body)
    {
        try { return JsonSerializer.Deserialize<T>(body); }
        catch (JsonException) { throw new HandyApiException($"{operation}: invalid JSON response."); }
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, string operation, HttpRequestMessage request, CancellationToken ct)
    {
        try { return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
        catch (HttpRequestException ex)
        {
            throw new HandyApiException(ex.StatusCode is { } status
                ? $"{operation}: HTTP transport failed ({(int)status})."
                : $"{operation}: HTTP transport failed.");
        }
        catch (IOException) { throw new HandyApiException($"{operation}: transport I/O failed."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TaskCanceledException($"{operation}: request was canceled by the transport.", null, ct);
        }
    }

    private static async Task<string> ReadBodyAsync(string operation, HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadAsStringAsync(ct); }
        catch (HttpRequestException) { throw new HandyApiException($"{operation}: response read failed."); }
        catch (IOException) { throw new HandyApiException($"{operation}: response I/O failed."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TaskCanceledException($"{operation}: response read was canceled by the transport.", null, ct);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _pendingRequestsCts.Cancel();
        _http.Dispose();
        _hostingHttp.Dispose();
        _pendingRequestsCts.Dispose();
    }

    private sealed class ServerTimeResponse
    {
        [JsonPropertyName("server_time")] public long ServerTime { get; set; }
    }

    private sealed class UploadResponse
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    private sealed class DeviceResponse<T> where T : class
    {
        [JsonPropertyName("result")] public T? Result { get; set; }
        [JsonPropertyName("error")] public System.Text.Json.JsonElement? Error { get; set; }
    }

    private sealed class ConnectedResult
    {
        [JsonPropertyName("connected")] public bool Connected { get; set; }
    }

    public sealed class DeviceInfo
    {
        [JsonPropertyName("fw_status")] public int? FirmwareStatus { get; set; }
        [JsonPropertyName("fw_version")] public string FirmwareVersion { get; set; } = "";
        [JsonPropertyName("hw_model_name")] public string ModelName { get; set; } = "";
        [JsonPropertyName("hw_model_no")] public int? ModelNumber { get; set; }
        [JsonPropertyName("hw_model_variant")] public int? ModelVariant { get; set; }
    }

    private sealed class OffsetResult
    {
        [JsonPropertyName("offset")] public int Offset { get; set; }
    }

    public sealed class SliderStroke
    {
        [JsonPropertyName("min")] public double Min { get; set; }
        [JsonPropertyName("max")] public double Max { get; set; }
        [JsonPropertyName("min_absolute")] public double MinAbsolute { get; set; }
        [JsonPropertyName("max_absolute")] public double MaxAbsolute { get; set; }
    }

    public sealed class DeviceCapabilities
    {
        [JsonPropertyName("slider")] public int SliderCount { get; set; }
    }

    public sealed class SliderSettings
    {
        [JsonPropertyName("x_limit_start")] public double PhysicalMinMm { get; set; }
        [JsonPropertyName("x_limit_stop")] public double PhysicalMaxMm { get; set; }
        [JsonPropertyName("x_min_speed")] public double MinSpeedMmPerSecond { get; set; }
        [JsonPropertyName("x_max_speed")] public double MaxSpeedMmPerSecond { get; set; }
        [JsonPropertyName("temp_high_trigger")] public double? TemperatureHighTrigger { get; set; }
        [JsonPropertyName("temp_hysteresis")] public double? TemperatureHysteresis { get; set; }
    }

    public sealed class SliderState
    {
        [JsonPropertyName("motor_temp")] public double MotorTemperature { get; set; }
    }
}
