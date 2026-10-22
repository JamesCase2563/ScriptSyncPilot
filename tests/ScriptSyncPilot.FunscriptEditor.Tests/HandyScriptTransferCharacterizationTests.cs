using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class HandyScriptTransferCharacterizationTests
{
    private const string ScriptUrl = "https://example.test/script.csv?token=synthetic-token";
    private const string ConnectionKey = "synthetic-connection-key";

    [TestMethod]
    public async Task UploadThenSetup_UsesSeparateTransportsAndExpectedPayloads()
    {
        const string csv = "\n0,0\n500,100\n";
        using var hosting = new StubHandler(async (request, ct) =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("https://www.handyfeeling.com/api/hosting/v2/upload", request.RequestUri!.AbsoluteUri);
            Assert.IsFalse(request.Headers.Contains("X-Connection-Key"));
            Assert.IsFalse(request.Headers.Contains("X-Api-Key"));
            Assert.IsInstanceOfType(request.Content, typeof(MultipartFormDataContent));
            var file = ((MultipartFormDataContent)request.Content!).Single();
            Assert.AreEqual("file", file.Headers.ContentDisposition!.Name!.Trim('"'));
            Assert.AreEqual("script.csv", file.Headers.ContentDisposition.FileName!.Trim('"'));
            Assert.AreEqual("text/csv", file.Headers.ContentType!.MediaType);
            Assert.AreEqual(csv, await file.ReadAsStringAsync(ct));
            return JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new { url = ScriptUrl }));
        });
        using var device = new StubHandler(async (request, ct) =>
        {
            Assert.AreEqual(HttpMethod.Put, request.Method);
            Assert.AreEqual("/api/hssp/setup", request.RequestUri!.AbsolutePath);
            Assert.AreEqual(ConnectionKey, request.Headers.GetValues("X-Connection-Key").Single());
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.AreEqual(ScriptUrl, payload.RootElement.GetProperty("url").GetString());
            return JsonResponse(HttpStatusCode.OK, """{"result":"ok"}""");
        });
        using var client = CreateClient(device, hosting);

        var url = await client.UploadScriptCsvAsync(csv, CancellationToken.None);
        Assert.AreEqual(ScriptUrl, url);
        await client.HsspSetupAsync(url, CancellationToken.None);
        Assert.AreEqual(1, hosting.RequestCount);
        Assert.AreEqual(1, device.RequestCount);
    }

    [TestMethod]
    public async Task FailedUpload_CurrentlyIncludesStatusAndRawBody()
    {
        var body = $"synthetic upload failure {ScriptUrl}";
        using var hosting = RespondWith(HttpStatusCode.BadGateway, body);
        using var device = RespondWith(HttpStatusCode.OK, "{}");
        using var client = CreateClient(device, hosting);

        var error = await Assert.ThrowsExceptionAsync<HandyApiException>(
            () => client.UploadScriptCsvAsync("0,0", CancellationToken.None));

        StringAssert.Contains(error.Message, "script upload failed: 502");
        StringAssert.Contains(error.Message, body);
        Assert.AreEqual(0, device.RequestCount);
    }

    [TestMethod]
    public async Task UploadWithoutUrl_CurrentlyIncludesRawBody()
    {
        var body = JsonSerializer.Serialize(new { detail = ScriptUrl });
        using var hosting = RespondWith(HttpStatusCode.OK, body);
        using var device = RespondWith(HttpStatusCode.OK, "{}");
        using var client = CreateClient(device, hosting);

        var error = await Assert.ThrowsExceptionAsync<HandyApiException>(
            () => client.UploadScriptCsvAsync("0,0", CancellationToken.None));

        StringAssert.Contains(error.Message, "no url in response");
        StringAssert.Contains(error.Message, body);
    }

    [TestMethod]
    public async Task MalformedUploadResponse_ThrowsJsonException()
    {
        using var hosting = RespondWith(HttpStatusCode.OK, "{");
        using var device = RespondWith(HttpStatusCode.OK, "{}");
        using var client = CreateClient(device, hosting);

        await Assert.ThrowsExceptionAsync<JsonException>(
            () => client.UploadScriptCsvAsync("0,0", CancellationToken.None));
    }

    [TestMethod]
    public async Task SetupHttpFailure_PreservesStatusWithoutResponseBodyOrCredentials()
    {
        using var hosting = RespondWith(HttpStatusCode.OK, "{}");
        using var device = RespondWith(HttpStatusCode.BadGateway, $"{ScriptUrl} {ConnectionKey}");
        using var client = CreateClient(device, hosting);

        var error = await Assert.ThrowsExceptionAsync<HandyApiException>(
            () => client.HsspSetupAsync(ScriptUrl, CancellationToken.None));

        StringAssert.Contains(error.Message, "hssp/setup failed: 502");
        Assert.IsFalse(error.Message.Contains(ScriptUrl, StringComparison.Ordinal));
        Assert.IsFalse(error.Message.Contains(ConnectionKey, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SetupDeviceError_PreservesCodeAndNameWithoutMessageDetails()
    {
        var body = JsonSerializer.Serialize(new
        {
            error = new { code = 1002, name = "DeviceTimeout", message = $"{ScriptUrl} {ConnectionKey}" }
        });
        using var hosting = RespondWith(HttpStatusCode.OK, "{}");
        using var device = RespondWith(HttpStatusCode.OK, body);
        using var client = CreateClient(device, hosting);

        var error = await Assert.ThrowsExceptionAsync<HandyApiException>(
            () => client.HsspSetupAsync(ScriptUrl, CancellationToken.None));

        Assert.AreEqual(1002, error.Code);
        Assert.AreEqual("DeviceTimeout", error.Name);
        StringAssert.Contains(error.Message, "hssp/setup");
        Assert.IsFalse(error.Message.Contains(ScriptUrl, StringComparison.Ordinal));
        Assert.IsFalse(error.Message.Contains(ConnectionKey, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task UploadCancellation_IsPassedToHostingTransport()
    {
        using var cancellation = new CancellationTokenSource();
        using var hosting = new StubHandler(async (_, ct) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            throw new AssertFailedException("Canceled upload should not complete.");
        });
        using var device = RespondWith(HttpStatusCode.OK, "{}");
        using var client = CreateClient(device, hosting);

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(
            () => client.UploadScriptCsvAsync("0,0", cancellation.Token));
        Assert.AreEqual(1, hosting.RequestCount);
        Assert.AreEqual(0, device.RequestCount);
    }

    private static HandyApiClient CreateClient(HttpMessageHandler device, HttpMessageHandler hosting) =>
        new(ConnectionKey, device, new Uri("https://example.test/api/"), hosting);

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static StubHandler RespondWith(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(JsonResponse(status, body)));

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return respond(request, cancellationToken);
        }
    }
}
