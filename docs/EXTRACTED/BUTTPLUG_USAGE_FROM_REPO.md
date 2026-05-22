# Buttplug Usage Extracted from ScriptPlayer Source

This file summarizes how the `ScriptPlayer` codebase uses Buttplug (useful as a local reference).

Files inspected
- `ScriptPlayer.Shared/Devices/ButtplugAdapter.cs`
- `ScriptPlayer.Shared/Devices/ButtplugDevice.cs`
- `ScriptPlayer/ScriptPlayer/ViewModels/MainViewModel.cs` (connection flow & UI hooks)

Key points
- The project uses the Buttplug C# client (`Buttplug.Client`) and references `Buttplug.dll`.
- Connection: `ButtplugClient.ConnectAsync(new ButtplugWebsocketConnector(new Uri(url)))`.
- Device lifecycle events handled: `DeviceAdded`, `DeviceRemoved`, `ErrorReceived`, `ServerDisconnect`.
- Common device commands used in code:
  - `device.VibrateAsync(speed)` — single-motor vibrate command.
  - `device.LinearAsync(durationMs, value)` — linear actuator command (stroke length/position over time).
  - `device.RotateAsync(speed, clockwise)` — rotation commands for compatible devices.
  - `device.Stop()` — stop device motion.
- Scanning and discovery: `client.StartScanningAsync()` and `StartScanning()` wrapper.
- The adapter serializes higher-level script events into Buttplug commands in `ButtplugAdapter.Set(...)`.
- Synchronization: adapter calls are coordinated with a scheduler in the Core/Player to maintain timing.

Implementation notes
- The adapter uses a `SemaphoreSlim` `_clientLock` to serialize sends to the Buttplug client and avoid concurrent access.
- Conversion logic maps script positions/speeds to device-specific parameters (handled in `CommandConverter` and `VibratorConversionMode`).
- The UI exposes controls to start/stop Buttplug server, set connection URL, and scanning behavior.

Where to look next
- To see the exact message shapes and capabilities supported by Buttplug, grab the official protocol docs (see `BUTTPLUG_NOTES.md`).
