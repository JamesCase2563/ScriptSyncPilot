using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScriptSyncPilot.HandySync;

namespace ScriptSyncPilot.FunscriptEditor.Tests;

[TestClass]
public sealed class DeviceSliderControlTests
{
    [TestMethod]
    public void FirmwareGateRequiresVersionFourAndNotUpdateRequired()
    {
        Assert.IsTrue(ScriptSyncPilot.HandySync.MainForm.IsDeviceControlsFirmwareSupported("4.3.0+dcee7e58", 0));
        Assert.IsTrue(ScriptSyncPilot.HandySync.MainForm.IsDeviceControlsFirmwareSupported("4.3.0", 1));
        Assert.IsFalse(ScriptSyncPilot.HandySync.MainForm.IsDeviceControlsFirmwareSupported("3.9.0", 0));
        Assert.IsFalse(ScriptSyncPilot.HandySync.MainForm.IsDeviceControlsFirmwareSupported("4.3.0", 2));
        Assert.IsFalse(ScriptSyncPilot.HandySync.MainForm.IsDeviceControlsFirmwareSupported("unknown", 0));
        Assert.IsFalse(ScriptSyncPilot.HandySync.MainForm.IsDeviceControlsFirmwareSupported("4.3.0", null));
    }

    [TestMethod]
    public void PercentConversionRoundTripsAndUsesAcceptedNormalizedValues()
    {
        for (var percent = 0; percent <= 100; percent++)
            Assert.AreEqual(percent, DeviceControlLogic.FromRelativeStroke(DeviceControlLogic.ToRelativeStroke(percent)));

        Assert.AreEqual(21, DeviceControlLogic.FromRelativeStroke(0.206));
        Assert.AreEqual(79, DeviceControlLogic.FromRelativeStroke(0.794));
        var normalizedPercent = DeviceControlLogic.FromRelativeStroke(0.206);
        for (var iteration = 0; iteration < 10; iteration++)
            normalizedPercent = DeviceControlLogic.FromRelativeStroke(DeviceControlLogic.ToRelativeStroke(normalizedPercent));
        Assert.AreEqual(21, normalizedPercent);
    }

    [TestMethod]
    public void InvalidPersistedStrokeRangesFallBackToFullRange()
    {
        Assert.AreEqual((0, 100), DeviceControlLogic.NormalizeStrokeRange(-1, 80));
        Assert.AreEqual((0, 100), DeviceControlLogic.NormalizeStrokeRange(20, 101));
        Assert.AreEqual((0, 100), DeviceControlLogic.NormalizeStrokeRange(80, 20));
        Assert.AreEqual((0, 100), DeviceControlLogic.NormalizeStrokeRange(50, 50));
        Assert.AreEqual((20, 80), DeviceControlLogic.NormalizeStrokeRange(20, 80));
    }

    [TestMethod]
    public void PollAcceptanceRejectsRacedOrLocallyOwnedValues()
    {
        Assert.IsTrue(DeviceControlLogic.ShouldApplyPolledValue(4, 4, false, false, false));
        Assert.IsFalse(DeviceControlLogic.ShouldApplyPolledValue(4, 5, false, false, false));
        Assert.IsFalse(DeviceControlLogic.ShouldApplyPolledValue(4, 4, true, false, false));
        Assert.IsFalse(DeviceControlLogic.ShouldApplyPolledValue(4, 4, false, true, false));
        Assert.IsFalse(DeviceControlLogic.ShouldApplyPolledValue(4, 4, false, false, true));
    }

    [TestMethod]
    public void TemperatureBandsUseThresholdsAndClearWithHysteresis()
    {
        var band = DeviceTemperatureBand.Normal;
        band = DeviceControlLogic.GetTemperatureBand(band, 69, 80, 10);
        Assert.AreEqual(DeviceTemperatureBand.Normal, band);
        band = DeviceControlLogic.GetTemperatureBand(band, 70, 80, 10);
        Assert.AreEqual(DeviceTemperatureBand.Warm, band);
        band = DeviceControlLogic.GetTemperatureBand(band, 80, 80, 10);
        Assert.AreEqual(DeviceTemperatureBand.Hot, band);
        band = DeviceControlLogic.GetTemperatureBand(band, 70, 80, 10);
        Assert.AreEqual(DeviceTemperatureBand.Hot, band);
        band = DeviceControlLogic.GetTemperatureBand(band, 69, 80, 10);
        Assert.AreEqual(DeviceTemperatureBand.Warm, band);
        band = DeviceControlLogic.GetTemperatureBand(band, 60, 80, 10);
        Assert.AreEqual(DeviceTemperatureBand.Warm, band);
        band = DeviceControlLogic.GetTemperatureBand(band, 59, 80, 10);
        Assert.AreEqual(DeviceTemperatureBand.Normal, band);
    }

