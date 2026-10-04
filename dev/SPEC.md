# ScripySyncPilot — Project Specification

## Overview

ScripySyncPilot is a multi-component system for creating, editing, and playing time-synced device scripts alongside video. The system provides:
- a portable controller core that sends commands to devices (Buttplug-compatible, Bluetooth/Wi‑Fi gadgets),
- a multi-track script editor (timeline UI) for composing device tracks and events,
- a player that synchronizes script playback with video (and audio) with latency compensation,
- adapters and interfaces for external gadgets (GarminRally, tachometers, other BLE/WiFi sensors),
- optional GUI and web-based frontends for editing and playback control.

This document outlines the architecture, data model, components, integrations, and an initial MVP plan.

## Goals

- Provide a reliable, low-latency playback engine to drive devices in sync with video.
- Offer an intuitive multitrack editor for composing scripts (tracks per device/channel).
- Make device integration extensible via adapter/plugin architecture.
- Support both native GUI (desktop) and web-based remote control frontends.
- Centralize all device output through the Controller to maintain consistent firmware/driver state for devices (for example, The Handy via Buttplug).

## Non-Goals (initial)

- Implement every possible device integration at once — start with Buttplug and a generic BLE/Wi‑Fi adapter.
- Full-fledged audio/video analysis features (only minimal import/seek and scrub support in MVP).

## High-level Architecture

- `Core` — playback engine, scheduler, script file I/O, device adapter interfaces.
- `Editor` — multitrack timeline UI to create/edit scripts and map tracks to devices.
- `Player` — runtime that binds `Core` to video playback, manages latency compensation and transport.
- `Devices` — adapter layer implementing device-specific protocols (Buttplug, BLE, TCP/UDP). 
- `API` — IPC/HTTP/WebSocket API to control playback, query state, and expose device telemetry.
- `UI` — Desktop UI (WPF/WinUI or cross-platform Electron/MAUI) and optional Web UI (SPA + WebSocket).

Components map to repository modules/services:
- `ScriptSyncPilot.ControllerHost` — host for the Controller; provides a Windows desktop GUI for interactive device management and a headless/CLI host for service scenarios. Hosts adapters (Buttplug, BLE, TCP) and enforces firmware/version policies.
- `ScriptSyncPilot.Core` — library with scheduler, timeline model, transport, and plugin interfaces.
- `ScriptSyncPilot.Editor` — desktop editor app (timeline, track inspector, import/export).
- `ScriptSyncPilot.Player` — lightweight player app/embed for playback with video controls.
- `ScriptSyncPilot.Devices` — folder for adapters: `ButtplugAdapter`, `BluetoothAdapter`, `TcpAdapter`, `TelemetryAdapters`.
- `ScriptSyncPilot.API` — ASP.NET Core (or lightweight IPC) exposing WebSocket/REST for remote control and web UI.

## Controller — single authoritative output for devices

- The `Controller` (part of `ScriptSyncPilot.Core`) is the single authoritative output that sends commands to physical devices. All adapters, UIs, and external inputs MUST route device commands through the Controller rather than directly driving hardware.
- The Controller is hosted by `ScriptSyncPilot.ControllerHost`, which provides both a Windows desktop GUI implementation (for interactive device management, firmware checks, and on-screen status) and a headless/CLI host for automated or service scenarios. All UI and adapter integrations should interact with the Controller via the host's public APIs rather than opening device transports directly.
- Responsibilities of the Controller:
  - Host and manage the Buttplug client instance (or other device SDK clients) used to communicate with toys such as The Handy.
  - Enforce firmware/version and capability checks before sending commands, and provide an upgrade/compatibility policy surface to the UI.
  - Expose a well-defined async command API that adapters and the Player use to schedule commands; adapters provide telemetry and mapping but do not directly write to device transports.
  - Provide per-device latency/RTT measurement and apply compensation when scheduling device commands.
- Rationale: centralizing device output ensures consistent firmware versions, avoids conflicting direct access from multiple frontends or adapters, and simplifies capability negotiation and logging.

## Data Model & File Format

- Base script format: JSON-based schema inspired by FunScript but extended for multiple tracks and metadata.
- Top-level fields: `version`, `metadata`, `duration`, `tracks[]`.
- `track` fields: `id`, `name`, `deviceBinding` (adapter + channel), `events[]`.
- `event` fields: `timestampMs`, `type`, `value` (numeric, curve, or structured), `durationMs` (optional), `easing` (optional).
- Include `markers[]` and `videoReference` (path/URI + optional offset).
- Provide a JSON Schema (`docs/schema/script.schema.json`) for validation and tooling.

