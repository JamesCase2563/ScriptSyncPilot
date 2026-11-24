namespace ScriptSyncPilot.FunscriptEditor;

/// <summary>Jet-style rainbow palette used for script-speed coloring.</summary>
public static class SegmentSpeedPalette
{
    private static readonly Color[] Stops =
    [
        Color.FromArgb(0, 0, 128),
        Color.FromArgb(0, 0, 255),
        Color.FromArgb(0, 127, 255),
        Color.FromArgb(0, 255, 255),
        Color.FromArgb(127, 255, 127),
        Color.FromArgb(255, 255, 0),
        Color.FromArgb(255, 127, 0),
        Color.FromArgb(255, 0, 0),
        Color.FromArgb(128, 0, 0),
    ];

    public static Color GetColor(double normalizedPosition)
    {
        var position = Math.Clamp(normalizedPosition, 0, 1) * (Stops.Length - 1);
        var lowerIndex = Math.Min((int)position, Stops.Length - 2);
        var fraction = position - lowerIndex;
        var lower = Stops[lowerIndex];
        var upper = Stops[lowerIndex + 1];
        return Color.FromArgb(
            Interpolate(lower.A, upper.A, fraction),
            Interpolate(lower.R, upper.R, fraction),
            Interpolate(lower.G, upper.G, fraction),
            Interpolate(lower.B, upper.B, fraction));
    }

    public static Color GetHighContrastColor() => SystemColors.Highlight;

    private static int Interpolate(int start, int end, double amount) =>
        (int)Math.Round(start + (end - start) * amount);
}
