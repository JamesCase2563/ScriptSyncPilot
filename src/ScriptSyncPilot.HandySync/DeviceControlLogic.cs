namespace ScriptSyncPilot.HandySync;

internal enum DeviceTemperatureBand
{
    Normal,
    Warm,
    Hot
}

internal static class DeviceControlLogic
{
    public static double ToRelativeStroke(int percent) => Math.Clamp(percent, 0, 100) / 100.0;

    public static int FromRelativeStroke(double value) => Math.Clamp((int)Math.Round(value * 100), 0, 100);

    public static (int Minimum, int Maximum) NormalizeStrokeRange(int minimum, int maximum) =>
        minimum is < 0 or > 100 || maximum is < 0 or > 100 || minimum >= maximum
            ? (0, 100)
            : (minimum, maximum);

    public static bool ShouldApplyPolledValue(
        int capturedEditVersion,
        int currentEditVersion,
        bool writePending,
        bool writeInFlight,
        bool beingAdjusted) =>
        capturedEditVersion == currentEditVersion && !writePending && !writeInFlight && !beingAdjusted;

    public static DeviceTemperatureBand GetTemperatureBand(
        DeviceTemperatureBand current,
        double temperature,
        double trigger,
        double hysteresis)
    {
        var warmThreshold = trigger - 10;
        return current switch
        {
            DeviceTemperatureBand.Hot when temperature >= trigger - hysteresis => DeviceTemperatureBand.Hot,
            DeviceTemperatureBand.Hot when temperature >= warmThreshold - hysteresis => DeviceTemperatureBand.Warm,
            DeviceTemperatureBand.Warm when temperature >= warmThreshold - hysteresis && temperature < trigger => DeviceTemperatureBand.Warm,
            _ when temperature >= trigger => DeviceTemperatureBand.Hot,
            _ when temperature >= warmThreshold => DeviceTemperatureBand.Warm,
            _ => DeviceTemperatureBand.Normal
        };
    }
}
