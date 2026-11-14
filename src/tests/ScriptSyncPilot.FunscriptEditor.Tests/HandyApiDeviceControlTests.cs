using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class HandyApiDeviceControlTests
{
    [TestMethod]
    public async Task DeviceControlEndpoints_UseExpectedRequestsAndParseResults()
    {
        using var handler = new RecordingHandler(
            "{\"result\":{\"fw_status\":0,\"fw_version\":\"4.3.0\",\"hw_model_name\":\"H01\",\"hw_model_no\":0,\"hw_model_variant\":0}}",
            "{\"result\":{\"offset\":-250}}",
            "{\"result\":\"ok\"}",
            "{\"result\":{\"min\":0.25,\"max\":0.75,\"min_absolute\":30.165,\"max_absolute\":80.495}}",
            "{\"result\":{\"slider\":1}}",
            "{\"result\":{\"x_limit_start\":0,\"x_limit_stop\":110.66,\"x_min_speed\":32,\"x_max_speed\":400,\"temp_high_trigger\":80,\"temp_hysteresis\":10}}",
            "{\"result\":{\"motor_temp\":29.3}}",
            ""
        );
        using var client = new HandyApiClient("test-connection-key", handler, new Uri("https://example.test/api/"));

        var info = await client.GetInfoAsync(CancellationToken.None);
        Assert.AreEqual(0, info.FirmwareStatus);
        Assert.AreEqual("4.3.0", info.FirmwareVersion);
        Assert.AreEqual("H01", info.ModelName);
        Assert.AreEqual(0, info.ModelNumber);
        Assert.AreEqual(0, info.ModelVariant);
        Assert.AreEqual("info", handler.Requests[0].Path);
        Assert.AreEqual("GET", handler.Requests[0].Method);
        Assert.AreEqual("LGtuDB6Ikqu4s65pK7eoshlqkDV_ziiG", handler.Requests[0].ApiKey);
        Assert.AreEqual("test-connection-key", handler.Requests[0].ConnectionKey);

        Assert.AreEqual(-250, await client.GetOffsetAsync(CancellationToken.None));
        Assert.AreEqual("hstp/offset", handler.Requests[1].Path);
        Assert.AreEqual("GET", handler.Requests[1].Method);

        await client.SetOffsetAsync(250, CancellationToken.None);
        Assert.AreEqual("hstp/offset", handler.Requests[2].Path);
        Assert.AreEqual("PUT", handler.Requests[2].Method);
        using (var offsetBody = JsonDocument.Parse(handler.Requests[2].Body!))
            Assert.AreEqual(250, offsetBody.RootElement.GetProperty("offset").GetInt32());

        var stroke = await client.SetStrokeAsync(0.25, 0.75, CancellationToken.None);
        Assert.AreEqual("slider/stroke", handler.Requests[3].Path);
        Assert.AreEqual("PUT", handler.Requests[3].Method);
        Assert.AreEqual(0.25, stroke.Min);
        Assert.AreEqual(0.75, stroke.Max);
        Assert.AreEqual(30.165, stroke.MinAbsolute);
        Assert.AreEqual(80.495, stroke.MaxAbsolute);
        using (var strokeBody = JsonDocument.Parse(handler.Requests[3].Body!))
        {
            Assert.AreEqual(0.25, strokeBody.RootElement.GetProperty("min").GetDouble());
            Assert.AreEqual(0.75, strokeBody.RootElement.GetProperty("max").GetDouble());
        }

        Assert.AreEqual(1, (await client.GetCapabilitiesAsync(CancellationToken.None)).SliderCount);
        Assert.AreEqual("capabilities", handler.Requests[4].Path);
        var settings = await client.GetSliderSettingsAsync(CancellationToken.None);
        Assert.AreEqual("settings/slider", handler.Requests[5].Path);
        Assert.AreEqual(80, settings.TemperatureHighTrigger);
        Assert.AreEqual(29.3, (await client.GetSliderStateAsync(CancellationToken.None)).MotorTemperature, 0.001);
        Assert.AreEqual("slider/state", handler.Requests[6].Path);
        await client.SetModeAsync(1, CancellationToken.None);
        Assert.AreEqual("mode2", handler.Requests[7].Path);
    }

    [TestMethod]
    public async Task SuccessfulHttpResponseWithDeviceError_ThrowsWithoutLeakingDetails()
    {
        using var handler = new RecordingHandler(
            "{\"error\":{\"code\":1002,\"name\":\"DeviceTimeout\",\"message\":\"connection secret should not leak\"}}"
        );
        using var client = new HandyApiClient("sensitive-connection-key", handler, new Uri("https://example.test/api/"));

        var exception = await Assert.ThrowsExceptionAsync<HandyApiException>(
            () => client.SetOffsetAsync(100, CancellationToken.None));

        Assert.AreEqual(1002, exception.Code);
        Assert.IsNull(exception.Name);
        Assert.IsFalse(exception.Message.Contains("connection secret", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(exception.Message.Contains("sensitive-connection-key", StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("[]")]
    [DataRow("42")]
    [DataRow("true")]
    [DataRow("\"synthetic-secret\"")]
    public async Task MalformedError_WithValidResult_IsRejected(string error)
    {
        var body = "{\"result\":{\"min\":0.25,\"max\":0.75,\"connected\":true},\"error\":" + error + "}";
        using var handler = new RecordingHandler(body, body, body, body);
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        Func<Task>[] calls =
        [
            () => client.SetStrokeAsync(0.25, 0.75, CancellationToken.None),
            () => client.GetStrokeAsync(CancellationToken.None),
            () => client.SetOffsetAsync(100, CancellationToken.None),
            () => client.IsConnectedAsync(CancellationToken.None)
        ];
        foreach (var call in calls)
        {
            var exception = await Assert.ThrowsExceptionAsync<HandyApiException>(call);
            StringAssert.Contains(exception.Message, "invalid response shape");
            Assert.IsFalse(exception.ToString().Contains("synthetic-secret", StringComparison.Ordinal));
            Assert.IsNull(exception.InnerException);
        }
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(",\"error\":null")]
    public async Task MissingOrNullError_WithValidResult_IsAccepted(string errorProperty)
    {
        var body = "{\"result\":{\"min\":0.25,\"max\":0.75,\"connected\":true}" + errorProperty + "}";
        using var handler = new RecordingHandler(body, body, body, body);
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        Assert.AreEqual(0.25, (await client.SetStrokeAsync(0.25, 0.75, CancellationToken.None)).Min);
        Assert.AreEqual(0.75, (await client.GetStrokeAsync(CancellationToken.None)).Max);
        await client.SetOffsetAsync(100, CancellationToken.None);
        Assert.IsTrue(await client.IsConnectedAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ObjectError_WithValidResult_TakesPrecedence()
    {
        const string body = """{"result":{"min":0.25,"max":0.75},"error":{"code":1002,"message":"synthetic-secret"}}""";
        using var handler = new RecordingHandler(body);
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        var exception = await Assert.ThrowsExceptionAsync<HandyApiException>(
            () => client.SetStrokeAsync(0.25, 0.75, CancellationToken.None));
        Assert.AreEqual(1002, exception.Code);
        Assert.IsFalse(exception.ToString().Contains("synthetic-secret", StringComparison.Ordinal));
    }

    private sealed class RecordingHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);
        public List<RequestRecord> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestRecord(
                request.Method.Method,
                request.RequestUri!.AbsolutePath.TrimStart('/').Replace("api/", "", StringComparison.Ordinal),
                request.Headers.GetValues("X-Api-Key").Single(),
                request.Headers.GetValues("X-Connection-Key").Single(),
                body));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record RequestRecord(string Method, string Path, string ApiKey, string ConnectionKey, string? Body);
}