Example snippet:

```
{
  "version": "0.1",
  "metadata": { "author":"", "created":"" },
  "durationMs": 90000,
  "tracks": [ { "id":"t1", "name":"Main", "deviceBinding":{ "adapter":"buttplug", "channel":0 }, "events":[ ... ] } ]
}
```

## Editor Features (MVP → v1)

- Multi-track timeline with video scrub/seek and playhead.
- Add/drag/drop events, snap to markers, grid snapping, zoom/scroll timeline.
- Track inspector: map track → device adapter/channel, set scaling and curves.
- Import/Export FunScript and the project's JSON format.
- Basic undo/redo, save/load project files.

## Playback Engine

- Timebase driven scheduler with configurable transport (play, pause, stop, seek).
- High-resolution timer (sub-ms scheduling where possible) and batching of device commands.
- Latency compensation per-device (measured RTT / estimated command latency).
- Queueing and back-pressure handling to avoid flooding devices.
- Video sync: listen to video player's playback position (via API) and drive scheduler accordingly.

## Device Integration & Adapters

- Adapter interface contract:
  - `Connect()`, `Disconnect()`, `SendCommand(command)`, `QueryState()`.
  - Support async operations and cancellation.
- Initial adapters:
  - `ButtplugAdapter` — use the Buttplug C# client for supported toys.
  - `BluetoothAdapter` — generic BLE support for sensors and simple actuators.
  - `TcpUdpAdapter` — for Wi‑Fi enabled gadgets exposing simple TCP/UDP APIs.
  - `TelemetryAdapter` — ingest telemetry from third-party gadgets (e.g., GarminRally) and expose mapped events.
- Mapping layer: telemetry → script events (e.g., tachometer RPM → intensity on a track).

## External Device Integration Notes

- GarminRally and similar devices: if they expose BLE characteristics or a Bluetooth LE API, implement an adapter that reads telemetry and publishes normalized events.
- If devices expose network endpoints, use `TcpUdpAdapter` to poll or subscribe.
- Provide developer docs and an adapter template in `ScriptSyncPilot.Devices/Templates`.

## API and Remote Control

- Provide a WebSocket API for real-time transport control and telemetry streaming.
- Provide a minimal REST surface for project CRUD and adapter management.
- Authentication: optionally allow a local token for remote web UI access.

## UI Options & Recommendations

- Desktop (MVP): implement the Editor as a WPF (Windows) or MAUI (cross-platform) app to leverage existing .NET codebase.
- Web UI (v1+): SPA (React/Blazor) that connects via WebSocket for realtime playback control.
- Keep UI and Core decoupled — UI talks to Core via API/IPC only.

## Tech Stack Recommendations

- Language/runtime: .NET 6 or later (align with existing repo), C# for core and desktop UI.
- Use Buttplug C# client for supported toy control.
- For Web UI: static SPA served by `ScriptSyncPilot.API` with WebSocket control.
- Data persistence: local filesystem JSON project files; optionally an indexed database (LiteDB) later.

## Security & Privacy

- Device control APIs must be local-only by default; require user consent before exposing to network.
- Store no unnecessary telemetry by default; provide opt-in settings for logs/telemetry.

## MVP Roadmap

1. Core playback engine + JSON script schema + Buttplug adapter. (Minimal player CLI + API)
2. Desktop player with video sync (embed video player + playback controls).
3. Simple multitrack editor with save/load and FunScript import/export.
4. WebSocket API + simple Web UI remote control.
5. Adapters for BLE/TCP devices and a telemetry adapter for tachometers/Garmin.

## Deliverables (first sprint)

- `docs/SPEC.md` (this file).
- `docs/schema/script.schema.json` (JSON Schema).
- Project skeleton folders: `ScriptSyncPilot.ControllerHost`, `ScriptSyncPilot.Core`, `ScriptSyncPilot.Editor`, `ScriptSyncPilot.Player`, `ScriptSyncPilot.Devices`, `ScriptSyncPilot.API`.
- A minimal prototype: Core library + ButtplugAdapter + a CLI player that can load a JSON script and print scheduled commands (no device connected).

## Next Steps

1. Create project skeleton (repositories/projects) and update `ScriptSyncPilot.code-workspace`.
2. Implement the JSON schema and a small serializer/validator in the Core library.
3. Prototype the scheduler and a CLI player to validate timing and scheduling design.

---

If you'd like, I can create the project skeleton next (projects, csproj files, initial classes) and add the JSON schema. Tell me which MVP items to prioritize.
