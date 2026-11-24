namespace ScriptSyncPilot.Common;

/// <summary>Platform-neutral RGB color value, independent of any UI toolkit.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B, byte A = byte.MaxValue);

/// <summary>Jet-style rainbow palette used for script-speed coloring.</summary>
public static class SegmentSpeedPalette
{
    private static readonly RgbColor[] Stops =
    [
        new(0, 0, 128),
        new(0, 0, 255),
        new(0, 127, 255),
        new(0, 255, 255),
        new(127, 255, 127),
        new(255, 255, 0),
        new(255, 127, 0),
        new(255, 0, 0),
        new(128, 0, 0),
    ];

    public static RgbColor GetColor(double normalizedPosition)
    {
        var position = Math.Clamp(normalizedPosition, 0, 1) * (Stops.Length - 1);
        var lowerIndex = Math.Min((int)position, Stops.Length - 2);
        var fraction = position - lowerIndex;
        var lower = Stops[lowerIndex];
        var upper = Stops[lowerIndex + 1];
        return new RgbColor(
            Interpolate(lower.R, upper.R, fraction),
            Interpolate(lower.G, upper.G, fraction),
            Interpolate(lower.B, upper.B, fraction),
            Interpolate(lower.A, upper.A, fraction));
    }

    private static byte Interpolate(byte start, byte end, double amount) =>
        (byte)Math.Clamp(Math.Round(start + (end - start) * amount), byte.MinValue, byte.MaxValue);
}
