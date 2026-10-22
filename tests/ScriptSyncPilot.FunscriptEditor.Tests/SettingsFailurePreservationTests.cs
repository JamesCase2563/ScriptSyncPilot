using System.Reflection;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class SettingsFailurePreservationTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PartialStagedWriteFailure_PreservesOriginalAndInMemoryEntries(bool existingFile)
    {
        WithDirectory(directory =>
        {
            var path = Path.Combine(directory, "settings.json");
            var settings = new AppSettings();
            settings.AddConnectionKey("First", "synthetic-first");
            settings.AddConnectionKey("Second", "synthetic-second");
            if (existingFile) settings.Save(path);
            var original = existingFile ? File.ReadAllBytes(path) : null;
            var entries = settings.ConnectionKeys.ToArray();
            settings.ConnectionKey = "synthetic-edited";
            settings.DeviceOffsetMs = 175;

            var error = Assert.ThrowsException<SettingsException>(() => settings.Save(path, (stream, json) =>
            {
                Assert.AreEqual(existingFile, File.Exists(path));
                if (existingFile) CollectionAssert.AreEqual(original!, File.ReadAllBytes(path));
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length / 2);
                throw new IOException("synthetic secret write details");
            }));

            Assert.IsFalse(error.ToString().Contains("synthetic", StringComparison.Ordinal));
            Assert.IsNull(error.InnerException);
            Assert.AreEqual(existingFile, File.Exists(path));
            if (existingFile) CollectionAssert.AreEqual(original!, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(entries, settings.ConnectionKeys.ToArray());
            Assert.AreEqual("synthetic-edited", settings.ConnectionKey);
            Assert.AreEqual(175, settings.DeviceOffsetMs);
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);

            settings.Save(path);
            var restored = AppSettings.Load(path);
            Assert.AreEqual("synthetic-edited", restored.ConnectionKey);
            Assert.AreEqual(175, restored.DeviceOffsetMs);
            Assert.AreEqual(entries[0].Id, restored.ConnectionKeys[0].Id);
            Assert.AreEqual(entries[1].ProtectedValue, restored.ConnectionKeys[1].ProtectedValue);
        });
    }

    [TestMethod]
    public void SuccessfulReplacement_LeavesOriginalVisibleUntilStagingFinishes()
    {
        WithDirectory(directory =>
        {
            var path = Path.Combine(directory, "settings.json");
            var settings = new AppSettings { ConnectionKey = "synthetic-old" };
            settings.Save(path);
            var original = File.ReadAllBytes(path);
            var originalEntry = settings.ConnectionKeys.Single();
            settings.ConnectionKey = "synthetic-new";
            settings.Save(path, (stream, json) =>
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length / 2);
                stream.Flush();
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                Assert.AreSame(originalEntry, settings.ConnectionKeys.Single());
                Assert.AreEqual(Path.GetFullPath(directory),
                    Path.GetDirectoryName(((FileStream)stream).Name));
                stream.Write(bytes, bytes.Length / 2, bytes.Length - bytes.Length / 2);
            });

            Assert.AreEqual("synthetic-new", AppSettings.Load(path).ConnectionKey);
            Assert.AreEqual(originalEntry.Id, settings.ConnectionKeys.Single().Id);
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
        });
    }

    [TestMethod]
    public void ProtectionFailure_DoesNotCreateStagingFileOrCommitEntries()
    {
        WithDirectory(directory =>
        {
            var path = Path.Combine(directory, "settings.json");
            new AppSettings { ConnectionKey = "synthetic-original" }.Save(path);
            var original = File.ReadAllBytes(path);
            var settings = new AppSettings(new UnavailableProtector()) { ConnectionKey = "synthetic-new" };
            Assert.ThrowsException<SettingsException>(() => settings.Save(path,
                (_, _) => Assert.Fail("Writing must not start when protection fails.")));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
            Assert.AreEqual(0, settings.ConnectionKeys.Count);
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
        });
    }

    [TestMethod]
    public void ReplacementDenied_PreservesOriginalAndAllowsRetry()
    {
        WithDirectory(directory =>
        {
            var path = Path.Combine(directory, "settings.json");
            var settings = new AppSettings { ConnectionKey = "synthetic-original" };
            settings.Save(path);
            var original = File.ReadAllBytes(path);
            var entry = settings.ConnectionKeys.Single();
            settings.ConnectionKey = "synthetic-replacement";
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.ThrowsException<SettingsException>(() => settings.Save(path));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                Assert.AreSame(entry, settings.ConnectionKeys.Single());
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
                Assert.ThrowsException<SettingsException>(() => AppSettings.Load(path, new UnavailableProtector()));
            }
            settings.Save(path);
            Assert.AreEqual("synthetic-replacement", AppSettings.Load(path).ConnectionKey);
        });
    }

    [TestMethod]
    public void UnavailableKeyOrCorruptedSettings_NeverModifySourceFile()
    {
        WithDirectory(directory =>
        {
            var path = Path.Combine(directory, "settings.json");
            new AppSettings { ConnectionKey = "synthetic-key" }.Save(path);
            var original = File.ReadAllBytes(path);
            var error = Assert.ThrowsException<SettingsException>(
                () => AppSettings.Load(path, new UnavailableProtector()));
            Assert.IsFalse(error.ToString().Contains("synthetic", StringComparison.Ordinal));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));

            File.WriteAllText(path, """{"ConnectionKeys":[{"Id":"invalid","ProtectedValue":"synthetic-secret"}]}""");
            var corrupt = File.ReadAllBytes(path);
            var invalid = Assert.ThrowsException<SettingsException>(() => AppSettings.Load(path));
            Assert.IsFalse(invalid.ToString().Contains("synthetic-secret", StringComparison.Ordinal));
            CollectionAssert.AreEqual(corrupt, File.ReadAllBytes(path));
        });
    }

    [TestMethod]
    public void UnreadableFile_IsExplicitAndNotReplacedWithDefaults()
    {
        WithDirectory(directory =>
        {
            var path = Path.Combine(directory, "settings.json");
            new AppSettings().Save(path);
            var original = File.ReadAllBytes(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.ThrowsException<SettingsException>(() => AppSettings.Load(path));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        });
    }

    [STATestMethod]
    public void FormSaveFailure_CancelsClosingAndAllowsRetryWithoutStartingShutdown()
    {
        var errors = new List<string>();
        var saves = 0;
        using var form = new ScriptSyncPilot.HandySync.MainForm(new AppSettings(),
            _ => throw new AssertFailedException("No client expected."),
            _ =>
            {
                saves++;
                throw new SettingsException("Synthetic safe save failure.");
            }, errors.Add);
        _ = form.Handle;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var args = new FormClosingEventArgs(CloseReason.UserClosing, false);
            typeof(Form).GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, [args]);

            Assert.IsTrue(args.Cancel);
            Assert.IsFalse(form.IsDisposed);
            Assert.AreEqual(attempt, saves);
            Assert.AreEqual(attempt, errors.Count);
            Assert.IsFalse(GetField<bool>(form, "_isClosing"));
            Assert.IsFalse(GetField<CancellationTokenSource>(form, "_lifetimeCts").IsCancellationRequested);
        }
    }

    private static T GetField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void WithDirectory(Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ScriptSyncPilot-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { test(directory); }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private sealed class UnavailableProtector : IConnectionKeyProtector
    {
        public string Protect(string connectionKey) =>
            throw new CryptographicException("synthetic unavailable protection details");
        public string Unprotect(string protectedValue) =>
            throw new CryptographicException("synthetic unavailable decryption details");
    }
}
