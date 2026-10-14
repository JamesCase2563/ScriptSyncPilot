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

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static object? GetPrivateField(object target, string name) =>
        target.GetType().GetField(name, PrivateInstance)!.GetValue(target);

    private static void SetPrivateField(object target, string name, object? value) =>
        target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);

    private static void InvokePrivate(object target, string name) =>
        target.GetType().GetMethod(name, PrivateInstance)!.Invoke(target, null);

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
