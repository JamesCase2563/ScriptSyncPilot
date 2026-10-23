using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScriptSyncPilot.HandySync;

internal sealed class ProtectedConnectionKey
{
    public const int CurrentVersion = 1;

    public Guid Id { get; }
    public string Label { get; }
    public int Version { get; }
    public string ProtectedValue { get; }

    [JsonConstructor]
    public ProtectedConnectionKey(Guid id, string label, int version, string protectedValue)
    {
        if (id == Guid.Empty)
            throw new JsonException("A protected connection-key entry requires a non-empty identifier.");
        if (string.IsNullOrWhiteSpace(label))
            throw new JsonException("A protected connection-key entry requires a label.");
        if (version != CurrentVersion)
            throw new JsonException("The protected connection-key entry version is missing or unsupported.");
        if (string.IsNullOrWhiteSpace(protectedValue))
            throw new JsonException("A protected connection-key entry requires a protected value.");

        try
        {
            Convert.FromBase64String(protectedValue);
        }
        catch (FormatException)
        {
            throw new JsonException("The protected connection-key value is not valid Base64.");
        }

        Id = id;
        Label = label;
        Version = version;
        ProtectedValue = protectedValue;
    }
}