    [STATestMethod]
    public void SingleThumbAcceptsSignedValueAndDoesNotClampAgainstSecondThumb()
    {
        using var slider = new DeviceSliderControl { Minimum = -1000, Maximum = 1000 };

        slider.SetValues(350, 0);

        Assert.AreEqual(350, slider.LowerValue);
        Assert.AreEqual(350, slider.UpperValue);
    }

    [STATestMethod]
    public void SingleThumbIsRenderedAtItsSelectedHorizontalValue()
    {
        using var slider = new DeviceSliderControl { Minimum = -1000, Maximum = 1000, Size = new Size(220, 40) };
        slider.SetValues(500, 0);
        var valueToPoint = typeof(DeviceSliderControl).GetMethod("ValueToPoint", PrivateInstance)!;
        var thumb = (PointF)valueToPoint.Invoke(slider, [500])!;
        using var bitmap = new Bitmap(slider.Width, slider.Height);

        slider.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));

        Assert.AreEqual(Color.WhiteSmoke.ToArgb(), bitmap.GetPixel((int)Math.Round(thumb.X), (int)Math.Round(thumb.Y)).ToArgb());
    }

    [STATestMethod]
    public void RangeThumbsStayOrderedAndKeyboardMovesOnlyActiveThumb()
    {
        using var slider = new DeviceSliderControl { IsRange = true, Orientation = Orientation.Vertical };
        slider.SetValues(20, 80);
        var onKeyDown = typeof(DeviceSliderControl).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!;

        onKeyDown.Invoke(slider, [new KeyEventArgs(Keys.Up)]);
        Assert.AreEqual(21, slider.LowerValue);
        Assert.AreEqual(80, slider.UpperValue);

        onKeyDown.Invoke(slider, [new KeyEventArgs(Keys.Space)]);
        onKeyDown.Invoke(slider, [new KeyEventArgs(Keys.Up)]);
        Assert.AreEqual(21, slider.LowerValue);
        Assert.AreEqual(81, slider.UpperValue);

        slider.SetValues(80, 80);
        onKeyDown.Invoke(slider, [new KeyEventArgs(Keys.Up)]);
        Assert.AreEqual(80, slider.LowerValue);
        Assert.AreEqual(81, slider.UpperValue);
    }

    [STATestMethod]
    public void ArrowKeyPreprocessingRoutesAdjustmentKeyToSlider()
    {
        using var form = new Form();
        using var slider = new DeviceSliderControl { IsRange = true, Orientation = Orientation.Vertical };
        slider.SetValues(20, 80);
        form.Controls.Add(slider);
        form.Show();
        slider.Focus();
        var isInputKey = typeof(DeviceSliderControl).GetMethod("IsInputKey", PrivateInstance)!;
        Assert.IsTrue((bool)isInputKey.Invoke(slider, [Keys.Up])!);
        var message = Message.Create(slider.Handle, 0x0100, (IntPtr)Keys.Up, IntPtr.Zero);

        var processed = slider.PreProcessMessage(ref message);

        Assert.IsFalse(processed, "An adjustment key should pass through dialog-key preprocessing to the control.");
        typeof(Control).GetMethod("WndProc", PrivateInstance)!.Invoke(slider, [message]);
        Assert.AreEqual(21, slider.LowerValue);
        Assert.AreEqual(80, slider.UpperValue);
        form.Close();
    }

    [STATestMethod]
    public void MouseDragClampsAtOtherThumbAndProgrammaticSetIsSilent()
    {
        using var slider = new DeviceSliderControl { IsRange = true, Size = new Size(220, 40) };
        slider.SetValues(20, 80);
        var changedEvents = 0;
        slider.ValuesChanged += (_, _) => changedEvents++;
        var valueToPoint = typeof(DeviceSliderControl).GetMethod("ValueToPoint", PrivateInstance)!;
        var mouseDown = typeof(DeviceSliderControl).GetMethod("OnMouseDown", PrivateInstance)!;
        var mouseMove = typeof(DeviceSliderControl).GetMethod("OnMouseMove", PrivateInstance)!;
        var mouseUp = typeof(DeviceSliderControl).GetMethod("OnMouseUp", PrivateInstance)!;
        var minimumPoint = (PointF)valueToPoint.Invoke(slider, [20])!;
        var beyondMaximum = (PointF)valueToPoint.Invoke(slider, [95])!;
        _ = slider.Handle;

        slider.SetValues(30, 70);
        Assert.AreEqual(0, changedEvents);
        slider.SetValues(20, 80);
        mouseDown.Invoke(slider, [new MouseEventArgs(MouseButtons.Left, 1, (int)minimumPoint.X, (int)minimumPoint.Y, 0)]);
        Assert.IsTrue(slider.IsBeingAdjusted);
        mouseMove.Invoke(slider, [new MouseEventArgs(MouseButtons.Left, 0, (int)beyondMaximum.X, (int)beyondMaximum.Y, 0)]);

        Assert.AreEqual(80, slider.LowerValue);
        Assert.AreEqual(80, slider.UpperValue);
        Assert.AreEqual(1, changedEvents);

        mouseUp.Invoke(slider, [new MouseEventArgs(MouseButtons.Left, 0, (int)beyondMaximum.X, (int)beyondMaximum.Y, 0)]);
        Assert.IsFalse(slider.IsBeingAdjusted);
    }

    [STATestMethod]
    public void MouseHitTestingSelectsNearestThumbAndLowerThumbWinsCoincidentTie()
    {
        using var slider = new DeviceSliderControl { IsRange = true, Size = new Size(220, 40) };
        slider.SetValues(20, 80);
        var valueToPoint = typeof(DeviceSliderControl).GetMethod("ValueToPoint", PrivateInstance)!;
        var mouseDown = typeof(DeviceSliderControl).GetMethod("OnMouseDown", PrivateInstance)!;
        var mouseMove = typeof(DeviceSliderControl).GetMethod("OnMouseMove", PrivateInstance)!;
        var mouseUp = typeof(DeviceSliderControl).GetMethod("OnMouseUp", PrivateInstance)!;
        var upperPoint = (PointF)valueToPoint.Invoke(slider, [80])!;
        var beyondUpper = (PointF)valueToPoint.Invoke(slider, [95])!;
        _ = slider.Handle;

        mouseDown.Invoke(slider, [new MouseEventArgs(MouseButtons.Left, 1, (int)upperPoint.X, (int)upperPoint.Y, 0)]);
        mouseMove.Invoke(slider, [new MouseEventArgs(MouseButtons.Left, 0, (int)beyondUpper.X, (int)beyondUpper.Y, 0)]);
        mouseUp.Invoke(slider, [new MouseEventArgs(MouseButtons.Left, 0, (int)beyondUpper.X, (int)beyondUpper.Y, 0)]);
        Assert.AreEqual(20, slider.LowerValue);
        Assert.AreEqual(95, slider.UpperValue);

        slider.SetValues(80, 80);
        var coincident = (PointF)valueToPoint.Invoke(slider, [80])!;
        mouseDown.Invoke(slider, [new MouseEventArgs(MouseButtons.Left, 1, (int)coincident.X, (int)coincident.Y, 0)]);
        mouseMove.Invoke(slider, [new MouseEventArgs(MouseButtons.Left, 0, (int)beyondUpper.X, (int)beyondUpper.Y, 0)]);
        mouseUp.Invoke(slider, [new MouseEventArgs(MouseButtons.Left, 0, (int)beyondUpper.X, (int)beyondUpper.Y, 0)]);
        Assert.AreEqual(80, slider.LowerValue);
        Assert.AreEqual(80, slider.UpperValue);
    }

    [STATestMethod]
    public void LosingMouseCaptureEndsDragWithoutMouseUp()
    {
        using var slider = new DeviceSliderControl { IsRange = true, Size = new Size(220, 40) };
        slider.SetValues(20, 80);
        var valueToPoint = typeof(DeviceSliderControl).GetMethod("ValueToPoint", PrivateInstance)!;
        var mouseDown = typeof(DeviceSliderControl).GetMethod("OnMouseDown", PrivateInstance)!;
        var minimumPoint = (PointF)valueToPoint.Invoke(slider, [20])!;
        _ = slider.Handle;

        mouseDown.Invoke(slider, [new MouseEventArgs(MouseButtons.Left, 1, (int)minimumPoint.X, (int)minimumPoint.Y, 0)]);
        Assert.IsTrue(slider.IsBeingAdjusted);

        slider.Capture = false;

        Assert.IsFalse(slider.IsBeingAdjusted);
    }

    [STATestMethod]
    public void RapidStrokeChangesSendOnlyTheNewestValue()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new StrokeWriteHandler();
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        SetPrivateField(form, "_handy", client);
        SetPrivateField(form, "_deviceControlsSupported", true);
        SetPrivateField(form, "_hasDeviceSlider", true);
        var slider = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;

        slider.SetValues(10, 90);
        InvokePrivate(form, "ScheduleStrokeWrite");
        slider.SetValues(25, 75);
        InvokePrivate(form, "ScheduleStrokeWrite");

        var wait = Stopwatch.StartNew();
        while (handler.WriteCount == 0 && wait.Elapsed < TimeSpan.FromSeconds(3))
            Application.DoEvents();

        Assert.AreEqual(1, handler.WriteCount);
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.AreEqual(0.25, body.RootElement.GetProperty("min").GetDouble());
        Assert.AreEqual(0.75, body.RootElement.GetProperty("max").GetDouble());
    }

    [STATestMethod]
    public void ConnectAdoptsDeviceValuesWithoutWritingSavedValuesBack()
    {
        using var form = new ScriptSyncPilot.HandySync.MainForm();
        using var handler = new ConnectReadHandler();
        using var client = new HandyApiClient("test-key", handler, new Uri("https://example.test/api/"));
        SetPrivateField(form, "_handy", client);
        SetPrivateField(form, "_deviceControlsSupported", true);
        SetPrivateField(form, "_hasDeviceSlider", true);
        var settings = (AppSettings)GetPrivateField(form, "_settings")!;
        settings.DeviceOffsetMs = -500;
        settings.StrokeMinPercent = 0;
        settings.StrokeMaxPercent = 100;

        InvokePrivateTask(form, "ReadAndAdoptDeviceControlsAsync", client).GetAwaiter().GetResult();
        ((System.Windows.Forms.Timer)GetPrivateField(form, "_devicePollTimer")!).Stop();

        var offsetSlider = (DeviceSliderControl)GetPrivateField(form, "_offsetSlider")!;
        var strokeSlider = (DeviceSliderControl)GetPrivateField(form, "_strokeSlider")!;
        Assert.AreEqual(250, offsetSlider.LowerValue);
        Assert.AreEqual(20, strokeSlider.LowerValue);
        Assert.AreEqual(80, strokeSlider.UpperValue);
        Assert.AreEqual(250, settings.DeviceOffsetMs);
        Assert.AreEqual(20, settings.StrokeMinPercent);
        Assert.AreEqual(80, settings.StrokeMaxPercent);
        Assert.IsTrue(handler.Methods.All(method => method == "GET"));
    }

    [TestMethod]
    public void LegacyOffsetIsIgnoredAndOmittedFromSerializedSettings()
    {
        var messages = new List<string>();
        var settings = AppSettings.Deserialize(
            "{\"OffsetMs\":725,\"StrokeMinPercent\":20,\"StrokeMaxPercent\":80}", messages.Add);

        Assert.AreEqual(0, settings.DeviceOffsetMs);
        Assert.AreEqual(20, settings.StrokeMinPercent);
        Assert.AreEqual(80, settings.StrokeMaxPercent);
        Assert.AreEqual(1, messages.Count);
        using var serialized = JsonDocument.Parse(settings.Serialize());
        Assert.IsFalse(serialized.RootElement.TryGetProperty("OffsetMs", out _));
        Assert.IsTrue(serialized.RootElement.TryGetProperty("DeviceOffsetMs", out _));

        foreach (var invalidLegacyValue in new[] { "null", "{\"OffsetMs\":\"725\"}" })
        {
            var ignored = AppSettings.Deserialize(invalidLegacyValue, messages.Add);
            Assert.AreEqual(0, ignored.DeviceOffsetMs);
        }
        Assert.AreEqual(1, messages.Count);
    }

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static object? GetPrivateField(object target, string name) =>
        target.GetType().GetField(name, PrivateInstance)!.GetValue(target);

    private static void SetPrivateField(object target, string name, object? value) =>
        target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);

    private static void InvokePrivate(object target, string name) =>
        target.GetType().GetMethod(name, PrivateInstance)!.Invoke(target, null);

    private static Task InvokePrivateTask(object target, string name, params object[] arguments) =>
        (Task)target.GetType().GetMethod(name, PrivateInstance)!.Invoke(target, arguments)!;

    private sealed class ConnectReadHandler : HttpMessageHandler
    {
        public List<string> Methods { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method.Method);
            var path = request.RequestUri!.AbsolutePath;
            var body = path switch
            {
                "/api/capabilities" => "{\"result\":{\"slider\":1}}",
                "/api/settings/slider" => "{\"result\":{\"x_limit_start\":0,\"x_limit_stop\":110,\"x_min_speed\":30,\"x_max_speed\":400,\"temp_high_trigger\":80,\"temp_hysteresis\":10}}",
                "/api/hstp/offset" => "{\"result\":{\"offset\":250}}",
                "/api/slider/stroke" => "{\"result\":{\"min\":0.2,\"max\":0.8,\"min_absolute\":25,\"max_absolute\":85}}",
                _ => throw new InvalidOperationException($"Unexpected request path: {path}")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StrokeWriteHandler : HttpMessageHandler
    {
        public int WriteCount { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            WriteCount++;
            using var body = JsonDocument.Parse(LastBody);
            var minimum = body.RootElement.GetProperty("min").GetDouble();
            var maximum = body.RootElement.GetProperty("max").GetDouble();
            var response = JsonSerializer.Serialize(new
            {
                result = new { min = minimum, max = maximum, min_absolute = minimum * 100, max_absolute = maximum * 100 }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }
}
