using System.Text.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using ScriptSyncPilot.Common;

namespace ScriptSyncPilot.HandySync;

public sealed class SettingsException(string message) : Exception(message);

/// <summary>Small persisted settings blob, kept separate from any runtime session state.</summary>
public sealed class AppSettings
{
    private readonly IConnectionKeyProtector _protector;
    private List<ProtectedConnectionKey> _connectionKeys = [];

    public AppSettings() : this(new DpapiConnectionKeyProtector()) { }

    internal AppSettings(IConnectionKeyProtector protector) => _protector = protector;

    [JsonIgnore]
    public string ConnectionKey { get; set; } = "";
    internal bool RequiresKeyReentry { get; private set; }
    internal IReadOnlyList<ProtectedConnectionKey> ConnectionKeys => _connectionKeys.AsReadOnly();

    internal Guid AddConnectionKey(string label, string connectionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionKey);

        ProtectedConnectionKey entry;
        try
        {
            entry = new ProtectedConnectionKey(Guid.NewGuid(), label, ProtectedConnectionKey.CurrentVersion,
                _protector.Protect(connectionKey));
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException
            or ArgumentException or PlatformNotSupportedException)
        {
            throw new SettingsException("The connection key could not be protected. No entry was added.");
        }

        _connectionKeys.Add(entry);
        if (_connectionKeys.Count == 1)
        {
            ConnectionKey = connectionKey;
            RequiresKeyReentry = false;
        }
        return entry.Id;
    }

    internal string? GetFirstConnectionKey() =>
        _connectionKeys.Count == 0 ? null : UnprotectConnectionKey(_connectionKeys[0]);

    internal void DeleteConnectionKey(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("A non-empty connection-key identifier is required.", nameof(id));
        var index = _connectionKeys.FindIndex(entry => entry.Id == id);
        if (index < 0)
            throw new KeyNotFoundException("The connection-key entry was not found.");

        var nextKey = index == 0 && _connectionKeys.Count > 1
            ? UnprotectConnectionKey(_connectionKeys[1])
            : "";
        _connectionKeys.RemoveAt(index);
        if (index == 0)
            ConnectionKey = nextKey;
    }

    private string UnprotectConnectionKey(ProtectedConnectionKey entry)
    {
        try
        {
            var key = _protector.Unprotect(entry.ProtectedValue);
            if (string.IsNullOrWhiteSpace(key))
                throw new CryptographicException();
            return key;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException
            or ArgumentException or PlatformNotSupportedException)
        {
            throw new SettingsException(
                "A saved connection key could not be decrypted. Check the Windows account/profile; no entry was changed.");
        }
    }

    public string MpvIpcPipe { get; set; } = @"\\.\pipe\vrheadtrack";
    public int DeviceOffsetMs { get; set; }
    public int StrokeMinPercent { get; set; }
    public int StrokeMaxPercent { get; set; } = 100;
    public string LastScriptPath { get; set; } = "";

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScriptSyncPilot.HandySync", "settings.json");

    public static AppSettings Load() => Load(FilePath);

    internal static AppSettings Load(string path, IConnectionKeyProtector? protector = null)
    {
        try
        {
            return Deserialize(File.ReadAllText(path), protector: protector);
        }
        catch (FileNotFoundException) { return new AppSettings(protector ?? new DpapiConnectionKeyProtector()); }
        catch (DirectoryNotFoundException) { return new AppSettings(protector ?? new DpapiConnectionKeyProtector()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SettingsException("Settings could not be read. The existing file has not been changed.");
        }
    }

    internal static AppSettings Deserialize(string json, Action<string>? log = null,
        IConnectionKeyProtector? protector = null)
    {
        LogDiscardedLegacyOffset(json, log ?? AppLog.Write);
        try
        {
            var stored = JsonSerializer.Deserialize<StoredSettings>(json) ?? new StoredSettings();
            if (stored.MpvIpcPipe is null || stored.LastScriptPath is null)
                throw new JsonException("Settings text fields must not be null.");
            var keys = stored.ConnectionKeys
                ?? throw new JsonException("Connection keys must be an array.");
            var settings = new AppSettings(protector ?? new DpapiConnectionKeyProtector())
            {
                MpvIpcPipe = stored.MpvIpcPipe,
                DeviceOffsetMs = stored.DeviceOffsetMs,
                StrokeMinPercent = stored.StrokeMinPercent,
                StrokeMaxPercent = stored.StrokeMaxPercent,
                LastScriptPath = stored.LastScriptPath
            };
            var ids = new HashSet<Guid>();
            foreach (var entry in keys)
            {
                if (entry is null || !ids.Add(entry.Id))
                    throw new JsonException("Connection-key entries require unique identifiers.");
                var key = settings.UnprotectConnectionKey(entry);
                if (ids.Count == 1) settings.ConnectionKey = key;
            }
            settings._connectionKeys = keys;
            using var document = JsonDocument.Parse(json);
            settings.RequiresKeyReentry = keys.Count == 0
                && document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("ConnectionKey", out _);
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException
            or ArgumentException or PlatformNotSupportedException)
        {
            throw new SettingsException(
                "Settings or saved connection keys could not be loaded. Check the file and Windows account/profile before retrying; the file has not been changed.");
        }
    }

    internal string Serialize()
    {
        var json = Serialize(out var keys);
        _connectionKeys = keys;
        return json;
    }

    private string Serialize(out List<ProtectedConnectionKey> keys)
    {
        try
        {
            keys = new List<ProtectedConnectionKey>(_connectionKeys);
            if (!string.IsNullOrWhiteSpace(ConnectionKey))
            {
                var first = keys.FirstOrDefault();
                var entry = new ProtectedConnectionKey(first?.Id ?? Guid.NewGuid(),
                    first?.Label ?? "Default", ProtectedConnectionKey.CurrentVersion,
                    _protector.Protect(ConnectionKey));
                if (keys.Count == 0) keys.Add(entry);
                else keys[0] = entry;
            }
            else if (keys.Count > 0 || RequiresKeyReentry)
            {
                throw new SettingsException("The saved connection key cannot be replaced with an empty key.");
            }

            var stored = new StoredSettings
            {
                ConnectionKeys = keys,
                MpvIpcPipe = MpvIpcPipe,
                DeviceOffsetMs = DeviceOffsetMs,
                StrokeMinPercent = StrokeMinPercent,
                StrokeMaxPercent = StrokeMaxPercent,
                LastScriptPath = LastScriptPath
            };
            var json = JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true });
            return json;
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException
            or ArgumentException or PlatformNotSupportedException)
        {
            throw new SettingsException("The connection key could not be protected. Settings have not been saved.");
        }
    }

    private sealed class StoredSettings
    {
        public List<ProtectedConnectionKey> ConnectionKeys { get; set; } = [];
        public string MpvIpcPipe { get; set; } = @"\\.\pipe\vrheadtrack";
        public int DeviceOffsetMs { get; set; }
        public int StrokeMinPercent { get; set; }
        public int StrokeMaxPercent { get; set; } = 100;
        public string LastScriptPath { get; set; } = "";
    }

    private static void LogDiscardedLegacyOffset(string json, Action<string> log)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("OffsetMs", out var legacyOffset)
                && legacyOffset.ValueKind == JsonValueKind.Number
                && legacyOffset.TryGetInt32(out var value) && value != 0)
                log("Discarded legacy local OffsetMs setting; the device-side HSTP offset is independent.");
        }
        catch (JsonException)
        {
        }
    }

    public void Save() => Save(FilePath);

    internal void Save(string path, Action<Stream, string>? writeStagedContent = null)
    {
        var json = Serialize(out var keys);
        string? temporaryPath = null;
        var ownsTemporaryFile = false;
        try
        {
            var destination = Path.GetFullPath(path);
            var dir = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(dir);
            temporaryPath = Path.Combine(dir, $".settings-{Guid.NewGuid():N}.tmp");
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporaryFile = true;
                if (writeStagedContent is not null)
                    writeStagedContent(stream, json);
                else
                {
                    using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
                    writer.Write(json);
                    writer.Flush();
                }
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(destination))
                File.Replace(temporaryPath, destination, null);
            else
                File.Move(temporaryPath, destination);
            ownsTemporaryFile = false;
            _connectionKeys = keys;
            RequiresKeyReentry = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            throw new SettingsException("Settings could not be written. Check file access and available disk space before retrying.");
        }
        finally
        {
            if (ownsTemporaryFile)
            {
                try { File.Delete(temporaryPath!); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    AppLog.Write("Settings temporary-file cleanup failed. A protected temporary settings file may remain in the settings directory.");
                }
            }
        }
    }
}
