using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class HandyApiTimeoutTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(100);
    private const string SyntheticSecret = "synthetic-handler-secret";

    [TestMethod]
    public async Task RestRequestTimeout_CancelsTransport_AndReturnsSafeTimeoutMessage()
    {
        using var handler = new BlockingHandler();
        using var client = CreateClient(handler, controlTimeout: ShortTimeout);

        var request = client.IsConnectedAsync(CancellationToken.None);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => request);

        Assert.IsTrue(handler.CancellationObserved.Task.IsCompleted);
        Assert.AreEqual("connected: request timed out.", error.Message);
        Assert.IsNull(error.InnerException, "Transport exception details must not be retained in the public timeout error.");
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task HostingUploadTimeout_CancelsTransport_AndReturnsSafeTimeoutMessage()
    {
        using var deviceHandler = new NeverUsedHandler();
        using var hostingHandler = new BlockingHandler();
        using var client = CreateClient(deviceHandler, hostingHandler, uploadTimeout: ShortTimeout);

        var request = client.UploadScriptCsvAsync("0,0", CancellationToken.None);
        await hostingHandler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => request);

        Assert.IsTrue(hostingHandler.CancellationObserved.Task.IsCompleted);
        Assert.AreEqual("script upload: request timed out.", error.Message);
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ResponseBodyTransferTimeout_CancelsContentRead()
    {
        using var content = new BlockingContent();
        using var handler = new ContentHandler(content);
        using var client = CreateClient(handler, controlTimeout: ShortTimeout);

        var request = client.GetInfoAsync(CancellationToken.None);
        await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => request);

        Assert.IsTrue(content.CancellationObserved.Task.IsCompleted);
        Assert.AreEqual("info: request timed out.", error.Message);
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CallerCancellation_RemainsCancellation_AndIsNotReportedAsTimeout()
    {
        using var handler = new BlockingHandler();
        using var client = CreateClient(handler, controlTimeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();

        var request = client.IsConnectedAsync(cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        var error = await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => request);

        Assert.IsTrue(handler.CancellationObserved.Task.IsCompleted);
        Assert.AreEqual("connected: request canceled.", error.Message);
        Assert.IsFalse(error.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(error.Message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    private static HandyApiClient CreateClient(HttpMessageHandler deviceHandler,
        HttpMessageHandler? hostingHandler = null, TimeSpan? controlTimeout = null, TimeSpan? uploadTimeout = null) =>
        new("test-key", deviceHandler, new Uri("https://example.test/api/"), hostingHandler,
            controlTimeout, scriptUploadTimeout: uploadTimeout);

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult();
                throw new TaskCanceledException(SyntheticSecret);
            }

            throw new InvalidOperationException("The blocking handler should only finish through cancellation.");
        }
    }

    private sealed class ContentHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    private sealed class BlockingContent : HttpContent
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult();
                throw new TaskCanceledException(SyntheticSecret);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class NeverUsedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new AssertFailedException("Unexpected device request.");
    }
}
