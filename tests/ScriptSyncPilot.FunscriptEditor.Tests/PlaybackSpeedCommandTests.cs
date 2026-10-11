using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.FunscriptEditor;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class PlaybackSpeedCommandTests
{
    [TestMethod]
    public async Task SendPlaybackSpeedAsync_ReportsCurrentSessionFailureWithoutThrowing()
    {
        var messages = new List<string>();

        await MainForm.SendPlaybackSpeedAsync(
            static _ => Task.FromException(new IOException("pipe closed")),
            CancellationToken.None,
            static () => true,
            messages.Add);

        CollectionAssert.AreEqual(new[] { "Speed change failed: pipe closed" }, messages);
    }

    [TestMethod]
    public async Task SendPlaybackSpeedAsync_SuppressesFailureAfterSessionReplacement()
    {
        var messages = new List<string>();

        await MainForm.SendPlaybackSpeedAsync(
            static _ => Task.FromException(new IOException("old pipe closed")),
            CancellationToken.None,
            static () => false,
            messages.Add);

        Assert.AreEqual(0, messages.Count);
    }
}